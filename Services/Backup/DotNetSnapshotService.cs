using System;
using System.Diagnostics;
using System.Linq;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup;

public sealed class DotNetSnapshotService
{
    private static readonly Lazy<DotNetSnapshotService> _instance =
        new(() => new DotNetSnapshotService());

    public static DotNetSnapshotService Instance => _instance.Value;

    private DotNetSnapshotService()
    {
    }

    public DotNetSnapshot Collect()
    {
        var snapshot = new DotNetSnapshot();

        try
        {
            snapshot.Sdks =
    Run(
        "dotnet",
        "--list-sdks");

            snapshot.Runtimes =
                Run("dotnet", "--list-runtimes");

            snapshot.Workloads =
                Run("dotnet", "workload list");
        }
        catch
        {
        }

        return snapshot;
    }

    private static System.Collections.Generic.List<string>
        Run(string file, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi);

        string output =
            p?.StandardOutput.ReadToEnd() ?? "";

        p?.WaitForExit();

        return output
            .Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }
}