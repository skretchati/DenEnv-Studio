using System;
using System.IO;
using System.Linq;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup
{
    public sealed class VsCodeSnapshotService
    {
        private static readonly Lazy<VsCodeSnapshotService> _instance =
            new(() => new VsCodeSnapshotService());

        public static VsCodeSnapshotService Instance => _instance.Value;

        private VsCodeSnapshotService() { }

        public VsCodeSnapshot Collect()
        {
            var snapshot = new VsCodeSnapshot();

            try
            {
                string appData = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);

                string vscodeUser = Path.Combine(
                    appData,
                    "Code",
                    "User");

                snapshot.SettingsPath = vscodeUser;
                string settingsFile =
    Path.Combine(vscodeUser, "settings.json");

if (File.Exists(settingsFile))
    snapshot.SettingsJson = File.ReadAllText(settingsFile);

string keybindingsFile =
    Path.Combine(vscodeUser, "keybindings.json");

if (File.Exists(keybindingsFile))
    snapshot.KeybindingsJson = File.ReadAllText(keybindingsFile);

string tasksFile =
    Path.Combine(vscodeUser, "tasks.json");

if (File.Exists(tasksFile))
    snapshot.TasksJson = File.ReadAllText(tasksFile);

string launchFile =
    Path.Combine(vscodeUser, "launch.json");

if (File.Exists(launchFile))
    snapshot.LaunchJson = File.ReadAllText(launchFile);

                string extRoot = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    ".vscode",
                    "extensions");

                if (Directory.Exists(extRoot))
                {
                    foreach (var dir in Directory.GetDirectories(extRoot))
                    {
                        try
                        {
                            string name = Path.GetFileName(dir);

int versionSeparator = name.LastIndexOf('-');

if (versionSeparator <= 0)
    continue;

string extensionPart = name[..versionSeparator];
string versionPart = name[(versionSeparator + 1)..];

int publisherSeparator = extensionPart.IndexOf('.');

if (publisherSeparator <= 0)
    continue;

string publisher =
    extensionPart[..publisherSeparator];

string extensionId =
    extensionPart[(publisherSeparator + 1)..];

snapshot.Extensions.Add(
    new VsCodeExtensionInfo
    {
        Publisher = publisher,
        Id = extensionId,
        Version = versionPart
    });
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }

            return snapshot;
        }
    }
}