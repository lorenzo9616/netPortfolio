using System.Text.RegularExpressions;
using OcrApi.Models;

namespace OcrApi.Services;

/// <summary>
/// Stateless singleton that maps OCR text output to <see cref="OcrProperty"/>
/// definitions using regex or keyword heuristics.
/// </summary>
public class FieldMatcher : IFieldMatcher
{
    // Window size (chars) used when extracting context around a keyword hit.
    private const int KeywordWindowSize = 50;

    public List<ExtractedField> MatchFields(
        IEnumerable<OcrProperty> properties,
        ExtractTextResponse ocrResult)
    {
        // Join all text blocks into one searchable string.
        var rawText = string.Join(" ", ocrResult.TextBlocks.Select(b => b.Text));

        var results = new List<ExtractedField>();

        foreach (var property in properties)
        {
            var field = MatchSingleField(property, rawText);
            results.Add(field);
        }

        return results;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static ExtractedField MatchSingleField(OcrProperty property, string rawText)
    {
        var heuristic = property.SearchHeuristic;

        // No heuristic configured — cannot extract.
        if (string.IsNullOrWhiteSpace(heuristic))
        {
            return new ExtractedField
            {
                PropertyName   = property.Name,
                ExtractedValue = null,
                Confidence     = 0.0
            };
        }

        // Try regex first; catch ArgumentException for invalid patterns and
        // fall back to keyword matching.
        try
        {
            var match = Regex.Match(rawText, heuristic, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return new ExtractedField
                {
                    PropertyName   = property.Name,
                    ExtractedValue = match.Value,
                    Confidence     = 0.9
                };
            }
        }
        catch (ArgumentException)
        {
            // heuristic is not a valid regex — fall through to keyword search.
        }

        // Keyword / plain-text fallback.
        return KeywordMatch(property.Name, heuristic, rawText);
    }

    private static ExtractedField KeywordMatch(
        string propertyName, string keyword, string rawText)
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

        // Extract a window of up to KeywordWindowSize characters starting at
        // the match position (bounded by the string length).
        var start  = idx;
        var length = Math.Min(KeywordWindowSize, rawText.Length - start);
        var window = rawText.Substring(start, length).Trim();

        return new ExtractedField
        {
            PropertyName   = propertyName,
            ExtractedValue = window,
            Confidence     = 0.5
        };
    }
}
