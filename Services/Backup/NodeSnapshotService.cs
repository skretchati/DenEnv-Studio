using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup;

public sealed class NodeSnapshotService
{
    private static readonly Lazy<NodeSnapshotService> _instance =
        new(() => new NodeSnapshotService());

    public static NodeSnapshotService Instance => _instance.Value;

    private NodeSnapshotService()
    {
    }

    public NodeSnapshot Collect()
    {
        var snapshot = new NodeSnapshot();

        try
        {
            string npmPath = FindNpm();

            snapshot.NodeVersion =
                RunSingle("node", "--version");

            snapshot.NpmVersion =
                RunSingle(npmPath, "--version");

            snapshot.GlobalPackages =
                RunList(
                    npmPath,
                    "list -g --depth=0");
        }
        catch
        {
        }

        return snapshot;
    }

    private static string FindNpm()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "npm.cmd",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            string output =
                process?.StandardOutput.ReadToEnd() ?? "";

            process?.WaitForExit();

            string[] paths =
                output.Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);

            string? preferred =
                paths.FirstOrDefault(
                    x => x.Contains(
                        @"AppData\Roaming\npm",
                        StringComparison.OrdinalIgnoreCase));

            return preferred ??
                   paths.LastOrDefault() ??
                   "npm.cmd";
        }
        catch
        {
            return "npm.cmd";
        }
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

    // убираем путь npm
    .Where(x => !x.Contains(@":\"))

    // убираем служебные строки npm
    .Where(x => !x.StartsWith("npm notice"))

    // убираем древовидные символы
    .Select(x =>
        x.Replace("+-- ", "")
         .Replace("`-- ", "")
         .Replace("|", "")
         .Trim())

    .ToList();
    }
}