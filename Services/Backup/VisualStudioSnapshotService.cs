using System;
using System.Diagnostics;
using DevEnvStudio.Services.Backup.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace DevEnvStudio.Services.Backup;

public sealed class VisualStudioSnapshotService
{
    private static readonly Lazy<VisualStudioSnapshotService> _instance =
        new(() => new VisualStudioSnapshotService());

    public static VisualStudioSnapshotService Instance => _instance.Value;

    private VisualStudioSnapshotService()
    {
    }

    public VisualStudioSnapshot Collect()
    {
        var snapshot = new VisualStudioSnapshot();

        try
        {
            string output = RunVsWhere();

            snapshot.InstanceId =
                Extract(output, "instanceId:");

            snapshot.InstallPath =
                Extract(output, "installationPath:");

            snapshot.Version =
                Extract(output, "installationVersion:");

            snapshot.Edition =
                Extract(output, "displayName:");
            TryLoadWorkloads(snapshot);
        }
        catch
        {
        }

        return snapshot;
    }

    private static string RunVsWhere()
    {
        string vswhere =
            Environment.ExpandEnvironmentVariables(
                @"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe");

        var psi = new ProcessStartInfo
        {
            FileName = vswhere,
            Arguments = "-all",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);

        string output =
            process?.StandardOutput.ReadToEnd() ?? "";

        process?.WaitForExit();

        return output;
    }

    private static string Extract(string text, string key)
    {
        foreach (string line in text.Split('\n'))
        {
            if (line.StartsWith(key))
                return line[key.Length..].Trim();
        }

        return string.Empty;
    }
    private static void TryLoadWorkloads(
    VisualStudioSnapshot snapshot)
{
    try
    {
        string stateFile =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "Microsoft",
                "VisualStudio",
                "Packages",
                "_Instances",
                snapshot.InstanceId,
                "state.packages.json");

        if (!File.Exists(stateFile))
            return;

        string json =
            File.ReadAllText(stateFile);

        var matches =
            Regex.Matches(
                json,
                @"Microsoft\.VisualStudio\.Workload\.[A-Za-z0-9\.]+");

        foreach (Match match in matches)
        {
            if (!snapshot.Workloads.Contains(match.Value))
                snapshot.Workloads.Add(match.Value);
        }
    }
    catch
    {
    }
}
}