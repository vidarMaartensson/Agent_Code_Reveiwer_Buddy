namespace AgenticService.Agents;

public class FetchedCodeDetails
{
    public string CodeContent { get; set; } = string.Empty;
    public List<string> ScannedFiles { get; set; } = new List<string>();
    public List<(string Path, string Content)> Files { get; set; } = new();

    /// <summary>
    /// Returns only the files mentioned in the given text (e.g. review findings),
    /// falling back to all code when none match.
    /// </summary>
    public string GetCodeForFilesMentionedIn(string text)
    {
        var mentioned = Files
            .Where(f => text.Contains(f.Path, StringComparison.OrdinalIgnoreCase) ||
                        text.Contains(Path.GetFileName(f.Path), StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Content)
            .ToList();

        var subset = string.Concat(mentioned);
        // CodeContent is already capped to the token budget, so never send more than it
        return mentioned.Count > 0 && subset.Length < CodeContent.Length ? subset : CodeContent;
    }
}
