using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup;

public sealed class PowerShellSnapshotService
{
private static readonly Lazy<PowerShellSnapshotService> _instance =
new(() => new PowerShellSnapshotService());
public static PowerShellSnapshotService Instance => _instance.Value;

private PowerShellSnapshotService()
{
}

public PowerShellSnapshot Collect()
{
    var snapshot = new PowerShellSnapshot();

    try
    {
        using (var ps = PowerShell.Create())
        {
            ps.AddScript("$PROFILE");

            string profilePath =
                ps.Invoke()
                  .FirstOrDefault()
                  ?.ToString() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(profilePath) &&
                File.Exists(profilePath))
            {
                snapshot.ProfileContent =
                    File.ReadAllText(profilePath);
            }
        }

        using var psModules = PowerShell.Create();

        psModules.AddScript(
            "Get-InstalledModule | Select Name,Version");

        var modulesResult =
            psModules.Invoke();

        foreach (var item in modulesResult)
        {
            var module =
                new PowerShellModuleInfo
                {
                    Name =
                        item.Properties["Name"]
                            ?.Value
                            ?.ToString() ?? "",

                    Version =
                        item.Properties["Version"]
                            ?.Value
                            ?.ToString() ?? ""
                };

            if (!snapshot.Modules.Any(
                    x =>
                        x.Name == module.Name &&
                        x.Version == module.Version))
            {
                snapshot.Modules.Add(module);
            }
        }

        using var psRepos = PowerShell.Create();

        psRepos.AddScript(
            "Get-PSRepository | Select Name,SourceLocation");

        var reposResult =
            psRepos.Invoke();

        foreach (var item in reposResult)
        {
            snapshot.Repositories.Add(
                new PowerShellRepositoryInfo
                {
                    Name =
                        item.Properties["Name"]
                            ?.Value
                            ?.ToString() ?? "",

                    SourceLocation =
                        item.Properties["SourceLocation"]
                            ?.Value
                            ?.ToString() ?? ""
                });
        }

        snapshot.Modules =
            snapshot.Modules
                .OrderBy(x => x.Name)
                .ToList();

        snapshot.Repositories =
            snapshot.Repositories
                .OrderBy(x => x.Name)
                .ToList();
    }
    catch
    {
    }

    return snapshot;
}
}
