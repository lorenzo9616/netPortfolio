using OcrApi.Models;

namespace OcrApi.Services;

public interface IHandwritingExtractorService
{
    Task<HandwritingExtractionResult> ExtractAsync(
        byte[] fileBytes,
        string fileName,
        string contentType,
        CancellationToken ct = default);
}
