using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using backend.Data;
using backend.Models;
using backend.Options;

namespace backend.Services;

public class GitHubBuildProvider : IBuildProvider
{
    private readonly HttpClient _httpClient;
    private readonly GitHubOptions _options;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<GitHubBuildProvider> _logger;

    public GitHubBuildProvider(
        HttpClient httpClient, 
        IOptions<GitHubOptions> options, 
        AppDbContext dbContext, 
        ILogger<GitHubBuildProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<BuildStatusDto> DispatchBuildAsync(BuildRequestDto request)
    {
        var owner = string.IsNullOrEmpty(_options.Owner) ? "mertbektaas" : _options.Owner;
        var repo = "datactiveGitOps"; // Main GitOps repository holding workflows/build.yml
        var url = $"repos/{owner}/{repo}/actions/workflows/build.yml/dispatches";

        _logger.LogInformation("Dispatching real GitHub Actions build for Tag: {Tag}, Web: {BranchWeb}, Server: {BranchServer}",
            request.Tag, request.BranchWeb, request.BranchServer);

        var payload = new
        {
            @ref = "main",
            inputs = new
            {
                branch_web = request.BranchWeb,
                branch_server = request.BranchServer,
                schema = request.Schema,
                tag = request.Tag
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogError("GitHub API Authorization failed (401) during workflow_dispatch.");
                throw new InvalidOperationException("GitHub API yetkilendirme hatası (401). Lütfen GITHUB_TOKEN izinlerini kontrol edin.");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Workflow file build.yml or repository '{Repo}' not found (404).", repo);
                throw new KeyNotFoundException($"GitHub workflow 'build.yml' veya depo '{repo}' bulunamadı (404).");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                _logger.LogError("GitHub workflow_dispatch failed with status {StatusCode}: {ErrorText}", response.StatusCode, errorText);
                throw new HttpRequestException($"GitHub Actions tetikleme hatası ({response.StatusCode}): {errorText}");
            }

            var ticketPart = request.Tag.Contains('-') ? request.Tag.Split('-')[0] : "K1";
            var targetNamespace = $"build-{DateTime.UtcNow:yyyyMMdd}-{ticketPart}";

            // Save build record to PostgreSQL DB
            var buildRecord = new BuildHistory
            {
                Tag = request.Tag,
                Namespace = targetNamespace,
                BranchWeb = request.BranchWeb,
                BranchServer = request.BranchServer,
                Schema = request.Schema,
                Status = "queued",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.BuildHistories.Add(buildRecord);
            await _dbContext.SaveChangesAsync();

            return new BuildStatusDto(
                BuildId: buildRecord.Id.ToString(),
                Tag: buildRecord.Tag,
                Namespace: buildRecord.Namespace,
                Status: buildRecord.Status,
                CreatedAt: buildRecord.CreatedAt
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while dispatching workflow for Tag: {Tag}", request.Tag);
            throw;
        }
    }
}
