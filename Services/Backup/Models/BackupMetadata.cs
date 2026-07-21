using System;
using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models
{
    /// <summary>
    /// Stored UNENCRYPTED in the .devbackup container.
    /// Readable without decryption — used for preview and restore planning.
    /// </summary>
    public sealed class BackupMetadata
    {
        // ── Identity ────────────────────────────────────────────────
        public string BackupId      { get; init; } = Guid.NewGuid().ToString();
        public string BackupName    { get; init; } = string.Empty;
        public string ProfileName   { get; init; } = string.Empty;

        // ── Format ──────────────────────────────────────────────────
        public int    FormatVersion { get; init; } = 1;
        public string AppVersion    { get; init; } = "1.0.0";

        // ── Timestamps ──────────────────────────────────────────────
        public DateTime CreatedAtUtc   { get; init; } = DateTime.UtcNow;
        public string   CreatedAtLocal { get; init; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // ── Source machine (informational only, not used for crypto) ─
        public string MachineName   { get; init; } = Environment.MachineName;
        public string UserName      { get; init; } = Environment.UserName;
        public string OsVersion     { get; init; } = Environment.OSVersion.VersionString;

        // ── Content summary (unencrypted preview) ───────────────────
        public int    TotalFiles         { get; set; }
        public long   TotalUncompressed  { get; set; }
        public long   TotalCompressed    { get; set; }
        public string CompressionRatio   => TotalUncompressed > 0
            ? $"{100.0 - (TotalCompressed * 100.0 / TotalUncompressed):F1}%"
            : "—";

        // ── Encryption descriptor (algorithm info only, no keys) ────
        public string EncryptionAlgorithm { get; init; } = "AES-256-GCM";
        public string KeyDerivation       { get; init; } = "HKDF-SHA256";
        public string Compression         { get; init; } = "GZip";

        /// <summary>
        /// True when the backup is additionally protected with a user password
        /// (double-wrapped session key). Used by the UI to prompt for password.
        /// </summary>
        public bool IsPasswordProtected { get; init; }

        // ── Profile snapshot ────────────────────────────────────────
public List<string> IncludedRoots { get; init; } = new();
public List<string> ExcludedPaths { get; init; } = new();
public GitSnapshot? Git { get; set; }

// ── Development Environment ────────────────────────────────
public VsCodeSnapshot? VsCode { get; set; }
// ── Ssh snapshot ────────────────────────────────
public SshSnapshot? Ssh { get; set; }
// ── Windows Terminal ────────────────────────────────
public TerminalSnapshot? Terminal { get; set; }
// ── EnvironmentSnapshot ────────────────────────────────
public EnvironmentSnapshot? EnvironmentVariables { get; set; }
// ── DotNetSnapshot ────────────────────────────────
public DotNetSnapshot? DotNet { get; set; }
// ── VisualStudioSnapshot ────────────────────────────────
public VisualStudioSnapshot? VisualStudio { get; set; }
// ── NodeSnapshot ────────────────────────────────
public NodeSnapshot? Node { get; set; }
// ── PythonSnapshot ────────────────────────────────
public PythonSnapshot? Python { get; set; }
// ── NuGetSnapshot ────────────────────────────────
public NuGetSnapshot? NuGet { get; set; }
// ── PowerShellModulesSnapshot ────────────────────────────────
public PowerShellSnapshot? PowerShell { get; set; }
// ── CertificateSnapshot ────────────────────────────────
public CertificateSnapshot? Certificates { get; set; }
// ── WslSnapshot ────────────────────────────────
public WslSnapshot? Wsl { get; set; }
    }
}
