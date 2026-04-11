using OcrApi.Models;
using OcrApi.Services;
using Xunit;

namespace OcrApi.Tests;

public class FieldMatcherTests
{
    private readonly IFieldMatcher _matcher = new FieldMatcher();

    /// <summary>
    /// Creates an ExtractTextResponse whose TextBlocks contain the supplied text
    /// as a single block (FieldMatcher joins blocks, not RawText).
    /// </summary>
    private static ExtractTextResponse OcrResult(string rawText) => new()
    {
        Success          = true,
        PageCount        = 1,
        RawText          = rawText,
        TextBlocks       = string.IsNullOrEmpty(rawText)
            ? new List<OcrTextBlock>()
            : new List<OcrTextBlock>
            {
                new OcrTextBlock { Text = rawText, Confidence = 95f, Page = 1 }
            },
        ProcessingTimeMs = 0f,
    };

    private static OcrProperty Property(string name, string? heuristic) => new()
    {
        Id              = 1,
        Name            = name,
        DataType        = "string",
        SearchHeuristic = heuristic,
        IsActive        = true,
    };

    [Fact]
    public void MatchFields_PropertyWithMatchingPattern_ReturnsMatch()
    {
        var props  = new List<OcrProperty> { Property("InvoiceNumber", @"INV-\d+") };
        var result = OcrResult("Reference: INV-00423 dated today");

        var fields = _matcher.MatchFields(props, result);

        var field = Assert.Single(fields);
        Assert.Equal("InvoiceNumber", field.PropertyName);
        Assert.Equal("INV-00423", field.ExtractedValue);
    }

    [Fact]
    public void MatchFields_PropertyWithNoMatch_ReturnsNullValue()
    {
        var props  = new List<OcrProperty> { Property("InvoiceNumber", @"INV-\d+") };
        var result = OcrResult("No invoice number here");

        var fields = _matcher.MatchFields(props, result);

        var field = Assert.Single(fields);
        Assert.Null(field.ExtractedValue);
    }

    [Fact]
    public void MatchFields_PropertyWithNoHeuristic_ReturnsNullValue()
    {
        var props  = new List<OcrProperty> { Property("Signature", null) };
        var result = OcrResult("John Smith signed here");

        var fields = _matcher.MatchFields(props, result);

        var field = Assert.Single(fields);
        Assert.Null(field.ExtractedValue);
    }

    [Fact]
    public void MatchFields_EmptyRawText_ReturnsAllNulls()
    {
        var props  = new List<OcrProperty> { Property("Date", @"\d{2}/\d{2}/\d{4}") };
        var result = OcrResult(string.Empty);

        var fields = _matcher.MatchFields(props, result);

        Assert.Single(fields);
        Assert.Null(fields[0].ExtractedValue);
    }

    [Fact]
    public void MatchFields_MultipleProperties_MatchesEach()
    {
        var props = new List<OcrProperty>
        {
            Property("Date",  @"\d{2}/\d{2}/\d{4}"),
            Property("Total", @"\$[\d,]+\.\d{2}"),
        };
        var result = OcrResult("Date: 03/15/2026 Total: $1,234.56");

        var fields = _matcher.MatchFields(props, result);

        Assert.Equal(2, fields.Count);
        Assert.Equal("03/15/2026", fields.First(f => f.PropertyName == "Date").ExtractedValue);
        Assert.Equal("$1,234.56",  fields.First(f => f.PropertyName == "Total").ExtractedValue);
    }
}
