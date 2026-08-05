using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using backend.Data;
using backend.Options;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Port 8080 configuration as specified in Issue K1.1 & CONTRACT.md
builder.WebHost.UseUrls("http://0.0.0.0:8080");

// Configure PostgreSQL DbContext with Npgsql
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Port=5432;Database=datactive_gitops;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure GitHub Options & Typed HttpClient Service (Solution 1 for K1.5)
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName));

builder.Services.AddHttpClient<IGitHubService, GitHubService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
    
    // Read GITHUB_TOKEN environment variable if appsettings token is empty
    var token = !string.IsNullOrEmpty(options.Token) 
        ? options.Token 
        : Environment.GetEnvironmentVariable("GITHUB_TOKEN");

    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Add("User-Agent", "DatactiveGitOps-Backend");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

    if (!string.IsNullOrEmpty(token))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
});

// Solution 2: ASP.NET Core Built-in HealthChecks Service
builder.Services.AddHealthChecks();

var app = builder.Build();

// Map /health endpoint with custom JSON output format
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            service = "datactive-backend"
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

// [FAZ-1] K1.4 — Mock Branches Endpoint
app.MapGet("/api/branches", (string? repo) =>
{
    var repoType = repo?.ToLowerInvariant();

    if (repoType == "server")
    {
        var serverBranches = new List<BranchDto>
        {
            new("main", true),
            new("feat/K1-5-api", false),
            new("fix/db-connection", false)
        };
        return Results.Ok(serverBranches);
    }

    // Default or 'web' repo branches
    var webBranches = new List<BranchDto>
    {
        new("main", true),
        new("feat/K1-5-auth", false),
        new("feat/ui-redesign", false)
    };
    return Results.Ok(webBranches);
});

app.Run();

// DTO for Branch Response
public record BranchDto(string Name, bool IsDefault);
