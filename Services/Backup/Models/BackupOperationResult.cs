using System;
using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models
{
    public enum OperationStatus { Success, PartialSuccess, Failed, Cancelled }

    public sealed class BackupOperationResult
    {
        public OperationStatus Status       { get; init; }
        public bool            Succeeded    => Status == OperationStatus.Success
                                           || Status == OperationStatus.PartialSuccess;
        public string          Message      { get; init; } = string.Empty;

        // ── Timing ──────────────────────────────────────────────────
        public DateTime StartedAtUtc   { get; init; }
        public DateTime CompletedAtUtc { get; init; }
        public TimeSpan Duration       => CompletedAtUtc - StartedAtUtc;
        public string   DurationFmt    => Duration.TotalSeconds < 60
            ? $"{Duration.TotalSeconds:F1}s"
            : $"{(int)Duration.TotalMinutes}m {Duration.Seconds}s";

        // ── Counters ─────────────────────────────────────────────────
        public int  FilesProcessed  { get; init; }
        public int  FilesSkipped    { get; init; }
        public int  FilesErrored    { get; init; }
        public long BytesProcessed  { get; init; }

        // ── Output ───────────────────────────────────────────────────
        /// <summary>Absolute path of the created .devbackup file (backup ops).</summary>
        public string? OutputPath { get; init; }

        /// <summary>Paths actually written on disk (restore ops).</summary>
        public List<string> RestoredPaths { get; init; } = new();

        /// <summary>Per-file errors logged during the operation.</summary>
        public List<string> Errors { get; init; } = new();

        // ── Factory helpers ──────────────────────────────────────────
        public static BackupOperationResult Succeed(
            DateTime start, int files, long bytes, string? outputPath = null,
            List<string>? restored = null, int skipped = 0) =>
            new()
            {
                Status         = OperationStatus.Success,
                Message        = "Operation completed successfully.",
                StartedAtUtc   = start,
                CompletedAtUtc = DateTime.UtcNow,
                FilesProcessed = files,
                FilesSkipped   = skipped,
                BytesProcessed = bytes,
                OutputPath     = outputPath,
                RestoredPaths  = restored ?? new()
            };

        public static BackupOperationResult Fail(DateTime start, string message,
            List<string>? errors = null) =>
            new()
            {
                Status         = OperationStatus.Failed,
                Message        = message,
                StartedAtUtc   = start,
                CompletedAtUtc = DateTime.UtcNow,
                Errors         = errors ?? new()
            };

        public static BackupOperationResult Cancel(DateTime start) =>
            new()
            {
                Status         = OperationStatus.Cancelled,
                Message        = "Operation was cancelled.",
                StartedAtUtc   = start,
                CompletedAtUtc = DateTime.UtcNow,
            };
    }
}
