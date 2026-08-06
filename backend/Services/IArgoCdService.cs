namespace backend.Services;

public interface IArgoCdService
{
    Task<ArgoCdSyncResult> CreateAndSyncApplicationAsync(string appName, string targetNamespace, string overlayPath);
    Task<ArgoCdSyncResult> TriggerSyncAsync(string appName);
}

public record ArgoCdSyncResult(
    bool Success,
    string AppName,
    string Status,
    string? Message
);
