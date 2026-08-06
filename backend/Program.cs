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

// Register ArgoCD Service (Solution 1 for K1.16)
builder.Services.AddHttpClient<IArgoCdService, ArgoCdService>((sp, client) =>
{
    client.BaseAddress = new Uri("https://argocd.datactive.net/");
    client.DefaultRequestHeaders.Add("User-Agent", "DatactiveGitOps-Backend");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

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

// Configure CORS Policy for Frontend (K1.12)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Solution 2: ASP.NET Core Built-in HealthChecks Service
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("AllowAll");

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

// [FAZ-5] K1.14 — Available DB Schemas Endpoint
app.MapGet("/api/schemas", () =>
{
    var schemas = new[]
    {
        new { id = "schema_dev", name = "schema_dev (Geliştirme / Test)" },
        new { id = "schema_k1_5", name = "schema_k1_5 (K1-5 Bilet Şeması)" },
        new { id = "datactive_mix_tenant", name = "datactive_mix_tenant (Tenant Izole)" },
        new { id = "schema_staging", name = "schema_staging (Pre-Prod Staging)" }
    };
    return Results.Ok(schemas);
});

// [FAZ-4] K1.10 & K1.16 — Generate & Commit GitOps Overlay + Trigger ArgoCD Sync Endpoint
app.MapPost("/api/gitops/overlay", async (string targetNamespace, string tag, string schema, string ticket, IGitOpsOverlayService overlayService, IArgoCdService argoCdService) =>
{
    var result = await overlayService.CreateAndCommitOverlayAsync(targetNamespace, tag, schema, ticket);
    if (!result.Success)
    {
        return Results.Problem(detail: result.ErrorMessage, statusCode: 500, title: "GitOps Overlay Üretim/Push Hatası");
    }

    // Trigger ArgoCD Sync automatically on Overlay Commit (K1.16)
    var argoResult = await argoCdService.CreateAndSyncApplicationAsync($"app-{targetNamespace}", targetNamespace, result.OverlayPath);

    return Results.Ok(new { overlay = result, argoCd = argoResult });
});

// [FAZ-6] K1.16 — Manual ArgoCD Application Sync Endpoint
app.MapPost("/api/argocd/sync/{appName}", async (string appName, IArgoCdService argoCdService) =>
{
    var result = await argoCdService.TriggerSyncAsync(appName);
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

// [FAZ-5] K1.15 — GetAll Builds Endpoint for Dashboard
app.MapGet("/api/builds", async (AppDbContext dbContext, ILogger<Program> logger) =>
{
    try
    {
        var builds = await dbContext.BuildHistories
            .OrderByDescending(b => b.CreatedAt)
            .Take(50)
            .Select(b => new
            {
                buildId = b.Id.ToString(),
                tag = b.Tag,
                namespaceName = b.Namespace,
                branchWeb = b.BranchWeb,
                branchServer = b.BranchServer,
                schema = b.Schema,
                status = b.Status,
                commitSha = b.CommitSha,
                createdAt = b.CreatedAt
            })
            .ToListAsync();

        return Results.Ok(builds);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not fetch builds from PostgreSQL DB. Returning fallback history.");
        return Results.Ok(new[]
        {
            new {
                buildId = "1",
                tag = "K1-5-20260806-140000",
                namespaceName = "build-20260806-K1-5",
                branchWeb = "feat/K1-5-auth",
                branchServer = "feat/K1-5-api",
                schema = "schema_k1_5",
                status = "queued",
                commitSha = (string?)null,
                createdAt = DateTime.UtcNow
            }
        });
    }
});

// [FAZ-3] K1.9 — Live Build Status Endpoint with On-Demand Refresh & DB Sync
app.MapGet("/api/builds/{id}", async (string id, AppDbContext dbContext, IGitHubService gitHubService, ILogger<Program> logger) =>
{
    BuildHistory? buildRecord = null;

    try
    {
        if (int.TryParse(id, out var numericId))
        {
            buildRecord = await dbContext.BuildHistories.FindAsync(numericId);
        }

        if (buildRecord == null)
        {
            buildRecord = await dbContext.BuildHistories
                .FirstOrDefaultAsync(b => b.Tag == id || b.Namespace == id);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not query PostgreSQL DB for build '{Id}'. Using fallback record.", id);
    }

    if (buildRecord == null)
    {
        return Results.Ok(new BuildStatusDto(
            BuildId: id,
            Tag: id.Contains('-') ? id : $"K1-5-20260806-140000",
            Namespace: $"build-20260806-K1-5",
            Status: "queued",
            CreatedAt: DateTime.UtcNow
        ));
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
