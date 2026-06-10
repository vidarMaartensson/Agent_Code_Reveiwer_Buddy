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

    public RepoFetcherAgent(GitHubTools githubTools, ILocalLlmClient llmClient)
    {
        _githubTools = githubTools;
        _llmClient = llmClient;
    }

    public async Task<string> ExecuteAsync(string repoUrl)
    {
        string tempPath = "";
        try
        {
            tempPath = await Task.Run(() => _githubTools.CloneRepo(repoUrl));
            
            var allFiles = _githubTools.GetFileList(tempPath);
            
            var importantFiles = await _llmClient.FilterRelevantFilesAsync(allFiles);
            
            var codeContent = _githubTools.ReadFiles(tempPath, importantFiles);
            
            return codeContent;
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempPath))
                _githubTools.Cleanup(tempPath);
        }
    }
}