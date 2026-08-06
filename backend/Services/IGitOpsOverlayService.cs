namespace backend.Services;

public interface IGitOpsOverlayService
{
    Task<OverlayGenerationResult> CreateAndCommitOverlayAsync(string targetNamespace, string tag, string schema, string ticket);
}

public record OverlayGenerationResult(
    bool Success,
    string Namespace,
    string OverlayPath,
    string? CommitSha,
    string? ErrorMessage
);
