namespace OcrApi.Models;

public class DocumentPage
{
    public int Id { get; set; }
    public int AnalysisResultId { get; set; }
    public int PageNumber { get; set; }
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
    public AnalysisResult AnalysisResult { get; set; } = null!;
}
