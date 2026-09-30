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

    // ~4 characters per token. Keep this well below the provider's tokens-per-minute limit,
    // since the reviewer and suggester each send the code once.
    private readonly int _maxCharacterLimit;
    private readonly int _maxFiles;

    public RepoFetcherAgent(GitHubTools githubTools, ILocalLlmClient llmClient, IConfiguration config)
    {
        _githubTools = githubTools;
        _llmClient = llmClient;
        _maxCharacterLimit = config.GetValue("LlmSettings:MaxCodeCharacters", 60000);
        _maxFiles = config.GetValue("LlmSettings:MaxFiles", 10);
    }

    public async Task<FetchedCodeDetails> ExecuteAsync(string repoUrl)
    {
        string tempPath = "";
        try
        {
            tempPath = await Task.Run(() => _githubTools.CloneRepo(repoUrl));
            
            var allFiles = _githubTools.GetFileList(tempPath);
            
            var importantFiles = (await _llmClient.FilterRelevantFilesAsync(allFiles))
                .Take(_maxFiles)
                .ToList();
            
            var codeContent = _githubTools.ReadFiles(tempPath, importantFiles);
            
            var fetchedDetails = new FetchedCodeDetails
            {
                ScannedFiles = importantFiles
            };

            if (codeContent.Length > _maxCharacterLimit)
            {
                fetchedDetails.CodeContent = codeContent[.._maxCharacterLimit] + "\n\n[Content Truncated due to context window limits...]";
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