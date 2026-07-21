using System;
using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models
{
    /// <summary>
    /// Per-file entry list stored UNENCRYPTED in the container.
    /// Enables content preview and restore planning without decryption.
    /// </summary>
    public sealed class ManifestEntry
    {
        /// <summary>Relative path inside the payload archive.</summary>
        public string RelativePath  { get; init; } = string.Empty;

        /// <summary>Absolute source path at backup creation time.</summary>
        public string SourcePath    { get; init; } = string.Empty;

        /// <summary>Absolute destination path for restore.</summary>
        public string DestPath      { get; init; } = string.Empty;

        public long     SizeBytes       { get; init; }
        public DateTime LastModifiedUtc { get; init; }
        public string   EntryType       { get; init; } = "File"; // File | Directory | Symlink

        public string SizeFmt => FormatBytes(SizeBytes);
        public string LastModifiedLocal =>
            LastModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        private static string FormatBytes(long b)
        {
            if (b < 1024L)               return $"{b} B";
            if (b < 1024L * 1024)        return $"{b / 1024.0:F1} KB";
            if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):F1} MB";
            return $"{b / (1024.0 * 1024 * 1024):F2} GB";
        }
    }

    public sealed class BackupManifest
    {
        public string             BackupId { get; init; } = string.Empty;
        public List<ManifestEntry> Entries  { get; init; } = new();

        public int FileCount => Entries.Count;
    }

    /// <summary>
    /// Extension helpers used by BackupEngine and RestoreEngine.
    /// Kept separate from the model so the model has no LINQ dependency.
    /// </summary>
    public static class BackupManifestExtensions
    {
        /// <summary>
        /// Returns (relativePath, absoluteSourcePath) pairs for File entries only.
        /// </summary>
        public static IReadOnlyList<(string relative, string absolute)>
            GetFilePairs(this BackupManifest m)
        {
            var list = new List<(string, string)>(m.Entries.Count);
            foreach (var e in m.Entries)
                if (e.EntryType == "File")
                    list.Add((e.RelativePath, e.SourcePath));
            return list;
        }

        /// <summary>
        /// Sum of SizeBytes for File entries only.
        /// </summary>
        public static long TotalSizeBytes(this BackupManifest m)
        {
            long s = 0;
            foreach (var e in m.Entries)
                if (e.EntryType == "File") s += e.SizeBytes;
            return s;
        }
    }
}
