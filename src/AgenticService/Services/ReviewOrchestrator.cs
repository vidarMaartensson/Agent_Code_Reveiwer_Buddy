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
        Exception? caughtException = null;
        bool isCancelled = false;
        FetchedCodeDetails? fetchedCodeDetails = null;

        _logger.LogInformation("Starting review for: {RepoUrl}", repoUrl);

        // Step 1: Fetching
        _logger.LogDebug("Step 1: Fetching repository...");
        metadata.Status = "Fetching Repository...";
        yield return new ReviewResponseChunk { Metadata = metadata };
        try
        {
            fetchedCodeDetails = await _fetcher.ExecuteAsync(repoUrl);
        }
        catch (OperationCanceledException) { isCancelled = true; }
        catch (Exception ex) { caughtException = ex; }

        if (isCancelled || caughtException != null) goto HandleError;

        if (string.IsNullOrWhiteSpace(fetchedCodeDetails?.CodeContent))
        {
            _logger.LogWarning("Fetch failed or repository was empty for {RepoUrl}", repoUrl);
            metadata.Status = "Error";
            metadata.ErrorMessage = "Unable to retrieve code from the provided repository. Please check the URL and visibility.";
            yield return new ReviewResponseChunk { Metadata = metadata };
            yield break;
        }

        metadata.ScannedFiles = fetchedCodeDetails.ScannedFiles;

        // Step 2: Analyzing
        _logger.LogDebug("Step 2: Analyzing code...");
        metadata.Status = "Analyzing Code...";
        yield return new ReviewResponseChunk { Metadata = metadata };
        yield return new ReviewResponseChunk { ReportChunk = _generator.GetReportHeader(metadata.GeneratedOn) + "\n\n" };
        yield return new ReviewResponseChunk { ReportChunk = _generator.GetAnalysisHeader(), Section = "Analysis" };

        var analysisBuffer = new System.Text.StringBuilder();
        var analysisEnumerator = _reviewer.ReviewCodeAsync(fetchedCodeDetails.CodeContent, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                string? chunk = null;
                try
                {
                    if (!await analysisEnumerator.MoveNextAsync()) break;
                    chunk = analysisEnumerator.Current;
                }
                catch (OperationCanceledException) { isCancelled = true; break; }
                catch (Exception ex) { caughtException = ex; break; }

                analysisBuffer.Append(chunk);
                yield return new ReviewResponseChunk { ReportChunk = chunk, Section = "Analysis" };
            }
        }
        finally { await analysisEnumerator.DisposeAsync(); }

        if (isCancelled || caughtException != null) goto HandleError;

        var analysis = analysisBuffer.ToString();
        if (string.IsNullOrWhiteSpace(analysis))
        {
            yield return new ReviewResponseChunk { ReportChunk = "The review agent failed to produce an analysis.", Section = "Analysis" };
        }
        yield return new ReviewResponseChunk { ReportChunk = "\n" };

        // Step 3: Suggestions
        _logger.LogDebug("Step 3: Generating suggestions...");
        metadata.Status = "Generating Suggestions...";
        yield return new ReviewResponseChunk { Metadata = metadata };
        yield return new ReviewResponseChunk { ReportChunk = "\n" + _generator.GetSuggestionsHeader(), Section = "Suggestions" };

        var suggestionsBuffer = new System.Text.StringBuilder();
        var suggestionsEnumerator = _suggester.SuggestImprovementsAsync(fetchedCodeDetails.CodeContent, analysis, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                string? chunk = null;
                try
                {
                    if (!await suggestionsEnumerator.MoveNextAsync()) break;
                    chunk = suggestionsEnumerator.Current;
                }
                catch (OperationCanceledException) { isCancelled = true; break; }
                catch (Exception ex) { caughtException = ex; break; }

                suggestionsBuffer.Append(chunk);
                yield return new ReviewResponseChunk { ReportChunk = chunk, Section = "Suggestions" };
            }
        }
        finally { await suggestionsEnumerator.DisposeAsync(); }

        if (isCancelled || caughtException != null) goto HandleError;

        var suggestions = suggestionsBuffer.ToString();
        if (string.IsNullOrWhiteSpace(suggestions))
        {
            yield return new ReviewResponseChunk { ReportChunk = "The agent was unable to generate specific code suggestions for the identified issues.", Section = "Suggestions" };
        }
        yield return new ReviewResponseChunk { ReportChunk = "\n" };

        yield return new ReviewResponseChunk { ReportChunk = _generator.GetReportFooter() };
        _logger.LogInformation("Review workflow completed.");
        metadata.Status = "Success";
        yield return new ReviewResponseChunk { Metadata = metadata };
        yield break;

    HandleError:
        if (isCancelled)
        {
            metadata.Status = "Timeout";
            metadata.ErrorMessage = "The review process took too long and was cancelled. This often happens with very large repositories or local LLM resource constraints.";
            _logger.LogError("The review request for {RepoUrl} timed out.", repoUrl);
        }
        else if (caughtException != null)
        {
            metadata.Status = "Error";
            metadata.ErrorMessage = $"An error occurred while processing the review: {caughtException.Message}";
            _logger.LogError(caughtException, "Error during review orchestration for {RepoUrl}", repoUrl);
        }
        yield return new ReviewResponseChunk { Metadata = metadata };
    }
}