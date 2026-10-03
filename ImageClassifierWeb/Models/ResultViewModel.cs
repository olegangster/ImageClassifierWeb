namespace ImageClassifierWeb.Models;

public class ResultViewModel
{
    public string? Error { get; set; }
    public string? Source { get; set; }
    public long ElapsedMs { get; set; }
    public long SizeBytes { get; set; }
    public List<KeyValuePair<string, float>> Top { get; set; } = new();
    public string? ImageDataUrl { get; set; }
}
