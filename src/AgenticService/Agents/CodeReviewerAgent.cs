using AgenticService.Infrastructure;
using AgenticService.Tools;

namespace AgenticService.Agents;

/// <summary>
/// Analyzes the source code and identifies potential improvements or bugs.
/// </summary>
public class CodeReviewerAgent(ILocalLlmClient llmClient, GuidelineTool guidelineTool)
{
    private readonly ILocalLlmClient _llmClient = llmClient;
    private readonly GuidelineTool _guidelineTool = guidelineTool;

    public async Task<string> ReviewCodeAsync(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            return "No code provided for review.";

        var guidelines = _guidelineTool.GetReviewGuidelines();

        var prompt = $"""
            You are a Senior Software Engineer. Review the following code specifically against the provided GUIDELINES.
            Identify bugs, performance issues, and readability problems.
            Do not provide the fixed code yet, just list the issues clearly.

            GUIDELINES:
            {guidelines}

            CODE:
            {sourceCode}
            """;

        return await _llmClient.GetCompletionAsync(prompt);
    }
}