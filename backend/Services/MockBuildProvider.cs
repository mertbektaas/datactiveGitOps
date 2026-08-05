using backend.Models;

namespace backend.Services;

public class MockBuildProvider : IBuildProvider
{
    private readonly ILogger<MockBuildProvider> _logger;

    public MockBuildProvider(ILogger<MockBuildProvider> logger)
    {
        _logger = logger;
    }

    public Task<BuildStatusDto> DispatchBuildAsync(BuildRequestDto request)
    {
        _logger.LogInformation(
            "Mock dispatching build for Web: {BranchWeb}, Server: {BranchServer}, Schema: {Schema}, Tag: {Tag}",
            request.BranchWeb, request.BranchServer, request.Schema, request.Tag
        );

        var buildId = Guid.NewGuid().ToString("N");
        var ticketPart = request.Tag.Contains('-') ? request.Tag.Split('-')[0] : "K1";
        var targetNamespace = $"build-{DateTime.UtcNow:yyyyMMdd}-{ticketPart}";

        var status = new BuildStatusDto(
            BuildId: buildId,
            Tag: request.Tag,
            Namespace: targetNamespace,
            Status: "queued",
            CreatedAt: DateTime.UtcNow
        );

        return Task.FromResult(status);
    }
}
