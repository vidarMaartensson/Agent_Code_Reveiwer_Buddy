using AgenticService.Infrastructure;

namespace AgenticService.Agents;

/// <summary>
/// Takes code review findings and generates specific code improvement suggestions.
/// </summary>
public class CodeSuggesterAgent
{
    private readonly ILocalLlmClient _llmClient;

    public CodeSuggesterAgent(ILocalLlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    public async Task<string> SuggestImprovementsAsync(string sourceCode, string reviewFindings)
    {
        var prompt = $"""
            Based on the following code review findings, provide specific code snippets and refactoring suggestions.
            Ensure the suggestions follow best practices.

            REVIEW FINDINGS:
            {reviewFindings}

            ORIGINAL CODE:
            {sourceCode}
            """;

        return await _llmClient.GetCompletionAsync(prompt);
    }
}