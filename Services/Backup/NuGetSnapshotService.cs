using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup;

public sealed class NuGetSnapshotService
{
    private static readonly Lazy<NuGetSnapshotService> _instance =
        new(() => new NuGetSnapshotService());

    public static NuGetSnapshotService Instance => _instance.Value;

    private NuGetSnapshotService()
    {
    }

    public NuGetSnapshot Collect()
    {
        var snapshot = new NuGetSnapshot();

        try
        {
            string configPath =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "NuGet",
                    "NuGet.Config");

            if (!File.Exists(configPath))
                return snapshot;

            snapshot.ConfigContent =
                File.ReadAllText(configPath);

            ParsePackageSources(
                configPath,
                snapshot.PackageSources);
        }
        catch
        {
        }

        return snapshot;
    }

    private static void ParsePackageSources(
        string configPath,
        List<string> sources)
    {
        try
        {
            var doc = XDocument.Load(configPath);

            var packageSources =
                doc.Descendants("packageSources")
                   .Elements("add");

            foreach (var source in packageSources)
            {
                string? key =
                    source.Attribute("key")?.Value;

                string? value =
                    source.Attribute("value")?.Value;

                if (!string.IsNullOrWhiteSpace(key) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    sources.Add($"{key} => {value}");
                }
            }
        }
        catch
        {
        }
    }
}