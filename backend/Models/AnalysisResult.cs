namespace OcrApi.Models;

public class AnalysisResult
{
    public int Id { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public string FileName { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public byte[]? ImageBytes { get; set; }
    public byte[]? SignatureImage { get; set; }
    public string? TableBlocksJson { get; set; }
    public string? DocumentType { get; set; }
    public List<SavedField> Fields { get; set; } = new();
    public List<SavedTextBlock> TextBlocks { get; set; } = new();
    public List<DocumentPage> Pages { get; set; } = new();
}

public class SavedField
{
    public int Id { get; set; }
    public int AnalysisResultId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string? ExtractedValue { get; set; }
    public string? ManualOverride { get; set; }
    public double Confidence { get; set; }

    public AnalysisResult AnalysisResult { get; set; } = null!;
}
