using OcrApi.Models;

namespace OcrApi.Data;

/// <summary>
/// Inserts a sample AnalysisResult on first boot so testers see real data
/// immediately without having to upload a document.
/// Only runs when the AnalysisResults table is empty.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Seeds one sample <see cref="AnalysisResult"/> if the table is empty.
    /// No-op when at least one row already exists.
    /// </summary>
    /// <param name="db">An open <see cref="OcrDbContext"/> with pending migrations applied.</param>
    public static void Initialize(OcrDbContext db)
    {
        if (db.AnalysisResults.Any())
            return;

        db.AnalysisResults.Add(new AnalysisResult
        {
            FileName   = "sample-invoice.png",
            RawText    = "Invoice INV-00423 dated 03/15/2026 Total $1,234.56 Processing Fee $12.35 John Smith",
            AnalyzedAt = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc),
            PageCount  = 1,
            Fields     = new List<SavedField>
            {
                new() { PropertyName = "Signature",     ExtractedValue = null,        Confidence = 0.0 },
                new() { PropertyName = "FullName",      ExtractedValue = "John Smith", Confidence = 0.82 },
                new() { PropertyName = "DateOfBirth",   ExtractedValue = "03/15/2026", Confidence = 0.91 },
                new() { PropertyName = "BillingTotal",  ExtractedValue = "$1,234.56",  Confidence = 0.95 },
                new() { PropertyName = "ProcessingFee", ExtractedValue = "$12.35",     Confidence = 0.93 },
            }
        });

        db.SaveChanges();
    }
}
