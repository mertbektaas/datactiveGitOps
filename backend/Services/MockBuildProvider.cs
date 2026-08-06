using backend.Models;

namespace backend.Services;

public class MockBuildProvider : IBuildProvider
{
    private readonly ILogger<MockBuildProvider> _logger;

    public MockBuildProvider(ILogger<MockBuildProvider> logger)
    {
        _logger = logger;
    }

    public Task<BuildResultDto> DispatchBuildAsync(BuildRequestDto request)
    {
        _logger.LogInformation("MockBuildProvider: Simulating build dispatch for Tag: {Tag}", request.Tag);

        var namespaceName = $"build-{request.Tag.ToLowerInvariant()}";

        var result = new BuildResultDto(
            BuildId: Guid.NewGuid().ToString("N").Substring(0, 8),
            Tag: request.Tag,
            Namespace: namespaceName,
            Status: "queued",
            Message: "Mock build dispatched successfully (Test Mode)."
        );

        return Task.FromResult(result);
    }

    public Task<string?> GetStatusAsync(string tag)
    {
        return Task.FromResult<string?>("success");
    }

    public Task<string> GetLogsAsync(string tag)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var mockLogs = $"[MOCK {now}] Mock Build Provider Test Environment Console Trace.\n" +
                       $"[INFO {now}] Simulating build execution for Tag: {tag}...\n" +
                       $"[INFO {now}] Step 1: Mocking Docker image build (harbor.datactive.net/datateam/datactive.web:{tag})...\n" +
                       $"[INFO {now}] Step 2: Mocking Kustomize overlay generation...\n" +
                       $"[SUCCESS {now}] Mock pipeline completed with status: SUCCESS.";
        return Task.FromResult(mockLogs);
    }
}
