namespace OcrApi.Models;

public class AnalysisResult
{
    public int Id { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public string FileName { get; set; } = string.Empty;
    public string? RawText { get; set; }
    public int PageCount { get; set; } = 1;
    public List<SavedField> Fields { get; set; } = new();
}

public class SavedField
{
    public int Id { get; set; }
    public int AnalysisResultId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string? ExtractedValue { get; set; }

    /// <summary>User-supplied correction, populated in Session 7.</summary>
    public string? ManualOverride { get; set; }

    public double Confidence { get; set; }

    // Navigation property
    public AnalysisResult AnalysisResult { get; set; } = null!;
}
