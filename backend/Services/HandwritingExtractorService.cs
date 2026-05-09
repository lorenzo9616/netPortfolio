using System.Text;
using System.Text.Json;
using OcrApi.Models;

namespace OcrApi.Services;

public class HandwritingExtractorService : IHandwritingExtractorService
{
    private readonly IOcrClient _ocrClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HandwritingExtractorService> _logger;

    public HandwritingExtractorService(
        IOcrClient ocrClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HandwritingExtractorService> logger)
    {
        _ocrClient          = ocrClient;
        _httpClientFactory  = httpClientFactory;
        _configuration      = configuration;
        _logger             = logger;
    }

    public async Task<HandwritingExtractionResult> ExtractAsync(
        byte[] fileBytes,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        // ── 1. Get page images from OCR service (PDF→images, no Tesseract) ───────
        ExtractPagesResponse pages;
        try
        {
            pages = await _ocrClient.ExtractPagesAsync(
                new MemoryStream(fileBytes), fileName, contentType, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HandwritingExtractorService: extract-pages failed — returning empty result");
            return new HandwritingExtractionResult { RawText = string.Empty, PageCount = 0 };
        }

        if (pages.PageImages.Count == 0)
            return new HandwritingExtractionResult { RawText = string.Empty, PageCount = 0 };

        var apiKey = _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("HandwritingExtractorService: Anthropic:ApiKey not configured — returning empty transcription");
            return new HandwritingExtractionResult
            {
                RawText    = string.Empty,
                PageImages = pages.PageImages,
                PageCount  = pages.PageCount,
            };
        }

        // ── 2. Transcribe each page with Claude Haiku Vision ─────────────────────
        var pageTexts = new List<string>();
        var client    = _httpClientFactory.CreateClient("anthropic");

        for (int i = 0; i < pages.PageImages.Count; i++)
        {
            var pageText = await TranscribePageAsync(client, apiKey, pages.PageImages[i], i + 1, ct);
            pageTexts.Add(pageText);
        }

        var rawText = pageTexts.Count == 1
            ? pageTexts[0]
            : string.Join("\n\n", pageTexts.Select((t, i) => $"--- Page {i + 1} ---\n{t}"));

        return new HandwritingExtractionResult
        {
            RawText    = rawText,
            PageImages = pages.PageImages,
            PageCount  = pages.PageCount,
        };
    }

    private async Task<string> TranscribePageAsync(
        HttpClient client, string apiKey, string base64Jpeg, int pageNum, CancellationToken ct)
    {
        var requestBody = new
        {
            model      = "claude-haiku-4-5-20251001",
            max_tokens = 4096,
            messages   = new[]
            {
                new
                {
                    role    = "user",
                    content = new object[]
                    {
                        new
                        {
                            type   = "image",
                            source = new
                            {
                                type       = "base64",
                                media_type = "image/jpeg",
                                data       = base64Jpeg,
                            },
                        },
                        new
                        {
                            type = "text",
                            text = "Transcribe all handwritten and printed text exactly as written. Preserve line breaks. Return plain text only.",
                        },
                    },
                },
            },
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Add("x-api-key", apiKey);

            var response     = await client.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "HandwritingExtractorService: Anthropic returned {Status} for page {Page} — {Body}",
                    (int)response.StatusCode, pageNum,
                    responseBody.Length > 200 ? responseBody[..200] : responseBody);
                return string.Empty;
            }

            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HandwritingExtractorService: transcription failed for page {Page}", pageNum);
            return string.Empty;
        }
    }
}
