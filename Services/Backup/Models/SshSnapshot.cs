using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models;

public sealed class SshSnapshot
{
    public string ConfigContent { get; set; } = string.Empty;

    public string KnownHostsContent { get; set; } = string.Empty;

    public Dictionary<string, string> PrivateKeys { get; set; } = new();

    public Dictionary<string, string> PublicKeys { get; set; } = new();
}