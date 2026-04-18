using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OcrApi.Models;

namespace OcrApi.Services;

public class OcrClient : IOcrClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<OcrClient> _logger;

    // HttpClient is injected by IHttpClientFactory via the typed-client registration
    // (AddHttpClient<IOcrClient, OcrClient>). Do NOT dispose it — the factory
    // manages its lifetime and connection pooling.
    public OcrClient(HttpClient httpClient, ILogger<OcrClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ExtractTextResponse> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        int? cropX,
        int? cropY,
        int? cropWidth,
        int? cropHeight,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "OcrClient: starting ExtractTextAsync for file '{FileName}' ({ContentType})",
            fileName, contentType);

        using var multipart = new MultipartFormDataContent();

        // ── File part ─────────────────────────────────────────────────────────
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);

        // ── Optional crop region ──────────────────────────────────────────────
        if (cropX.HasValue && cropY.HasValue && cropWidth.HasValue && cropHeight.HasValue)
        {
            // FastAPI form field names use snake_case — must match the Python parameter names exactly
            multipart.Add(new StringContent(cropX.Value.ToString()),      "crop_x");
            multipart.Add(new StringContent(cropY.Value.ToString()),      "crop_y");
            multipart.Add(new StringContent(cropWidth.Value.ToString()),  "crop_width");
            multipart.Add(new StringContent(cropHeight.Value.ToString()), "crop_height");
        }

        // ── POST to OCR engine ────────────────────────────────────────────────
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("/extract-text", multipart, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "OcrClient: network error reaching OCR service");
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            int statusCode = (int)response.StatusCode;
            if (statusCode >= 400 && statusCode < 500)
            {
                _logger.LogWarning(
                    "OcrClient: OCR service returned 4xx status {StatusCode} for '{FileName}'",
                    statusCode, fileName);
            }
            else
            {
                _logger.LogError(
                    "OcrClient: OCR service returned 5xx status {StatusCode} for '{FileName}'",
                    statusCode, fileName);
            }

            throw new HttpRequestException(
                $"OCR service responded with {statusCode}.",
                inner: null,
                statusCode: response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<ExtractTextResponse>(
            _jsonOptions, ct)
            ?? throw new InvalidOperationException("OCR service returned an empty response body.");

        _logger.LogInformation(
            "OcrClient: ExtractTextAsync complete — {PageCount} page(s), {BlockCount} text block(s), {Ms} ms",
            result.PageCount, result.TextBlocks.Count, result.ProcessingTimeMs);

        return result;
    }
}
