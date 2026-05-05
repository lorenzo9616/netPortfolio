namespace OcrApi.Services;

public record SuggestedProperty(
    string Name,
    string DataType,
    string SearchHeuristic,
    bool IsRegex
);

public record ClassificationResult(
    string DocumentType,
    List<SuggestedProperty> SuggestedProperties
);

public interface IDocumentClassifierService
{
    Task<ClassificationResult> ClassifyAsync(string rawText, CancellationToken ct = default);
}
