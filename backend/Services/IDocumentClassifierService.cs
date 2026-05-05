using System.Text.Json.Serialization;

namespace OcrApi.Services;

public record SuggestedProperty
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("dataType")]
    public string DataType { get; init; } = string.Empty;

    [JsonPropertyName("searchHeuristic")]
    public string SearchHeuristic { get; init; } = string.Empty;

    [JsonPropertyName("isRegex")]
    public bool IsRegex { get; init; }
}

public record ClassificationResult
{
    [JsonPropertyName("documentType")]
    public string DocumentType { get; init; } = "Unknown";

    [JsonPropertyName("suggestedProperties")]
    public List<SuggestedProperty> SuggestedProperties { get; init; } = new();
}

public interface IDocumentClassifierService
{
    Task<ClassificationResult> ClassifyAsync(string rawText, CancellationToken ct = default);
}
