using OcrApi.Models;

namespace OcrApi.Services;

public interface IOcrClient
{
    /// <summary>
    /// Forwards the uploaded file to the Python OCR engine and returns the
    /// structured text-extraction response.
    /// </summary>
    Task<ExtractTextResponse> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        int? cropX,
        int? cropY,
        int? cropWidth,
        int? cropHeight,
        CancellationToken ct = default);
}
