using OcrApi.Models;

namespace OcrApi.Services;

public interface IOcrClient
{
    Task<ExtractTextResponse> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        int? cropX,
        int? cropY,
        int? cropWidth,
        int? cropHeight,
        string lang = "eng",
        CancellationToken ct = default);

    Task<HttpResponseMessage> ExtractTextStreamAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        int? cropX,
        int? cropY,
        int? cropWidth,
        int? cropHeight,
        string lang = "eng",
        CancellationToken ct = default);
}
