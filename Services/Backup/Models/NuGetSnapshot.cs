using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models;

public sealed class NuGetSnapshot
{
    public string ConfigContent { get; set; } = string.Empty;

    public List<string> PackageSources { get; set; } = new();
}