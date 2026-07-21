using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models;

public sealed class VisualStudioSnapshot
{
    public string Edition { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string InstallPath { get; set; } = string.Empty;

    public string InstanceId { get; set; } = string.Empty;

    public List<string> Workloads { get; set; } = new();

    public List<VisualStudioExtensionInfo> Extensions { get; set; } = new();
}

public sealed class VisualStudioExtensionInfo
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;
}