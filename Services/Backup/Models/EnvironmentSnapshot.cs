using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models;

public sealed class EnvironmentSnapshot
{
    public Dictionary<string, string> UserVariables { get; set; } = new();

    public Dictionary<string, string> SystemVariables { get; set; } = new();

    public List<string> UserPathEntries { get; set; } = new();

    public List<string> SystemPathEntries { get; set; } = new();
}