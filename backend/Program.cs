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

// Register Tag Generator Service (Solution 1 for K1.11)
builder.Services.AddSingleton<ITagGeneratorService, TagGeneratorService>();

// Register GitOps Overlay Service (Solution 1 for K1.10)
builder.Services.AddScoped<IGitOpsOverlayService, GitOpsOverlayService>();

// Configure GitHub Options (K1.5)
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName));

// Configure GitHub Typed HttpClient Service (K1.5)
builder.Services.AddHttpClient<IGitHubService, GitHubService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
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

// Configure Build Provider with Feature Switch (K1.8)
var useMockBuildProvider = builder.Configuration.GetValue<bool>("UseMockBuildProvider", false);
if (useMockBuildProvider)
{
    builder.Services.AddScoped<IBuildProvider, MockBuildProvider>();
}
else
{
    builder.Services.AddHttpClient<IBuildProvider, GitHubBuildProvider>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
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
}

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

// [FAZ-4] K1.11 — Generate Tag Utility Endpoint
app.MapGet("/api/tags/generate", (string? branchWeb, string? branchServer, ITagGeneratorService tagGenerator) =>
{
    var generatedTag = tagGenerator.GenerateTag(branchWeb, branchServer);
    return Results.Ok(new { tag = generatedTag });
});

// [FAZ-4] K1.10 — Generate & Commit GitOps Overlay Endpoint
app.MapPost("/api/gitops/overlay", async (string targetNamespace, string tag, string schema, string ticket, IGitOpsOverlayService overlayService) =>
{
    var result = await overlayService.CreateAndCommitOverlayAsync(targetNamespace, tag, schema, ticket);
    if (!result.Success)
    {
        return Results.Problem(detail: result.ErrorMessage, statusCode: 500, title: "GitOps Overlay Üretim/Push Hatası");
    }
    return Results.Ok(result);
});

// [FAZ-3] K1.8 & K1.11 — Real/Mock Build Dispatch Endpoint with Auto-Tag Generation Support
app.MapPost("/api/builds", async (BuildRequestDto request, IBuildProvider buildProvider, ITagGeneratorService tagGenerator, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.BranchWeb) ||
        string.IsNullOrWhiteSpace(request.BranchServer) ||
        string.IsNullOrWhiteSpace(request.Schema))
    {
        return Results.BadRequest(new { error = "Alanlar (branch_web, branch_server, schema) zorunludur." });
    }

    var tag = string.IsNullOrWhiteSpace(request.Tag) || request.Tag.Equals("auto", StringComparison.OrdinalIgnoreCase)
        ? tagGenerator.GenerateTag(request.BranchWeb, request.BranchServer)
        : request.Tag;

    var finalRequest = request with { Tag = tag };

    try
    {
        var result = await buildProvider.DispatchBuildAsync(finalRequest);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning(ex, "Dispatch target workflow or repository not found.");
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError(ex, "GitHub Authorization error during build dispatch.");
        return Results.Problem(detail: ex.Message, statusCode: 502, title: "GitHub API Yetkilendirme Hatası");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error dispatching build for Tag: {Tag}", tag);
        return Results.Problem(detail: "Build tetikleme sırasında hata oluştu: " + ex.Message, statusCode: 502, title: "Build Tetikleme Hatası");
    }
});

// [FAZ-3] K1.9 — Live Build Status Endpoint with On-Demand Refresh & DB Sync
app.MapGet("/api/builds/{id}", async (string id, AppDbContext dbContext, IGitHubService gitHubService, ILogger<Program> logger) =>
{
    BuildHistory? buildRecord = null;

    if (int.TryParse(id, out var numericId))
    {
        buildRecord = await dbContext.BuildHistories.FindAsync(numericId);
    }

    if (buildRecord == null)
    {
        buildRecord = await dbContext.BuildHistories
            .FirstOrDefaultAsync(b => b.Tag == id || b.Namespace == id);
    }

    if (buildRecord == null)
    {
        return Results.NotFound(new { error = $"Build kaydı '{id}' bulunamadı." });
    }

    // On-Demand Refresh: If status is not final (queued or in_progress), query live status from GitHub API
    if (buildRecord.Status == "queued" || buildRecord.Status == "in_progress")
    {
        try
        {
            var liveStatus = await gitHubService.GetWorkflowRunStatusAsync(buildRecord.Tag);
            if (!string.IsNullOrEmpty(liveStatus) && liveStatus != buildRecord.Status)
            {
                logger.LogInformation("Updating build status for Tag '{Tag}' from '{OldStatus}' to '{NewStatus}' in DB.",
                    buildRecord.Tag, buildRecord.Status, liveStatus);

                buildRecord.Status = liveStatus;
                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not refresh live status from GitHub for Tag: {Tag}", buildRecord.Tag);
        }
    }

    var response = new BuildStatusDto(
        BuildId: buildRecord.Id.ToString(),
        Tag: buildRecord.Tag,
        Namespace: buildRecord.Namespace,
        Status: buildRecord.Status,
        CreatedAt: buildRecord.CreatedAt
    );

    return Results.Ok(response);
});

app.Run();

// DTO for Branch Response
public record BranchDto(string Name, bool IsDefault);
