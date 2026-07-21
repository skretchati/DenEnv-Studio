namespace DevEnvStudio.Services.Backup.Models;

public sealed class PowerShellSnapshot
{
    public string ProfileContent { get; set; } = string.Empty;

    public List<PowerShellModuleInfo> Modules { get; set; } = new();

    public List<PowerShellRepositoryInfo> Repositories { get; set; } = new();
}

public sealed class PowerShellModuleInfo
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;
}

public sealed class PowerShellRepositoryInfo
{
    public string Name { get; set; } = string.Empty;

    public string SourceLocation { get; set; } = string.Empty;
}