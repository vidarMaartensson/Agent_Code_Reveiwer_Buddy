using AgenticService.Agents;
using AgenticService.Models;
using System.Runtime.CompilerServices;

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

    public async IAsyncEnumerable<ReviewResponseChunk> RunFullReviewAsync(string repoUrl, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var metadata = new ReviewResponseMetadata { GeneratedOn = DateTime.UtcNow };

        try
        {
            _logger.LogInformation("Starting review for: {RepoUrl}", repoUrl);

            _logger.LogDebug("Step 1: Fetching repository...");
            var fetchedCodeDetails = await _fetcher.ExecuteAsync(repoUrl);
            if (string.IsNullOrWhiteSpace(fetchedCodeDetails.CodeContent))
            {
                _logger.LogWarning("Fetch failed or repository was empty for {RepoUrl}", repoUrl);
                metadata.Status = "Error";
                metadata.ErrorMessage = "Unable to retrieve code from the provided repository. Please check the URL and visibility.";
                yield return new ReviewResponseChunk { Metadata = metadata };
                yield break;
            }
            metadata.ScannedFiles = fetchedCodeDetails.ScannedFiles;
            yield return new ReviewResponseChunk { Metadata = metadata };

            _logger.LogDebug("Step 2: Analyzing code...");
            yield return new ReviewResponseChunk { ReportChunk = _generator.GetReportHeader(metadata.GeneratedOn) };
            yield return new ReviewResponseChunk { ReportChunk = _generator.GetAnalysisHeader(), Section = "Analysis" };
            
            var analysisChunks = _reviewer.ReviewCodeAsync(fetchedCodeDetails.CodeContent, cancellationToken);
            var analysisBuffer = new System.Text.StringBuilder();
            await foreach (var chunk in analysisChunks.WithCancellation(cancellationToken))
            {
                analysisBuffer.Append(chunk);
                yield return new ReviewResponseChunk { ReportChunk = chunk, Section = "Analysis" };
            }
            var analysis = analysisBuffer.ToString();

            if (string.IsNullOrWhiteSpace(analysis))
            {
                yield return new ReviewResponseChunk { ReportChunk = "The review agent failed to produce an analysis.", Section = "Analysis" };
            }
            yield return new ReviewResponseChunk { ReportChunk = "\n" }; // Add a newline for separation

            _logger.LogDebug("Step 3: Generating suggestions...");
            yield return new ReviewResponseChunk { ReportChunk = _generator.GetSuggestionsHeader(), Section = "Suggestions" };

            var suggestionsChunks = _suggester.SuggestImprovementsAsync(fetchedCodeDetails.CodeContent, analysis, cancellationToken);
            var suggestionsBuffer = new System.Text.StringBuilder();
            await foreach (var chunk in suggestionsChunks.WithCancellation(cancellationToken))
            {
                suggestionsBuffer.Append(chunk);
                yield return new ReviewResponseChunk { ReportChunk = chunk, Section = "Suggestions" };
            }
            var suggestions = suggestionsBuffer.ToString();

            if (string.IsNullOrWhiteSpace(suggestions))
            {
                yield return new ReviewResponseChunk { ReportChunk = "The agent was unable to generate specific code suggestions for the identified issues.", Section = "Suggestions" };
            }
            yield return new ReviewResponseChunk { ReportChunk = "\n" }; // Add a newline for separation

            yield return new ReviewResponseChunk { ReportChunk = _generator.GetReportFooter() };

            _logger.LogInformation("Review workflow completed.");
            // Final metadata update (status is already success unless an error occurred)
            metadata.Status = "Success";
            yield return new ReviewResponseChunk { Metadata = metadata };
        }
        catch (OperationCanceledException)
        {
            metadata.Status = "Timeout";
            metadata.ErrorMessage = "The review process took too long and was cancelled. This often happens with very large repositories or local LLM resource constraints.";
            _logger.LogError("The review request for {RepoUrl} timed out.", repoUrl);
            yield return new ReviewResponseChunk { Metadata = metadata };
        }
        catch (Exception ex)
        {
            metadata.Status = "Error";
            metadata.ErrorMessage = $"An error occurred while processing the review: {ex.Message}";
            _logger.LogError(ex, "Error during review orchestration for {RepoUrl}", repoUrl);
            yield return new ReviewResponseChunk { Metadata = metadata };
        }
    }
}