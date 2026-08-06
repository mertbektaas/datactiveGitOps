using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace backend.Services;

public class GitOpsOverlayService : IGitOpsOverlayService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GitOpsOverlayService> _logger;

    public GitOpsOverlayService(IWebHostEnvironment environment, IConfiguration configuration, ILogger<GitOpsOverlayService> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OverlayGenerationResult> CreateAndCommitOverlayAsync(string targetNamespace, string tag, string schema, string ticket)
    {
        try
        {
            // Root path of repository (parent of backend directory or current content root)
            var rootPath = FindRepositoryRoot(_environment.ContentRootPath);
            var overlayDir = Path.Combine(rootPath, "manifests", "overlays", targetNamespace);

            _logger.LogInformation("Generating Kustomize Overlay at path: {OverlayDir}", overlayDir);

            if (!Directory.Exists(overlayDir))
            {
                Directory.CreateDirectory(overlayDir);
            }

            var kustomizationYamlPath = Path.Combine(overlayDir, "kustomization.yaml");

            var kustomizationContent = new StringBuilder()
                .AppendLine("apiVersion: kustomize.config.k8s.io/v1beta1")
                .AppendLine("kind: Kustomization")
                .AppendLine($"namespace: {targetNamespace}")
                .AppendLine()
                .AppendLine("resources:")
                .AppendLine("  - \"../../base\"")
                .AppendLine()
                .AppendLine("images:")
                .AppendLine($"  - name: {_configuration["Registry:Image"] ?? "harbor.datactive.net/datateam/datactive.web"}")
                .AppendLine($"    newTag: \"{tag}\"")
                .AppendLine()
                .AppendLine("configMapGenerator:")
                .AppendLine("  - name: datactive-config")
                .AppendLine("    behavior: merge")
                .AppendLine("    literals:")
                .AppendLine($"      - DQLSchema={schema}")
                .AppendLine($"      - CustomerMetadataSchema={schema}.")
                .AppendLine($"      - ORACLEDataSchema={schema}.")
                .ToString();

            await File.WriteAllTextAsync(kustomizationYamlPath, kustomizationContent);

            var relativeOverlayPath = Path.Combine("manifests", "overlays", targetNamespace, "kustomization.yaml").Replace('\\', '/');

            // Perform Git Commit & Push
            var commitMessage = $"[build-{targetNamespace}] {ticket} - {schema}";
            var gitResult = await ExecuteGitCommitAndPushAsync(rootPath, relativeOverlayPath, commitMessage);

            if (!gitResult.Success)
            {
                _logger.LogError("Git commit/push failed for Overlay namespace '{Namespace}': {Error}", targetNamespace, gitResult.ErrorMessage);
                return new OverlayGenerationResult(false, targetNamespace, relativeOverlayPath, null, gitResult.ErrorMessage);
            }

            _logger.LogInformation("Successfully created, committed and pushed Overlay for namespace '{Namespace}' with Commit SHA: {CommitSha}",
                targetNamespace, gitResult.CommitSha);

            return new OverlayGenerationResult(true, targetNamespace, relativeOverlayPath, gitResult.CommitSha, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Kustomize overlay for namespace: {Namespace}", targetNamespace);
            return new OverlayGenerationResult(false, targetNamespace, string.Empty, null, ex.Message);
        }
    }

    private static string FindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return startPath;
    }

    private async Task<(bool Success, string? CommitSha, string? ErrorMessage)> ExecuteGitCommitAndPushAsync(string repoRoot, string filePattern, string commitMessage)
    {
        try
        {
            // git add
            var addResult = await RunProcessAsync("git", $"add \"{filePattern}\"", repoRoot);
            if (addResult.ExitCode != 0)
            {
                return (false, null, $"git add failed: {addResult.Error}");
            }

            // git commit
            var commitResult = await RunProcessAsync("git", $"commit -m \"{commitMessage}\"", repoRoot);
            if (commitResult.ExitCode != 0 && !commitResult.Output.Contains("nothing to commit"))
            {
                return (false, null, $"git commit failed: {commitResult.Error}");
            }

            // git rev-parse HEAD (get commit SHA)
            var shaResult = await RunProcessAsync("git", "rev-parse HEAD", repoRoot);
            var commitSha = shaResult.ExitCode == 0 ? shaResult.Output.Trim() : null;

            // git push (önce remote'u pull --rebase: başkasının push'larıyla çakışma olmasın)
            await RunProcessAsync("git", "pull --rebase", repoRoot);
            var pushResult = await RunProcessAsync("git", "push", repoRoot);
            if (pushResult.ExitCode != 0)
            {
                return (false, commitSha, $"git push failed: {pushResult.Error}");
            }

            return (true, commitSha, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string command, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return (process.ExitCode, outputBuilder.ToString().Trim(), errorBuilder.ToString().Trim());
    }
}
