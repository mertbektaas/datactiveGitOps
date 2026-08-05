using System.Text.Json.Serialization;

namespace backend.Models;

public record BuildRequestDto(
    [property: JsonPropertyName("branch_web")] string BranchWeb,
    [property: JsonPropertyName("branch_server")] string BranchServer,
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("tag")] string Tag
);

public record BuildStatusDto(
    [property: JsonPropertyName("buildId")] string BuildId,
    [property: JsonPropertyName("tag")] string Tag,
    [property: JsonPropertyName("namespace")] string Namespace,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt
);
