using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models
{
    public enum ValidationStatus
    {
        Valid,
        InvalidMagic,         // Not a .devbackup file
        UnsupportedVersion,   // Format version newer than this app supports
        TamperDetected,       // HMAC-SHA256 signature mismatch
        KeyCorrupted,         // Session key GCM auth tag failure
        PayloadCorrupted,     // Payload GCM auth tag failure
        ChecksumMismatch,     // One or more file SHA256 mismatches
        MetadataCorrupted,    // Cannot parse metadata.json
        ManifestCorrupted,    // Cannot parse manifest.json
        IOError,              // Filesystem read error
        UnknownError
    }

    public sealed class ValidationResult
    {
        public ValidationStatus   Status       { get; init; }
        public bool               IsValid      => Status == ValidationStatus.Valid;
        public string             Message      { get; init; } = string.Empty;

        /// <summary>Populated after successful metadata parse — usable even if payload is corrupt.</summary>
        public BackupMetadata?    Metadata     { get; init; }
        public BackupManifest?    Manifest     { get; init; }

        /// <summary>Per-file checksum failures when Status == ChecksumMismatch.</summary>
        public List<string>       FailedFiles  { get; init; } = new();

        // ── Factory helpers ─────────────────────────────────────────
        public static ValidationResult Ok(BackupMetadata meta, BackupManifest manifest) =>
            new() { Status = ValidationStatus.Valid, Message = "Archive is valid.",
                    Metadata = meta, Manifest = manifest };

        public static ValidationResult Fail(ValidationStatus status, string message,
            BackupMetadata? meta = null, List<string>? failedFiles = null) =>
            new() { Status = status, Message = message, Metadata = meta,
                    FailedFiles = failedFiles ?? new() };
    }
}
