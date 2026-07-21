using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup;

public sealed class PythonSnapshotService
{
    private static readonly Lazy<PythonSnapshotService> _instance =
        new(() => new PythonSnapshotService());

    public static PythonSnapshotService Instance => _instance.Value;

    private PythonSnapshotService()
    {
    }

    public PythonSnapshot Collect()
    {
        var snapshot = new PythonSnapshot();

        try
        {
            string version =
                RunSingle(
                    "python",
                    "--version");

            if (!string.IsNullOrWhiteSpace(version))
            {
                snapshot.Versions.Add(version);
            }

            snapshot.Packages =
                RunList(
                    "pip.exe",
                    "list --format=freeze");
        }
        catch
        {
        }

        return snapshot;
    }

    private static string RunSingle(
        string file,
        string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);

        string stdout =
            process?.StandardOutput.ReadToEnd() ?? "";

        string stderr =
            process?.StandardError.ReadToEnd() ?? "";

        process?.WaitForExit();

        return string.IsNullOrWhiteSpace(stdout)
            ? stderr.Trim()
            : stdout.Trim();
    }

    private static List<string> RunList(
        string file,
        string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);

        string stdout =
            process?.StandardOutput.ReadToEnd() ?? "";

        string stderr =
            process?.StandardError.ReadToEnd() ?? "";

        process?.WaitForExit();

        string output =
            string.IsNullOrWhiteSpace(stdout)
                ? stderr
                : stdout;

        return output
            .Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !x.StartsWith("[notice"))
            .Where(x => !x.StartsWith("WARNING:"))
            .ToList();
    }
}