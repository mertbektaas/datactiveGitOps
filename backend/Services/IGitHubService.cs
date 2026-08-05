namespace backend.Services;

public interface IGitHubService
{
    Task<IEnumerable<BranchDto>> GetBranchesAsync(string repoType);
}
