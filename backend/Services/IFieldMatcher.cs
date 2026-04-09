using OcrApi.Models;

namespace OcrApi.Services;

public interface IFieldMatcher
{
    /// <summary>
    /// Matches OCR text blocks against the configured property heuristics and
    /// returns one <see cref="ExtractedField"/> per property.
    /// </summary>
    List<ExtractedField> MatchFields(
        IEnumerable<OcrProperty> properties,
        ExtractTextResponse ocrResult);
}
