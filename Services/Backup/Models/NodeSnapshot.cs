using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models;

public sealed class NodeSnapshot
{
    public string NodeVersion { get; set; } = string.Empty;

    public string NpmVersion { get; set; } = string.Empty;

    public List<string> GlobalPackages { get; set; } = new();
}