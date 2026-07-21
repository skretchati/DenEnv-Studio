using System;
using System.Collections.Generic;
using System.IO;

namespace DevEnvStudio.Services.Backup.Models
{
    /// <summary>
    /// Defines what to collect for a backup operation.
    /// Drives ManifestBuilder file collection.
    /// </summary>
    public sealed class BackupProfile
    {
        public string Name        { get; init; } = "Default";
        public string Description { get; init; } = string.Empty;

        /// <summary>Absolute root paths to include recursively.</summary>
        public List<string> IncludeRoots { get; init; } = new();

        /// <summary>Absolute paths or wildcard patterns to exclude.</summary>
        public List<string> ExcludePaths { get; init; } = new();

        /// <summary>File extensions to skip (e.g. ".log", ".tmp").</summary>
        public List<string> ExcludeExtensions { get; init; } = new();

        /// <summary>Max single file size in bytes. 0 = no limit.</summary>
        public long MaxFileSizeBytes { get; init; } = 500L * 1024 * 1024; // 500 MB

        /// <summary>
        /// Optional user password for additional encryption layer.
        /// Set only when PasswordProtection is enabled in settings.
        /// </summary>
        public string? Password { get; init; }

        /// <summary>
        /// Destination folder for the created .devbackup file.
        /// Defaults to Desktop if null.
        /// </summary>
        public string? DestinationFolder { get; init; }

        public string ResolvedDestination =>
            DestinationFolder
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "DevEnvBackups");

        // ── Prebuilt developer profile ───────────────────────────────
        public static BackupProfile CreateDeveloperProfile()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return new BackupProfile
            {
                Name        = "Developer Environment",
                Description = "VS Code, Git, SSH, PowerShell, SDK configs",
                IncludeRoots = new List<string>
                {
                    // VS Code user settings
                    Path.Combine(appData, "Code", "User"),
                    // Git global config
                    Path.Combine(home, ".gitconfig"),
                    Path.Combine(home, ".gitattributes"),
                    // SSH keys
                    Path.Combine(home, ".ssh"),
                    // PowerShell profile
                    Path.Combine(home, "Documents", "PowerShell"),
                    Path.Combine(home, "Documents", "WindowsPowerShell"),
                    // NPM / Node config
                    Path.Combine(home, ".npmrc"),
                    Path.Combine(home, ".nvmrc"),
                    // Global dotfiles
                    Path.Combine(home, ".editorconfig"),
                    Path.Combine(home, ".wslconfig"),
                    // JetBrains configs
                    Path.Combine(appData, "JetBrains"),
                    // Windows Terminal settings
                    Path.Combine(localAppData, "Packages",
                        "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState"),
                },
                ExcludePaths = new List<string>
                {
                    Path.Combine(appData, "Code", "User", "workspaceStorage"),
                    Path.Combine(appData, "Code", "User", "globalStorage"),
                    Path.Combine(home, ".ssh", "known_hosts"),
                },
                ExcludeExtensions = new List<string>
                {
                    ".log", ".tmp", ".cache", ".lock",
                    ".pyc", ".class", ".o", ".obj",
                },
                MaxFileSizeBytes = 100L * 1024 * 1024
            };
        }
    }
}
