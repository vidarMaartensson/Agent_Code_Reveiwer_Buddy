using AgenticService.Agents;
using AgenticService.Infrastructure;
using AgenticService.Services;
using AgenticService.Models;
using AgenticService.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors();

//Tools and agents
builder.Services.AddTransient<GitHubTools>();
builder.Services.AddSingleton<GuidelineTool>();
if (builder.Configuration["LlmSettings:Provider"]?.Equals("Ollama", StringComparison.OrdinalIgnoreCase) == true)
    builder.Services.AddHttpClient<ILocalLlmClient, OllamaLlmClient>();
else
    builder.Services.AddHttpClient<ILocalLlmClient, GroqLlmClient>();
builder.Services.AddTransient<RepoFetcherAgent>();
builder.Services.AddTransient<CodeSuggesterAgent>();
builder.Services.AddTransient<CodeReviewerAgent>();
builder.Services.AddTransient<ReportGeneratorAgent>();
builder.Services.AddTransient<ReviewOrchestrator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Comma-separated list, e.g. Cors__AllowedOrigins=https://your-app.vercel.app. Unset = allow any (local dev).
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

app.UseCors(policy =>
{
    if (allowedOrigins is { Length: > 0 }) policy.WithOrigins(allowedOrigins);
    else policy.AllowAnyOrigin();
    policy.AllowAnyMethod().AllowAnyHeader();
});

app.MapGet("/", () => Results.Text("AgentCodeReviewerBuddy API"));

app.MapGet("/health", () => Results.Json(new { status = "Healthy", utc = DateTime.UtcNow }));

app.MapPost("/review", (string repoUrl, ReviewOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrEmpty(repoUrl)) return Results.BadRequest("URL is required.");
    var reviewStream = orchestrator.RunFullReviewAsync(repoUrl, cancellationToken);
    return Results.Ok(reviewStream);
});

app.Run();
