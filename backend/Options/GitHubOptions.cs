namespace backend.Options;

public class GitHubOptions
{
    public const string SectionName = "GitHub";
    public string Token { get; set; } = string.Empty;
    public string Owner { get; set; } = "mertbektaas";
    public string WebRepo { get; set; } = "datateam-web";
    public string ServerRepo { get; set; } = "datateam-core.server";
}
