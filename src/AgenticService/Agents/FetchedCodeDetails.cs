namespace AgenticService.Agents;

public class FetchedCodeDetails
{
    public string CodeContent { get; set; } = string.Empty;
    public List<string> ScannedFiles { get; set; } = new List<string>();
}