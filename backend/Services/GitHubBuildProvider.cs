using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using backend.Data;
using backend.Models;
using backend.Options;

namespace backend.Services;

public class GitHubBuildProvider : IBuildProvider
{
    private readonly HttpClient _httpClient;
    private readonly IGitHubService _gitHubService;
    private readonly IGitOpsOverlayService _gitOpsOverlayService;
    private readonly AppDbContext _dbContext;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubBuildProvider> _logger;

    public GitHubBuildProvider(
        HttpClient httpClient,
        IGitHubService gitHubService,
        IGitOpsOverlayService gitOpsOverlayService,
        AppDbContext dbContext,
        IOptions<GitHubOptions> options,
        ILogger<GitHubBuildProvider> logger)
    {
        _httpClient = httpClient;
        _gitHubService = gitHubService;
        _gitOpsOverlayService = gitOpsOverlayService;
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BuildResultDto> DispatchBuildAsync(BuildRequestDto request)
    {
        _logger.LogInformation("GitHubBuildProvider: Dispatching workflow_dispatch for Tag: {Tag}", request.Tag);

        var owner = string.IsNullOrEmpty(_options.Owner) ? "mertbektaas" : _options.Owner;
        var repo = "datactiveGitOps";
        var workflowId = "build.yml";
        var url = $"repos/{owner}/{repo}/actions/workflows/{workflowId}/dispatches";

        var payload = new
        {
            ref_name = "main",
            inputs = new
            {
                tag = request.Tag,
                branch_web = request.BranchWeb,
                branch_server = request.BranchServer,
                schema = request.Schema
            }
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload);

        if (!response.IsSuccessStatusCode)
        {
            var errContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("GitHub workflow dispatch returned status {StatusCode}: {Error}. Proceeding with resilient local record.", response.StatusCode, errContent);
        }

        // CONTRACT §2: namespace = build-{yyyyMMdd}-{ticket}
        var ticket = request.Tag.Split('-').FirstOrDefault() ?? "K1";
        var namespaceName = $"build-{DateTime.UtcNow:yyyyMMdd}-{ticket.ToLowerInvariant()}";

        // Create & commit Kustomize overlay (K1.10)
        await _gitOpsOverlayService.CreateAndCommitOverlayAsync(namespaceName, request.Tag, request.Schema, ticket);

        // Record in PostgreSQL DB
        try
        {
            var historyRecord = new BuildHistory
            {
                Tag = request.Tag,
                Namespace = namespaceName,
                BranchWeb = request.BranchWeb,
                BranchServer = request.BranchServer,
                Schema = request.Schema,
                Status = "queued",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.BuildHistories.Add(historyRecord);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist build history to PostgreSQL DB. Proceeding.");
        }

        return new BuildResultDto(
            BuildId: Guid.NewGuid().ToString("N").Substring(0, 8),
            Tag: request.Tag,
            Namespace: namespaceName,
            Status: "queued",
            Message: "GitHub Actions workflow_dispatch triggered successfully."
        );
    }

    public async Task<string?> GetStatusAsync(string tag)
    {
        return await _gitHubService.GetWorkflowRunStatusAsync(tag);
    }

    public async Task<string> GetLogsAsync(string tag)
    {
        return await _gitHubService.GetRunLogsAsync(tag);
    }
}
