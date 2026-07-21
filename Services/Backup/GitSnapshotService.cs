using System;
using System.IO;
using DevEnvStudio.Services.Backup.Models;
using System.Linq;

namespace DevEnvStudio.Services.Backup;

public sealed class GitSnapshotService
{
    private static readonly Lazy<GitSnapshotService> _instance =
        new(() => new GitSnapshotService());

    public static GitSnapshotService Instance => _instance.Value;

    private GitSnapshotService() { }

    public GitSnapshot Collect()
    {
        var snapshot = new GitSnapshot();

        try
        {
            string home =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);

            string gitConfig =
                Path.Combine(home, ".gitconfig");

            if (File.Exists(gitConfig))
{
    snapshot.GitConfigContent =
        File.ReadAllText(gitConfig);

    ParseGitConfig(
        snapshot.GitConfigContent,
        snapshot);
}

            string globalIgnore =
                Path.Combine(home, ".gitignore_global");

            if (File.Exists(globalIgnore))
                snapshot.GlobalGitIgnoreContent =
                    File.ReadAllText(globalIgnore);
        }
        catch
        {
        }

        return snapshot;
    }
    private static void ParseGitConfig(
    string config,
    GitSnapshot snapshot)
{
    try
    {
        string currentSection = "";

        foreach (string rawLine in config.Split('\n'))
        {
            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("[") &&
                line.EndsWith("]"))
            {
                currentSection =
                    line.Trim('[', ']')
                        .ToLowerInvariant();

                continue;
            }

            int eq = line.IndexOf('=');

            if (eq < 0)
                continue;

            string key =
                line[..eq].Trim()
                    .ToLowerInvariant();

            string value =
                line[(eq + 1)..].Trim();

            if (currentSection == "user")
            {
                if (key == "name")
                    snapshot.UserName = value;

                if (key == "email")
                    snapshot.UserEmail = value;
            }

            if (currentSection == "init")
            {
                if (key == "defaultbranch")
                    snapshot.DefaultBranch = value;
            }
        }
    }
    catch
    {
    }
}
}