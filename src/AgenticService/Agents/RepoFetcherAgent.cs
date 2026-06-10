using AgenticService.Infrastructure;
using AgenticService.Tools;

namespace AgenticService.Agents;

/// <summary>
/// This agent is responsible for acquiring the source code from a repository.
/// It acts as the first step in the review pipeline.
/// </summary>
public class RepoFetcherAgent
{
    private readonly GitHubTools _githubTools;
    private readonly ILocalLlmClient _llmClient;

    // Conservative character limit for local LLMs (roughly ~15k-20k tokens)
    private const int MaxCharacterLimit = 60000;

    public RepoFetcherAgent(GitHubTools githubTools, ILocalLlmClient llmClient)
    {
        _githubTools = githubTools;
        _llmClient = llmClient;
    }

    public async Task<FetchedCodeDetails> ExecuteAsync(string repoUrl)
    {
        string tempPath = "";
        try
        {
            tempPath = await Task.Run(() => _githubTools.CloneRepo(repoUrl));
            
            var allFiles = _githubTools.GetFileList(tempPath);
            
            var importantFiles = await _llmClient.FilterRelevantFilesAsync(allFiles);
            
            var codeContent = _githubTools.ReadFiles(tempPath, importantFiles);
            
            var fetchedDetails = new FetchedCodeDetails
            {
                ScannedFiles = importantFiles
            };

            if (codeContent.Length > MaxCharacterLimit)
            {
                fetchedDetails.CodeContent = codeContent[..MaxCharacterLimit] + "\n\n[Content Truncated due to context window limits...]";
            }
            else fetchedDetails.CodeContent = codeContent;
            return fetchedDetails;
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempPath))
                _githubTools.Cleanup(tempPath);
        }
    }
}