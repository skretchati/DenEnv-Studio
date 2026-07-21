using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models
{
    /// <summary>
    /// SHA-256 digest per manifest entry.
    /// Stored UNENCRYPTED — enables tamper detection without decryption.
    /// Also stored INSIDE the encrypted payload for post-decrypt verification.
    /// </summary>
    public sealed class ChecksumEntry
    {
        /// <summary>Matches ManifestEntry.RelativePath exactly.</summary>
        public string RelativePath { get; init; } = string.Empty;

        /// <summary>Lowercase hex SHA-256 of the raw (uncompressed) file bytes.</summary>
        public string Sha256Hex    { get; init; } = string.Empty;

        /// <summary>File size in bytes — cross-check without hashing.</summary>
        public long   SizeBytes    { get; init; }
    }

    public sealed class ChecksumManifest
    {
        public string BackupId { get; init; } = string.Empty;
        public List<ChecksumEntry> Entries { get; init; } = new();

        // Fast lookup by relative path
        private Dictionary<string, ChecksumEntry>? _index;

        public ChecksumEntry? FindByPath(string relativePath)
        {
            _index ??= BuildIndex();
            return _index.GetValueOrDefault(relativePath);
        }

        private Dictionary<string, ChecksumEntry> BuildIndex()
        {
            var d = new Dictionary<string, ChecksumEntry>(
                Entries.Count, System.StringComparer.OrdinalIgnoreCase);
            foreach (var e in Entries)
                d[e.RelativePath] = e;
            return d;
        }
    }
}
