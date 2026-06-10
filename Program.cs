using AgenticService.Agents;
using AgenticService.Infrastructure;
using AgenticService.Services;
using AgenticService.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Register Tools and Agents
builder.Services.AddSingleton<GitHubTools>();
builder.Services.AddSingleton<GuidelineTool>();
builder.Services.AddHttpClient<ILocalLlmClient, OllamaLlmClient>();
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

app.MapGet("/", () => Results.Text("AgentCodeReviewerBuddy API"));

app.MapGet("/health", () => Results.Json(new { status = "Healthy", utc = DateTime.UtcNow }));

app.MapPost("/review", async (string repoUrl, ReviewOrchestrator orchestrator) =>
{
    if (string.IsNullOrEmpty(repoUrl)) return Results.BadRequest("URL is required.");
    var result = await orchestrator.RunFullReviewAsync(repoUrl);
    return Results.Ok(result);
});

app.Run();
