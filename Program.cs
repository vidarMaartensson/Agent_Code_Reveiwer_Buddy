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

app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.MapGet("/", () => Results.Text("AgentCodeReviewerBuddy API"));

app.MapGet("/health", () => Results.Json(new { status = "Healthy", utc = DateTime.UtcNow }));

app.MapPost("/review", (string repoUrl, ReviewOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrEmpty(repoUrl)) return Results.BadRequest("URL is required.");
    var reviewStream = orchestrator.RunFullReviewAsync(repoUrl, cancellationToken);
    return Results.Ok(reviewStream);
});

app.Run();
