using System.Text.Json;

namespace OcrApi.Services;

public class DocumentClassifierService : IDocumentClassifierService
{
    private static readonly ClassificationResult _emptyResult = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentClassifierService> _logger;

    public DocumentClassifierService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DocumentClassifierService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration     = configuration;
        _logger            = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(string rawText, CancellationToken ct = default)
    {
        var apiKey = _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("DocumentClassifierService: Anthropic:ApiKey not configured — skipping AI classification");
            return _emptyResult;
        }

        var truncated = rawText.Length > 2000 ? rawText[..2000] : rawText;

        const string systemPrompt = """
            You are a document classifier. Analyze OCR text and respond with ONLY valid JSON, no other text.
            Identify the document type and suggest up to 8 field extraction properties specific to this document.

            Required JSON format:
            {
              "documentType": "Invoice",
              "suggestedProperties": [
                { "name": "VendorName", "dataType": "string", "searchHeuristic": "vendor|from|seller", "isRegex": false },
                { "name": "TotalDue", "dataType": "decimal", "searchHeuristic": "total\\s*due[:\\s]*\\$?([\\d,\\.]+)", "isRegex": true }
              ]
            }

            Rules:
            - documentType: short name (Invoice, Receipt, Medical Record, Legal Contract, ID Document, Meeting Minutes, Other)
            - name: PascalCase, no spaces
            - dataType: string | date | decimal | number
            - isRegex true → regex with one capture group for the value; false → keyword/phrase to search for
            - Avoid duplicating these already-standard properties: FullName, DateOfBirth, Signature, InvoiceNumber, InvoiceDate, DueDate, AccountNumber, TaxAmount
            - Return at most 8 suggestedProperties
            """;

        var requestBody = new
        {
            model      = "claude-haiku-4-5-20251001",
            max_tokens = 1024,
            system     = systemPrompt,
            messages   = new[] { new { role = "user", content = $"Classify this document:\n\n{truncated}" } }
        };

        try
        {
            var client = _httpClientFactory.CreateClient("anthropic");

            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages");
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            request.Headers.Add("x-api-key", apiKey);

            var response     = await client.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "DocumentClassifierService: Anthropic API returned {Status} — {Body}",
                    (int)response.StatusCode, responseBody.Length > 200 ? responseBody[..200] : responseBody);
                return _emptyResult;
            }

            using var doc  = JsonDocument.Parse(responseBody);
            var textContent = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "{}";

            var cleaned = textContent.Trim();
            if (cleaned.StartsWith("```"))
            {
                var newline = cleaned.IndexOf('\n');
                if (newline >= 0) cleaned = cleaned[(newline + 1)..];
                var lastFence = cleaned.LastIndexOf("```");
                if (lastFence >= 0) cleaned = cleaned[..lastFence].Trim();
            }

            var opts   = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<ClassificationResult>(cleaned, opts);

            return result ?? _emptyResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DocumentClassifierService: classification failed — continuing without AI properties");
            return _emptyResult;
        }
    }
}
