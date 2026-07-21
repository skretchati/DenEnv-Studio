using System;
using System.Linq;
using Microsoft.Win32;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup;

public sealed class WslSnapshotService
{
    private static readonly Lazy<WslSnapshotService> _instance =
        new(() => new WslSnapshotService());

    public static WslSnapshotService Instance => _instance.Value;

    private WslSnapshotService()
    {
    }

    public WslSnapshot Collect()
    {
        var snapshot = new WslSnapshot();

        try
        {
            if (!IsWslInstalled())
                return snapshot;

            snapshot.WslVersion =
                ProcessRunner.Run(
                    "wsl",
                    "--version");

            ParseDistributions(snapshot);
        }
        catch
        {
        }

        return snapshot;
    }

    private static bool IsWslInstalled()
    {
        using var key =
            Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Lxss");

        return key != null;
    }

    private static void ParseDistributions(
        WslSnapshot snapshot)
    {
        var lines =
            ProcessRunner.Run(
                "wsl",
                "-l -v")
            .Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in lines)
        {
            string line = raw.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith(
                "NAME",
                StringComparison.OrdinalIgnoreCase))
                continue;

            bool isDefault =
                line.StartsWith("*");

            if (isDefault)
                line = line[1..].Trim();

            string[] parts =
                line.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
                continue;

            string version = parts[^1];
            string state = parts[^2];

            string name =
                string.Join(
                    " ",
                    parts.Take(parts.Length - 2));

            snapshot.Distributions.Add(
                new WslDistributionInfo
                {
                    Name = name,
                    State = state,
                    Version = version
                });

            if (isDefault)
                snapshot.DefaultDistribution = name;
        }
    }
}