using System.Text.RegularExpressions;
using OcrApi.Models;

namespace OcrApi.Services;

/// <summary>
/// Stateless singleton that maps OCR text output to <see cref="OcrProperty"/>
/// definitions using regex or keyword heuristics.
/// </summary>
public class FieldMatcher : IFieldMatcher
{
    // Maximum chars to extract after a keyword match.
    private const int KeywordWindowSize = 50;

    // Y-coordinate bucket size (px) used when grouping text blocks into lines.
    private const int LineBucketSize = 12;

    public List<ExtractedField> MatchFields(
        IEnumerable<OcrProperty> properties,
        ExtractTextResponse ocrResult)
        => MatchFieldsCore(properties, ReconstructText(ocrResult));

    public List<ExtractedField> MatchFields(
        IEnumerable<OcrProperty> properties,
        string rawText)
        => MatchFieldsCore(properties, rawText);

    private static List<ExtractedField> MatchFieldsCore(
        IEnumerable<OcrProperty> properties, string rawText)
    {
        var results = new List<ExtractedField>();
        foreach (var property in properties)
            results.Add(MatchSingleField(property, rawText));
        return results;
    }

    // ── Text reconstruction ───────────────────────────────────────────────────

    // Groups words into lines by Y-coordinate proximity, preserving reading
    // order (top-to-bottom, left-to-right). Lines are joined with \n so that
    // keyword and regex patterns don't bleed across unrelated rows.
    private static string ReconstructText(ExtractTextResponse ocrResult)
    {
        var lines = ocrResult.TextBlocks
            .GroupBy(b => b.BoundingBox.Y / LineBucketSize)
            .OrderBy(g => g.Key)
            .Select(g => string.Join(" ", g.OrderBy(b => b.BoundingBox.X).Select(b => b.Text)));
        return string.Join("\n", lines);
    }

    // ── Field matching ────────────────────────────────────────────────────────

    private static ExtractedField MatchSingleField(OcrProperty property, string rawText)
    {
        var heuristic = property.SearchHeuristic;

        if (string.IsNullOrWhiteSpace(heuristic))
        {
            return new ExtractedField
            {
                PropertyName   = property.Name,
                ExtractedValue = null,
                Confidence     = 0.0
            };
        }

        if (property.IsRegex)
        {
            var match = Regex.Match(rawText, heuristic, RegexOptions.IgnoreCase);
            if (!match.Success)
                return new ExtractedField { PropertyName = property.Name, ExtractedValue = null, Confidence = 0.0 };

            // Prefer the first capture group so users can write e.g. "DOB:\s*(.+)"
            // and get only the value, not the surrounding label.
            var raw = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
            return new ExtractedField
            {
                PropertyName   = property.Name,
                ExtractedValue = NormalizeValue(raw, property.DataType),
                Confidence     = 0.9
            };
        }

        return KeywordMatch(property.Name, property.DataType, heuristic, rawText);
    }

    private static ExtractedField KeywordMatch(
        string propertyName, string dataType, string keyword, string rawText)
    {
        var idx = rawText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return new ExtractedField
            {
                PropertyName   = propertyName,
                ExtractedValue = null,
                Confidence     = 0.0
            };
        }

        // Skip past the keyword and any trailing colon/whitespace separator so
        // the extracted value doesn't include the label (e.g. "Name: " prefix).
        var valueStart = idx + keyword.Length;
        while (valueStart < rawText.Length &&
               (rawText[valueStart] == ':' || rawText[valueStart] == ' ' || rawText[valueStart] == '\t'))
        {
            valueStart++;
        }

        var length = Math.Min(KeywordWindowSize, rawText.Length - valueStart);
        if (length <= 0)
            return new ExtractedField { PropertyName = propertyName, ExtractedValue = null, Confidence = 0.0 };

        var window = rawText.Substring(valueStart, length);

        // Stop at the next reconstructed line boundary so we don't merge
        // a value with content from an unrelated line below it.
        var newlineIdx = window.IndexOf('\n');
        if (newlineIdx >= 0)
            window = window.Substring(0, newlineIdx);

        window = window.Trim();
        if (string.IsNullOrEmpty(window))
            return new ExtractedField { PropertyName = propertyName, ExtractedValue = null, Confidence = 0.0 };

        return new ExtractedField
        {
            PropertyName   = propertyName,
            ExtractedValue = NormalizeValue(window, dataType),
            Confidence     = 0.5
        };
    }

    // ── DataType-aware normalization ──────────────────────────────────────────

    private static string? NormalizeValue(string? value, string dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return dataType.ToLowerInvariant() switch
        {
            "date"                               => TryNormalizeDate(value),
            "decimal" or "currency" or "number"  => NormalizeDecimal(value),
            _                                    => value.Trim()
        };
    }

    private static string TryNormalizeDate(string value)
    {
        if (DateTime.TryParse(value.Trim(), out var dt))
            return dt.ToString("yyyy-MM-dd");
        return value.Trim();
    }

    // Strips everything that isn't a digit, decimal separator, or minus sign.
    // Handles common OCR artifacts like currency symbols and stray spaces.
    private static string NormalizeDecimal(string value) =>
        Regex.Replace(value.Trim(), @"[^\d.,\-]", "");
}
