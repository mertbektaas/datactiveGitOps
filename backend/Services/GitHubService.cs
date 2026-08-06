using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using backend.Options;

namespace backend.Services;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(HttpClient httpClient, IOptions<GitHubOptions> options, ILogger<GitHubService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<BranchDto>> GetBranchesAsync(string repoType)
    {
        var repoName = string.Equals(repoType, "server", StringComparison.OrdinalIgnoreCase)
            ? _options.ServerRepo
            : _options.WebRepo;

        var owner = string.IsNullOrEmpty(_options.Owner) ? "mertbektaas" : _options.Owner;
        var url = $"repos/{owner}/{repoName}/branches";

        try
        {
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub API branch query returned status {StatusCode} for repo '{RepoName}'. Falling back to default branches.", response.StatusCode, repoName);
                return GetFallbackBranches(repoType);
            }

            var branches = await response.Content.ReadFromJsonAsync<List<GitHubBranchResponse>>();
            if (branches == null || !branches.Any()) return GetFallbackBranches(repoType);

            return branches.Select(b => new BranchDto(
                b.Name, 
                b.Name.Equals("main", StringComparison.OrdinalIgnoreCase) || b.Name.Equals("master", StringComparison.OrdinalIgnoreCase)
            ));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching live branches from GitHub for repo: {RepoName}. Using fallback mock branches.", repoName);
            return GetFallbackBranches(repoType);
        }
    }

    private static IEnumerable<BranchDto> GetFallbackBranches(string repoType)
    {
        if (string.Equals(repoType, "server", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new BranchDto("main", true),
                new BranchDto("feat/K1-5-api", false),
                new BranchDto("feat/13333-api", false),
                new BranchDto("fix/auth-jwt-server", false)
            };
        }

        return new[]
        {
            new BranchDto("main", true),
            new BranchDto("feat/K1-5-auth", false),
            new BranchDto("feat/13333-fix", false),
            new BranchDto("fix/login-ui-web", false)
        };
    }

    public async Task<string?> GetWorkflowRunStatusAsync(string tag)
    {
        var owner = string.IsNullOrEmpty(_options.Owner) ? "mertbektaas" : _options.Owner;
        var repo = "datactiveGitOps";
        var url = $"repos/{owner}/{repo}/actions/runs?per_page=10";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch workflow runs from GitHub API. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var runsResponse = await response.Content.ReadFromJsonAsync<GitHubWorkflowRunsResponse>();
            if (runsResponse?.WorkflowRuns == null || !runsResponse.WorkflowRuns.Any())
            {
                return null;
            }

            // Find run matching the tag or pick the latest run
            var run = runsResponse.WorkflowRuns.FirstOrDefault(r => 
                (r.DisplayTitle != null && r.DisplayTitle.Contains(tag, StringComparison.OrdinalIgnoreCase)) ||
                (r.Name != null && r.Name.Contains(tag, StringComparison.OrdinalIgnoreCase))) 
                ?? runsResponse.WorkflowRuns.FirstOrDefault();

            if (run == null) return null;

            return MapGitHubRunStatus(run.Status, run.Conclusion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching live workflow run status for Tag: {Tag}", tag);
            return null;
        }
    }

    private static string MapGitHubRunStatus(string? status, string? conclusion)
    {
        var s = status?.ToLowerInvariant();
        var c = conclusion?.ToLowerInvariant();

        if (s == "queued" || s == "requested" || s == "waiting")
        {
            return "queued";
        }

        if (s == "in_progress")
        {
            return "in_progress";
        }

        if (s == "completed")
        {
            return c == "success" ? "success" : "failure";
        }

        return "in_progress";
    }
}

public class GitHubBranchResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("protected")]
    public bool Protected { get; set; }
}

public class GitHubWorkflowRunsResponse
{
    [JsonPropertyName("workflow_runs")]
    public List<GitHubWorkflowRunItem>? WorkflowRuns { get; set; }
}

public class GitHubWorkflowRunItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("display_title")]
    public string? DisplayTitle { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; set; }
}
