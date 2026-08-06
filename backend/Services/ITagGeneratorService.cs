namespace backend.Services;

public interface ITagGeneratorService
{
    string GenerateTag(string? branchWeb, string? branchServer = null);
}
