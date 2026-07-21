using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models;

public sealed class WslSnapshot
{
    public string WslVersion { get; set; } = string.Empty;

    public string DefaultDistribution { get; set; } = string.Empty;

    public List<WslDistributionInfo> Distributions { get; set; } = new();
}

public sealed class WslDistributionInfo
{
    public string Name { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;
}