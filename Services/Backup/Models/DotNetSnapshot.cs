using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models;

public sealed class DotNetSnapshot
{
    public List<string> Sdks { get; set; } = new();

    public List<string> Runtimes { get; set; } = new();

    public List<string> Workloads { get; set; } = new();
}