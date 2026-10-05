using AgenticService.Agents;
using AgenticService.Models;
using System.Runtime.CompilerServices;
using System.Text;

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
    private readonly bool _combinedMode;

    public ReviewOrchestrator(
        RepoFetcherAgent fetcher, 
        CodeReviewerAgent reviewer,
        CodeSuggesterAgent suggester,
        ReportGeneratorAgent generator,
        IConfiguration config,
        ILogger<ReviewOrchestrator> logger)
    {
        _fetcher = fetcher;
        _reviewer = reviewer;
        _suggester = suggester;
        _generator = generator;
        _logger = logger;
        // "Combined" (default): one LLM call sends the code once. "TwoStep": separate reviewer and suggester calls.
        _combinedMode = !string.Equals(config["LlmSettings:ReviewMode"], "TwoStep", StringComparison.OrdinalIgnoreCase);
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

        // Step 2 + 3: Analyzing and suggesting (one combined LLM call, or two steps)
        _logger.LogDebug("Step 2: Analyzing code...");
        metadata.Status = "Analyzing Code...";
        yield return new ReviewResponseChunk { Metadata = metadata };
        yield return new ReviewResponseChunk { ReportChunk = _generator.GetReportHeader(metadata.GeneratedOn) + "\n\n" };
        yield return new ReviewResponseChunk { ReportChunk = _generator.GetAnalysisHeader(), Section = "Analysis" };

        var hasAnalysis = false;
        var hasSuggestions = false;
        var inSuggestions = false;
        var sections = _combinedMode
            ? SplitOnMarker(_reviewer.ReviewAndSuggestAsync(fetchedCodeDetails.CodeContent, cancellationToken), cancellationToken)
            : StreamTwoStepAsync(fetchedCodeDetails, cancellationToken);
        var sectionsEnumerator = sections.GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                var part = (Text: "", IsSuggestions: false);
                try
                {
                    if (!await sectionsEnumerator.MoveNextAsync()) break;
                    part = sectionsEnumerator.Current;
                }
                catch (OperationCanceledException) { isCancelled = true; break; }
                catch (Exception ex) { caughtException = ex; break; }

                if (part.IsSuggestions && !inSuggestions)
                {
                    inSuggestions = true;
                    foreach (var chunk in StartSuggestionsSection(metadata, hasAnalysis)) yield return chunk;
                }
                if (part.Text.Length == 0) continue;

                if (inSuggestions) hasSuggestions |= !string.IsNullOrWhiteSpace(part.Text);
                else hasAnalysis |= !string.IsNullOrWhiteSpace(part.Text);
                yield return new ReviewResponseChunk { ReportChunk = part.Text, Section = inSuggestions ? "Suggestions" : "Analysis" };
            }
        }
        finally { await sectionsEnumerator.DisposeAsync(); }

        if (isCancelled || caughtException != null) goto HandleError;

        if (!inSuggestions)
        {
            foreach (var chunk in StartSuggestionsSection(metadata, hasAnalysis)) yield return chunk;
        }
        if (!hasSuggestions)
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

    private IEnumerable<ReviewResponseChunk> StartSuggestionsSection(ReviewResponseMetadata metadata, bool hasAnalysis)
    {
        if (!hasAnalysis)
        {
            yield return new ReviewResponseChunk { ReportChunk = "The review agent failed to produce an analysis.", Section = "Analysis" };
        }
        yield return new ReviewResponseChunk { ReportChunk = "\n" };

        _logger.LogDebug("Step 3: Generating suggestions...");
        metadata.Status = "Generating Suggestions...";
        yield return new ReviewResponseChunk { Metadata = metadata };
        yield return new ReviewResponseChunk { ReportChunk = "\n" + _generator.GetSuggestionsHeader(), Section = "Suggestions" };
    }

    /// <summary>
    /// Reviewer and suggester as separate LLM calls. The suggester only gets the files the analysis mentions.
    /// An empty suggestions part marks the switch to the suggestions section.
    /// </summary>
    private async IAsyncEnumerable<(string Text, bool IsSuggestions)> StreamTwoStepAsync(FetchedCodeDetails code, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var analysis = new StringBuilder();
        await foreach (var chunk in _reviewer.ReviewCodeAsync(code.CodeContent, cancellationToken))
        {
            analysis.Append(chunk);
            yield return (chunk, false);
        }

        yield return ("", true);

        var findings = analysis.ToString();
        var relevantCode = code.GetCodeForFilesMentionedIn(findings);
        await foreach (var chunk in _suggester.SuggestImprovementsAsync(relevantCode, findings, cancellationToken))
        {
            yield return (chunk, true);
        }
    }

    /// <summary>
    /// Splits the combined review stream at <see cref="CodeReviewerAgent.SuggestionsMarker"/>.
    /// An empty suggestions part marks the switch to the suggestions section.
    /// </summary>
    private static async IAsyncEnumerable<(string Text, bool IsSuggestions)> SplitOnMarker(IAsyncEnumerable<string> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        const string marker = CodeReviewerAgent.SuggestionsMarker;
        var pending = new StringBuilder();
        var found = false;

        await foreach (var chunk in source.WithCancellation(cancellationToken))
        {
            if (found)
            {
                yield return (chunk, true);
                continue;
            }

            pending.Append(chunk);
            var text = pending.ToString();
            var index = text.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                found = true;
                pending.Clear();
                // Models sometimes wrap the marker in bold or a heading, so drop that too
                yield return (text[..index].TrimEnd('*', '#', ' '), false);
                yield return ("", true);
                yield return (text[(index + marker.Length)..].TrimStart('*', ' ', '\r', '\n'), true);
                continue;
            }

            // Hold back a tail that could be the start of a marker split across chunks
            var safeLength = text.Length - (marker.Length - 1);
            if (safeLength > 0)
            {
                pending.Remove(0, safeLength);
                yield return (text[..safeLength], false);
            }
        }

        if (!found && pending.Length > 0)
            yield return (pending.ToString(), false);
    }
}
