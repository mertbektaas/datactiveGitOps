using backend.Models;

namespace backend.Services;

public interface IBuildProvider
{
    Task<BuildStatusDto> DispatchBuildAsync(BuildRequestDto request);
}
