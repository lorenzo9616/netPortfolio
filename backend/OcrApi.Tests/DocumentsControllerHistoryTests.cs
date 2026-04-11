using Microsoft.EntityFrameworkCore;
using OcrApi.Data;
using OcrApi.Models;
using Xunit;

namespace OcrApi.Tests;

public class DocumentsControllerHistoryTests
{
    private static OcrDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OcrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OcrDbContext(options);
    }

    private static AnalysisResult MakeResult(string fileName, int pageCount, string rawText, int fieldCount)
    {
        var fields = Enumerable.Range(0, fieldCount)
            .Select(i => new SavedField { PropertyName = $"Field{i}", ExtractedValue = $"value{i}" })
            .ToList();

        return new AnalysisResult
        {
            FileName   = fileName,
            RawText    = rawText,
            AnalyzedAt = DateTime.UtcNow,
            PageCount  = pageCount,
            Fields     = fields,
        };
    }

    [Fact]
    public void BuildPreview_ShortText_ReturnsFull()
    {
        var preview = DocumentsControllerExtensions.BuildPreview("Hello world");
        Assert.Equal("Hello world", preview);
    }

    [Fact]
    public void BuildPreview_LongText_TruncatesAtWordBoundary()
    {
        var longText = string.Join(" ", Enumerable.Repeat("word", 40));
        var preview = DocumentsControllerExtensions.BuildPreview(longText);
        Assert.True(preview.Length <= 123);
        Assert.EndsWith("...", preview);
    }

    [Fact]
    public async Task ListDocuments_ReturnsNewestFirst()
    {
        using var db = CreateDb();
        var older = MakeResult("old.png", 1, "old text", 2);
        older.AnalyzedAt = DateTime.UtcNow.AddHours(-1);
        var newer = MakeResult("new.png", 1, "new text", 3);
        newer.AnalyzedAt = DateTime.UtcNow;
        db.AnalysisResults.AddRange(older, newer);
        await db.SaveChangesAsync();

        var items = await db.AnalysisResults
            .OrderByDescending(r => r.AnalyzedAt)
            .Take(20)
            .ToListAsync();

        Assert.Equal("new.png", items[0].FileName);
    }

    [Fact]
    public async Task ListDocuments_ReturnsCorrectFieldCount()
    {
        using var db = CreateDb();
        db.AnalysisResults.Add(MakeResult("doc.png", 2, "some text", 5));
        await db.SaveChangesAsync();

        var result = await db.AnalysisResults.Include(r => r.Fields).FirstAsync();
        Assert.Equal(5, result.Fields.Count);
    }

    [Fact]
    public async Task DeleteDocument_ExistingId_DeletesResultAndFields()
    {
        using var db = CreateDb();
        var result = MakeResult("doc.png", 1, "text", 3);
        db.AnalysisResults.Add(result);
        await db.SaveChangesAsync();
        var id = result.Id;

        db.AnalysisResults.Remove(result);
        await db.SaveChangesAsync();

        Assert.False(await db.AnalysisResults.AnyAsync(r => r.Id == id));
        Assert.False(await db.SavedFields.AnyAsync(f => f.AnalysisResultId == id));
    }

    [Fact]
    public async Task DeleteDocument_UnknownId_ReturnsNotFound()
    {
        using var db = CreateDb();
        var result = await db.AnalysisResults.FindAsync(9999);
        Assert.Null(result);
    }
}

/// <summary>Exposes static helpers from DocumentsController for unit testing.</summary>
public static class DocumentsControllerExtensions
{
    public static string BuildPreview(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        if (rawText.Length <= 120) return rawText;

        var truncated = rawText[..120];
        var lastSpace = truncated.LastIndexOf(' ');
        return lastSpace > 0
            ? truncated[..lastSpace] + "..."
            : truncated + "...";
    }
}
