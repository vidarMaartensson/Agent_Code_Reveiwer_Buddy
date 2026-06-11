using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticService.Infrastructure;

public interface ILocalLlmClient
{
    Task<List<string>> FilterRelevantFilesAsync(List<string> allFiles);
    Task<string> GetCompletionAsync(string prompt);
    IAsyncEnumerable<string> StreamCompletionAsync(string prompt, CancellationToken cancellationToken = default);
}

public class OllamaLlmClient : ILocalLlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _baseUrl;

    public OllamaLlmClient(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _model = config["LlmSettings:ModelName"] ?? "llama3";
        _baseUrl = config["LlmSettings:BaseUrl"] ?? "http://localhost:11434";
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
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var url = $"{_baseUrl.TrimEnd('/')}/api/generate";
            
            var response = await _httpClient.PostAsJsonAsync(url, request, cts.Token);
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
        // Use the streaming method and aggregate results for non-streaming completion
        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in StreamCompletionAsync(prompt))
        {
            sb.Append(chunk);
        }
        return sb.ToString();
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new { model = _model, prompt = prompt, stream = true };
        var url = $"{_baseUrl.TrimEnd('/')}/api/generate";
        
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Content = JsonContent.Create(request);
        
        var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) // end of stream
                break;

            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaResponse? ollamaResponse = null;
            try
            {
                ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(line, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
            }
            catch (JsonException) { /* Log or handle malformed JSON if necessary */ }

            if (ollamaResponse != null && !string.IsNullOrEmpty(ollamaResponse.Response))
            {
                yield return ollamaResponse.Response;
            }
        }
    }

    private record OllamaResponse([property: JsonPropertyName("response")] string Response);
}