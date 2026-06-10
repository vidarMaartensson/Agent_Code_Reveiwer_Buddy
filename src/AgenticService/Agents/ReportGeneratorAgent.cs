namespace AgenticService.Agents;

/// <summary>
/// Formats the raw analysis results into a user-friendly report.
/// </summary>
public class ReportGeneratorAgent
{
    public string GetReportHeader(DateTime generatedOn)
    {
        return $"""
            # Code Review Report
            **Generated on:** {generatedOn:f}
            """;
    }

    public string GetAnalysisHeader()
    {
        return "## 🔍 Analysis Summary\n";
    }

    public string GetSuggestionsHeader()
    {
        return "## 💡 Suggested Improvements\n";
    }

    public string GetReportFooter()
    {
        return "\n---\n";
    }
}