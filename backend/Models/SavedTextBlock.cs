namespace OcrApi.Models;

public class SavedTextBlock
{
    public int Id { get; set; }
    public int AnalysisResultId { get; set; }
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public int Page { get; set; }
    public int BboxX { get; set; }
    public int BboxY { get; set; }
    public int BboxWidth { get; set; }
    public int BboxHeight { get; set; }

    public AnalysisResult AnalysisResult { get; set; } = null!;
}
