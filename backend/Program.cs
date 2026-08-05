using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using backend.Data;
using backend.Models;
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

// Add MemoryCache for GitHub API Rate Limit protection (K1.6)
builder.Services.AddMemoryCache();

// Register Build Provider (Solution 1 for K1.7: MockBuildProvider)
builder.Services.AddScoped<IBuildProvider, MockBuildProvider>();

// Configure GitHub Options & Typed HttpClient Service (K1.5)
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

// [FAZ-2] K1.6 — Live GitHub Branches Endpoint with 60s MemoryCache
app.MapGet("/api/branches", async (string? repo, IGitHubService gitHubService, IMemoryCache cache, ILogger<Program> logger) =>
{
    var repoType = string.IsNullOrWhiteSpace(repo) ? "web" : repo.Trim().ToLowerInvariant();
    var cacheKey = $"github_branches_{repoType}";

    if (cache.TryGetValue(cacheKey, out IEnumerable<BranchDto>? cachedBranches) && cachedBranches != null)
    {
        logger.LogInformation("Returning branches for '{RepoType}' from MemoryCache.", repoType);
        return Results.Ok(cachedBranches);
    }

    try
    {
        var branches = await gitHubService.GetBranchesAsync(repoType);
        
        // Cache the result for 60 seconds to protect against GitHub Rate Limit
        cache.Set(cacheKey, branches, TimeSpan.FromSeconds(60));
        
        return Results.Ok(branches);
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning(ex, "Repository not found for type: {RepoType}", repoType);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError(ex, "GitHub Authorization error for type: {RepoType}", repoType);
        return Results.Problem(detail: ex.Message, statusCode: 502, title: "GitHub API Yetkilendirme Hatası");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error fetching live branches from GitHub for type: {RepoType}", repoType);
        return Results.Problem(detail: "GitHub API servisiyle iletişim kurulurken bir hata oluştu: " + ex.Message, statusCode: 502, title: "GitHub Servis Hatası");
    }
});

// [FAZ-2] K1.7 — Mock Build Dispatch Endpoint
app.MapPost("/api/builds", async (BuildRequestDto request, IBuildProvider buildProvider) =>
{
    if (string.IsNullOrWhiteSpace(request.BranchWeb) ||
        string.IsNullOrWhiteSpace(request.BranchServer) ||
        string.IsNullOrWhiteSpace(request.Schema) ||
        string.IsNullOrWhiteSpace(request.Tag))
    {
        return Results.BadRequest(new { error = "Tüm alanlar (branch_web, branch_server, schema, tag) zorunludur." });
    }

    var result = await buildProvider.DispatchBuildAsync(request);
    return Results.Ok(result);
});

app.Run();

// DTO for Branch Response
public record BranchDto(string Name, bool IsDefault);
