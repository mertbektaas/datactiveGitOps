using backend.Models;

namespace backend.Services;

public interface IBuildProvider
{
    Task<BuildResultDto> DispatchBuildAsync(BuildRequestDto request);
    Task<string?> GetStatusAsync(string tag);
    Task<string> GetLogsAsync(string tag);
}
