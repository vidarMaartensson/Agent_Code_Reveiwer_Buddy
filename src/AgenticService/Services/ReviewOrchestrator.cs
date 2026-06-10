using AgenticService.Agents;

namespace AgenticService.Services;

/// <summary>
/// The Orchestrator manages the high-level workflow of the code review process.
/// It coordinates between fetching, reviewing, and reporting.
/// </summary>
public class ReviewOrchestrator
{
    private readonly RepoFetcherAgent _fetcher;
    private readonly CodeReviewerAgent _reviewer;
    private readonly CodeSuggesterAgent _suggester;
    private readonly ReportGeneratorAgent _generator;
    private readonly ILogger<ReviewOrchestrator> _logger;

    public ReviewOrchestrator(
        RepoFetcherAgent fetcher, 
        CodeReviewerAgent reviewer,
        CodeSuggesterAgent suggester,
        ReportGeneratorAgent generator,
        ILogger<ReviewOrchestrator> logger)
    {
        _fetcher = fetcher;
        _reviewer = reviewer;
        _suggester = suggester;
        _generator = generator;
        _logger = logger;
    }

    public async Task<string> RunFullReviewAsync(string repoUrl)
    {
        try
        {
            _logger.LogInformation("Starting review for: {RepoUrl}", repoUrl);

            _logger.LogDebug("Step 1: Fetching repository...");
            var rawCode = await _fetcher.ExecuteAsync(repoUrl);
            if (string.IsNullOrWhiteSpace(rawCode))
            {
                _logger.LogWarning("Fetch failed or repository was empty for {RepoUrl}", repoUrl);
                return "# Error\nUnable to retrieve code from the provided repository. Please check the URL and visibility.";
            }

            _logger.LogDebug("Step 2: Analyzing code...");
            var analysis = await _reviewer.ReviewCodeAsync(rawCode);
            if (string.IsNullOrWhiteSpace(analysis))
            {
                 throw new InvalidOperationException("The review agent failed to produce an analysis.");
            }

            _logger.LogDebug("Step 3: Generating suggestions...");
            var suggestions = await _suggester.SuggestImprovementsAsync(rawCode, analysis);

            _logger.LogDebug("Step 4: Compiling report...");
            var report = _generator.GenerateMarkdownReport(analysis, suggestions);

            _logger.LogInformation("Review workflow completed.");
            return report;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("The review request for {RepoUrl} timed out.", repoUrl);
            return "# Timeout Error\nThe review process took too long and was cancelled. This often happens with very large repositories or local LLM resource constraints.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during review orchestration for {RepoUrl}", repoUrl);
            return $"# Error\nAn error occurred while processing the review: {ex.Message}";
        }
    }
}