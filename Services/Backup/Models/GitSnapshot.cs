namespace DevEnvStudio.Services.Backup.Models;

public sealed class GitSnapshot
{
    public string GitConfigContent { get; set; } = string.Empty;

    public string GlobalGitIgnoreContent { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string DefaultBranch { get; set; } = string.Empty;
}