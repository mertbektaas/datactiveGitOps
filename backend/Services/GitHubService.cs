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

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogError("GitHub API Unauthorized (401). Check GitHub Token.");
                throw new InvalidOperationException("GitHub API yetkilendirme hatası (401). Lütfen GITHUB_TOKEN bilgisini kontrol edin.");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Repository '{RepoName}' not found (404) on GitHub.", repoName);
                throw new KeyNotFoundException($"Depo '{repoName}' GitHub üzerinde bulunamadı (404).");
            }

            response.EnsureSuccessStatusCode();

            var branches = await response.Content.ReadFromJsonAsync<List<GitHubBranchResponse>>();
            if (branches == null) return Enumerable.Empty<BranchDto>();

            return branches.Select(b => new BranchDto(
                b.Name, 
                b.Name.Equals("main", StringComparison.OrdinalIgnoreCase) || b.Name.Equals("master", StringComparison.OrdinalIgnoreCase)
            ));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling GitHub API for repo: {RepoName}", repoName);
            throw;
        }
    }
}

public class GitHubBranchResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("protected")]
    public bool Protected { get; set; }
}
