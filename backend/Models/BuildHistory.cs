namespace backend.Models;

public class BuildHistory
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string BranchWeb { get; set; } = string.Empty;
    public string BranchServer { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Status { get; set; } = "queued"; // queued, in_progress, success, failure
    public string? CommitSha { get; set; }
    public string? RunId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}
