using System;
using System.Linq;
using Microsoft.Win32;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup;

public sealed class EnvironmentSnapshotService
{
    private static readonly Lazy<EnvironmentSnapshotService> _instance =
        new(() => new EnvironmentSnapshotService());

    public static EnvironmentSnapshotService Instance => _instance.Value;

    private EnvironmentSnapshotService()
    {
    }

    public EnvironmentSnapshot Collect()
    {
        var snapshot = new EnvironmentSnapshot();

        try
        {
            ReadUserVariables(snapshot);
            ReadSystemVariables(snapshot);
        }
        catch
        {
        }

        return snapshot;
    }

    private static void ReadUserVariables(EnvironmentSnapshot snapshot)
    {
        using var key =
            Registry.CurrentUser.OpenSubKey("Environment");

        if (key == null)
            return;

        foreach (var name in key.GetValueNames())
        {
            string value =
                key.GetValue(name)?.ToString() ?? string.Empty;

            if (name.Equals("Path", StringComparison.OrdinalIgnoreCase))
            {
                snapshot.UserPathEntries =
                    value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                         .Select(x => x.Trim())
                         .ToList();
            }
            else
            {
                snapshot.UserVariables[name] = value;
            }
        }
    }

    private static void ReadSystemVariables(EnvironmentSnapshot snapshot)
    {
        using var key =
            Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");

        if (key == null)
            return;

        foreach (var name in key.GetValueNames())
        {
            string value =
                key.GetValue(name)?.ToString() ?? string.Empty;

            if (name.Equals("Path", StringComparison.OrdinalIgnoreCase))
            {
                snapshot.SystemPathEntries =
                    value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                         .Select(x => x.Trim())
                         .ToList();
            }
            else
            {
                snapshot.SystemVariables[name] = value;
            }
        }
    }
}