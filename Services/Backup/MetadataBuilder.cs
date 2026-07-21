using System;
using System.Collections.Generic;
using System.Linq;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup
{
    /// <summary>
    /// Builds BackupMetadata from a profile and manifest.
    /// Snapshots machine info at call time.
    /// Integrates with existing SystemInfoService snapshot (passed in to avoid double scan).
    /// </summary>
    public sealed class MetadataBuilder
    {
        private static readonly Lazy<MetadataBuilder> _instance =
            new(() => new MetadataBuilder(), true);
        public static MetadataBuilder Instance => _instance.Value;
        private MetadataBuilder() { }

        private const string AppVersion = "1.0.0";

        /// <summary>
        /// Builds the metadata record.
        /// Called AFTER ManifestBuilder so content stats can be embedded.
        /// </summary>
        public BackupMetadata Build(
            string backupId,
            BackupProfile profile,
            BackupManifest manifest,
            string? osVersion   = null,
            long compressedSize = 0,
            bool   isProtected  = false)
        {
            var fileEntries = manifest.Entries
                .Where(e => e.EntryType == "File")
                .ToList();

            long totalUncompressed = fileEntries.Sum(e => e.SizeBytes);

            string backupName = $"{profile.Name}_{DateTime.Now:yyyy-MM-dd_HH-mm}";
            if (isProtected)
                backupName = $"{backupName}[PROTECTED]";

            var meta = new BackupMetadata
            {
                BackupId         = backupId,
                BackupName       = backupName,
                ProfileName      = profile.Name,
                FormatVersion    = 1,
                AppVersion       = AppVersion,
                CreatedAtUtc     = DateTime.UtcNow,
                CreatedAtLocal   = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                MachineName      = Environment.MachineName,
                UserName         = Environment.UserName,
                OsVersion        = osVersion ?? Environment.OSVersion.VersionString,
                TotalFiles       = fileEntries.Count,
                TotalUncompressed = totalUncompressed,
                TotalCompressed  = compressedSize,
                EncryptionAlgorithm = "AES-256-GCM",
                KeyDerivation    = "HKDF-SHA256",
                Compression      = "GZip",
                IncludedRoots    = new List<string>(profile.IncludeRoots),
ExcludedPaths = new List<string>(profile.ExcludePaths),

VsCode = VsCodeSnapshotService.Instance.Collect(),

Git = GitSnapshotService.Instance.Collect(),

Ssh = SshSnapshotService.Instance.Collect(),

Terminal = TerminalSnapshotService.Instance.Collect(),

EnvironmentVariables =
    EnvironmentSnapshotService.Instance.Collect(),
    
DotNet = DotNetSnapshotService.Instance.Collect(),

VisualStudio =
    VisualStudioSnapshotService.Instance.Collect(),

Node = NodeSnapshotService.Instance.Collect(),

Python = PythonSnapshotService.Instance.Collect(),

NuGet = NuGetSnapshotService.Instance.Collect(),

PowerShell =
    PowerShellSnapshotService.Instance.Collect(),

Certificates =
    CertificateSnapshotService.Instance.Collect(),
    
Wsl = WslSnapshotService.Instance.Collect(),
                IsPasswordProtected = isProtected
            };

            return meta;
        }

        /// <summary>
        /// Generates the output filename for a .devbackup container.
        /// Format: ProfileName_YYYY-MM-DD_HH-mm.devbackup
        /// </summary>
        public static string BuildFileName(string profileName, DateTime? at = null)
        {
            string ts = (at ?? DateTime.Now).ToString("yyyy-MM-dd_HH-mm");
            string safe = SanitizeFileName(profileName);
            return $"{safe}_{ts}.devbackup";
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
