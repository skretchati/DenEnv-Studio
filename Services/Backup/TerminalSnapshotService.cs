using System;
using System.IO;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup;

public sealed class TerminalSnapshotService
{
    private static readonly Lazy<TerminalSnapshotService> _instance =
        new(() => new TerminalSnapshotService());

    public static TerminalSnapshotService Instance => _instance.Value;

    private TerminalSnapshotService()
    {
    }

    public TerminalSnapshot Collect()
    {
        var snapshot = new TerminalSnapshot();

        try
        {
            string localAppData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

            string settingsFile =
                Path.Combine(
                    localAppData,
                    "Packages",
                    "Microsoft.WindowsTerminal_8wekyb3d8bbwe",
                    "LocalState",
                    "settings.json");

            if (File.Exists(settingsFile))
            {
                snapshot.SettingsJson =
                    File.ReadAllText(settingsFile);
            }
        }
        catch
        {
        }

        return snapshot;
    }
}