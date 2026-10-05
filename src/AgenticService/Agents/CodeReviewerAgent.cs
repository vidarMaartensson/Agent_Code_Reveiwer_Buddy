using AgenticService.Infrastructure;
using System.Runtime.CompilerServices;
using AgenticService.Tools;

namespace AgenticService.Agents;

/// <summary>
/// Analyzes the source code and identifies potential improvements or bugs.
/// </summary>
public class CodeReviewerAgent(ILocalLlmClient llmClient, GuidelineTool guidelineTool)
{
    /// <summary>
    /// Separates the analysis from the suggestions in the combined review output.
    /// </summary>
    public const string SuggestionsMarker = "===SUGGESTIONS===";

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
            Always name the file each issue is in.

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

    /// <summary>
    /// Reviews the code and suggests improvements in a single LLM call, so the code is only sent once.
    /// The suggestions follow <see cref="SuggestionsMarker"/> in the output.
    /// </summary>
    public async IAsyncEnumerable<string> ReviewAndSuggestAsync(string sourceCode, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            yield return "No code provided for review.";
            yield break;
        }

        var guidelines = _guidelineTool.GetReviewGuidelines();

        var prompt = $"""
            You are a Senior Software Engineer. Review the following code specifically against the provided GUIDELINES.

            Answer in two parts:
            1. List the bugs, performance issues, and readability problems clearly, naming the file each issue is in. No fixed code in this part.
            2. Then write a line containing only {SuggestionsMarker}
               followed by specific code snippets and refactoring suggestions for those issues, following best practices.

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
