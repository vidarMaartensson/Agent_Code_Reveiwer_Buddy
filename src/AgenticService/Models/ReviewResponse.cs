namespace AgenticService.Models;

public class ReviewResponseMetadata
{
    public string Status { get; set; } = "Success";
    public List<string> ScannedFiles { get; set; } = new List<string>();
    public string? ErrorMessage { get; set; }
    public DateTime GeneratedOn { get; set; } = DateTime.UtcNow;
}

public class ReviewResponseChunk
{
    public ReviewResponseMetadata? Metadata { get; set; }
    public string? ReportChunk { get; set; }
    public string? Section { get; set; } // e.g., "Analysis", "Suggestions"
}