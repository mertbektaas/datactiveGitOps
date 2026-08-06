using backend.Models;

namespace backend.Services;

public interface IGitHubService
{
    Task<IEnumerable<BranchDto>> GetBranchesAsync(string repoType);
    Task<string?> GetWorkflowRunStatusAsync(string tag);
    Task<string> GetRunLogsAsync(string tag);
}
