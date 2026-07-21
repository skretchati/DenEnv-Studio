using System;
using System.IO;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup;

public sealed class SshSnapshotService
{
    private static readonly Lazy<SshSnapshotService> _instance =
        new(() => new SshSnapshotService());

    public static SshSnapshotService Instance => _instance.Value;

    private SshSnapshotService()
    {
    }

    public SshSnapshot Collect()
    {
        var snapshot = new SshSnapshot();

        try
        {
            string home =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);

            string sshDir =
                Path.Combine(home, ".ssh");

            if (!Directory.Exists(sshDir))
                return snapshot;

            string configFile =
                Path.Combine(sshDir, "config");

            if (File.Exists(configFile))
                snapshot.ConfigContent =
                    File.ReadAllText(configFile);

            string knownHostsFile =
                Path.Combine(sshDir, "known_hosts");

            if (File.Exists(knownHostsFile))
                snapshot.KnownHostsContent =
                    File.ReadAllText(knownHostsFile);

            foreach (string file in Directory.GetFiles(sshDir))
            {
                try
                {
                    string fileName =
                        Path.GetFileName(file);

                    if (fileName.EndsWith(".pub",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        snapshot.PublicKeys[fileName] =
                            File.ReadAllText(file);

                        continue;
                    }

                    if (fileName.StartsWith("id_",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        snapshot.PrivateKeys[fileName] =
                            File.ReadAllText(file);
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return snapshot;
    }
}