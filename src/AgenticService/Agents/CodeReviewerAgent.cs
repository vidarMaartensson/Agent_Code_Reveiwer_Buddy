using AgenticService.Infrastructure;
using System.Runtime.CompilerServices;
using AgenticService.Tools;

namespace AgenticService.Agents;

/// <summary>
/// Analyzes the source code and identifies potential improvements or bugs.
/// </summary>
public class CodeReviewerAgent(ILocalLlmClient llmClient, GuidelineTool guidelineTool)
{
    private readonly ILocalLlmClient _llmClient = llmClient;
    private readonly GuidelineTool _guidelineTool = guidelineTool;

    public async IAsyncEnumerable<string> ReviewCodeAsync(string sourceCode, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            yield return "No code provided for review.";
            yield break;
        }

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
        
        await foreach (var chunk in _llmClient.StreamCompletionAsync(prompt, cancellationToken))
        {
            yield return chunk;
        }
    }
}