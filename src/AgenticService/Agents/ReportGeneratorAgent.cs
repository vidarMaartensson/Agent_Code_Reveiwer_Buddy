namespace AgenticService.Agents;

/// <summary>
/// Formats the raw analysis results into a user-friendly report.
/// </summary>
public class ReportGeneratorAgent
{
    public string GenerateMarkdownReport(string analysisResults, string codeSuggestions)
    {
        return $"""
            # Code Review Report
            **Generated on:** {DateTime.UtcNow:f}
            
            ## 🔍 Analysis Summary
            {analysisResults}

            ## 💡 Suggested Improvements
            {codeSuggestions}
            
            ---
            """;
    }
}