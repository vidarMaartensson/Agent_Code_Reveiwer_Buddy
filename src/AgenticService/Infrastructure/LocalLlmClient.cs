using System.Net.Http.Json;

namespace AgenticService.Infrastructure;

public interface ILocalLlmClient
{
    Task<List<string>> FilterRelevantFilesAsync(List<string> allFiles);
    Task<string> GetCompletionAsync(string prompt);
}

public class OllamaLlmClient : ILocalLlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaLlmClient(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _model = config["LlmSettings:ModelName"] ?? "llama3";
    }

    public async Task<List<string>> FilterRelevantFilesAsync(List<string> allFiles)
    {
        try
        {
            var prompt = $"""
                Analyze the following list of file paths from a software project. 
                Identify the files most likely to contain core business logic, API definitions, or complex algorithms.
                Exclude boilerplate, simple configuration, and trivial assets.
                Return ONLY a comma-separated list of the relevant file paths.

                FILES:
                {string.Join("\n", allFiles)}
                """;

            var request = new { model = _model, prompt = prompt, stream = false };
            
            // Use a specific cancellation token for LLM calls to prevent infinite hangs
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", request, cts.Token);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: cts.Token);
            
            if (result == null || string.IsNullOrWhiteSpace(result.Response))
                return allFiles.Take(10).ToList();

            var filtered = result.Response.Split(',')
                            .Select(s => s.Trim())
                            .Where(allFiles.Contains)
                            .ToList();

            return filtered.Any() ? filtered : allFiles.Take(10).ToList();
        }
        catch (Exception)
        {
            // If LLM fails to filter, fallback to first 10 files to keep the process moving
            return allFiles.Take(10).ToList();
        }
    }

    public async Task<string> GetCompletionAsync(string prompt)
    {
        var request = new { model = _model, prompt = prompt, stream = false };
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", request, cts.Token);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: cts.Token);
        return result?.Response ?? string.Empty;
    }

    private record OllamaResponse(string Response);
}