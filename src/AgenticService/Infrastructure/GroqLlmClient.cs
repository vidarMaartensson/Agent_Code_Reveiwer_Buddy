using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticService.Infrastructure;

public class GroqLlmClient : ILocalLlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GroqLlmClient(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _model = config["Groq:ModelName"] ?? "llama-3.3-70b-versatile";
        _baseUrl = config["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1";

        var apiKey = config["Groq:ApiKey"] ?? config["GROQ_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Groq API key missing. Set it with: dotnet user-secrets set \"Groq:ApiKey\" \"<your key>\" (or env var GROQ_API_KEY).");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
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

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await _httpClient.PostAsJsonAsync(CompletionsUrl, BuildRequest(prompt, stream: false), cts.Token);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, cts.Token);
            var content = result?.Choices.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
                return allFiles.Take(10).ToList();

            var filtered = content.Split(',')
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
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, CompletionsUrl);
        requestMessage.Content = JsonContent.Create(BuildRequest(prompt, stream: true));

        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);

        // Groq streams Server-Sent Events: "data: {json}" lines, terminated by "data: [DONE]"
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) // end of stream
                break;

            if (!line.StartsWith("data:")) continue;

            var data = line["data:".Length..].Trim();
            if (data == "[DONE]") break;

            ChatCompletionResponse? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize<ChatCompletionResponse>(data, JsonOptions);
            }
            catch (JsonException) { /* Log or handle malformed JSON if necessary */ }

            var content = chunk?.Choices.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(content))
            {
                yield return content;
            }
        }
    }

    private string CompletionsUrl => $"{_baseUrl.TrimEnd('/')}/chat/completions";

    private object BuildRequest(string prompt, bool stream) => new
    {
        model = _model,
        messages = new[] { new { role = "user", content = prompt } },
        stream
    };

    private record ChatCompletionResponse([property: JsonPropertyName("choices")] List<Choice> Choices);
    private record Choice(
        [property: JsonPropertyName("message")] ChatMessage? Message,
        [property: JsonPropertyName("delta")] ChatMessage? Delta);
    private record ChatMessage([property: JsonPropertyName("content")] string? Content);
}
