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
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
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
        string lang = "eng",
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

        // ── Language and optional crop region ─────────────────────────────────
        multipart.Add(new StringContent(lang), "lang");

        if (cropX.HasValue && cropY.HasValue && cropWidth.HasValue && cropHeight.HasValue)
        {
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

    public async Task<HttpResponseMessage> ExtractTextStreamAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        int? cropX,
        int? cropY,
        int? cropWidth,
        int? cropHeight,
        string lang = "eng",
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "OcrClient: starting ExtractTextStreamAsync for file '{FileName}' ({ContentType})",
            fileName, contentType);

        var multipart   = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        multipart.Add(new StringContent(lang), "lang");

        if (cropX.HasValue && cropY.HasValue && cropWidth.HasValue && cropHeight.HasValue)
        {
            multipart.Add(new StringContent(cropX.Value.ToString()),      "crop_x");
            multipart.Add(new StringContent(cropY.Value.ToString()),      "crop_y");
            multipart.Add(new StringContent(cropWidth.Value.ToString()),  "crop_width");
            multipart.Add(new StringContent(cropHeight.Value.ToString()), "crop_height");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/extract-text-stream")
        {
            Content = multipart,
        };

        // ResponseHeadersRead: HttpClient returns as soon as response headers arrive,
        // body is streamed by the caller.  Timeout applies only to the header phase.
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    public async Task<ExtractPagesResponse> ExtractPagesAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "OcrClient: starting ExtractPagesAsync for file '{FileName}' ({ContentType})",
            fileName, contentType);

        using var multipart  = new MultipartFormDataContent();
        var fileContent      = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("/extract-pages", multipart, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "OcrClient: network error reaching OCR service (extract-pages)");
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OcrClient: OCR service returned {StatusCode} for extract-pages '{FileName}'",
                (int)response.StatusCode, fileName);
            throw new HttpRequestException(
                $"OCR service responded with {(int)response.StatusCode}.",
                inner: null,
                statusCode: response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<ExtractPagesResponse>(_jsonOptions, ct)
            ?? throw new InvalidOperationException("OCR service returned an empty response for extract-pages.");

        _logger.LogInformation(
            "OcrClient: ExtractPagesAsync complete — {PageCount} page(s)", result.PageCount);

        return result;
    }
}
