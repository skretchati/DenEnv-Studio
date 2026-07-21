namespace DevEnvStudio.Services.Backup.Models;

public sealed class VsCodeSnapshot
{
    public string? SettingsPath { get; set; }

    public string SettingsJson { get; set; } = string.Empty;

    public string KeybindingsJson { get; set; } = string.Empty;

    public string TasksJson { get; set; } = string.Empty;

    public string LaunchJson { get; set; } = string.Empty;

    public List<VsCodeExtensionInfo> Extensions { get; set; } = new();
}

public sealed class VsCodeExtensionInfo
{
    public string Id { get; set; } = string.Empty;

    public string Publisher { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;
}