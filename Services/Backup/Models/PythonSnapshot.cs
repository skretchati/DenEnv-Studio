using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models;

public sealed class PythonSnapshot
{
    public List<string> Versions { get; set; } = new();

    public List<string> Packages { get; set; } = new();
}