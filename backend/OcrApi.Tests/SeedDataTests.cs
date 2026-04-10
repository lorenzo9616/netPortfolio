using Microsoft.EntityFrameworkCore;
using OcrApi.Data;
using OcrApi.Models;
using Xunit;

namespace OcrApi.Tests;

public class SeedDataTests
{
    private static OcrDbContext CreateEmptyDb()
    {
        var options = new DbContextOptionsBuilder<OcrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new OcrDbContext(options);
    }

    [Fact]
    public void Initialize_WhenAnalysisResultsEmpty_InsertsOneSampleResult()
    {
        using var db = CreateEmptyDb();
        SeedData.Initialize(db);
        Assert.Equal(1, db.AnalysisResults.Count());
    }

    [Fact]
    public void Initialize_WhenAnalysisResultsEmpty_SampleResultHasFields()
    {
        using var db = CreateEmptyDb();
        SeedData.Initialize(db);
        var result = db.AnalysisResults.Include(r => r.Fields).First();
        Assert.NotEmpty(result.Fields);
    }

    [Fact]
    public void Initialize_WhenAnalysisResultsNotEmpty_DoesNotInsert()
    {
        using var db = CreateEmptyDb();
        db.AnalysisResults.Add(new AnalysisResult
        {
            FileName   = "existing.png",
            RawText    = "existing text",
            AnalyzedAt = DateTime.UtcNow,
            PageCount  = 1,
        });
        db.SaveChanges();
        SeedData.Initialize(db);
        Assert.Equal(1, db.AnalysisResults.Count());
    }
}
