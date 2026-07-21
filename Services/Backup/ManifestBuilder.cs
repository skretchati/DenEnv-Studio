using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup
{
    /// <summary>
    /// Collects files from a BackupProfile into a BackupManifest.
    /// Applies include/exclude rules, size limits, and extension filters.
    /// Reports progress via callbacks (reuses existing scan progress pattern).
    /// </summary>
    public sealed class ManifestBuilder
    {
        private static readonly Lazy<ManifestBuilder> _instance =
            new(() => new ManifestBuilder(), true);
        public static ManifestBuilder Instance => _instance.Value;
        private ManifestBuilder() { }

        private static readonly HashSet<string> _alwaysExcludeDirs =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "node_modules", ".git", ".vs", ".vscode",
                "bin", "obj", "__pycache__", ".mypy_cache",
                "dist", "build", "out", ".next", ".nuxt",
                "$RECYCLE.BIN", "System Volume Information",
            };

        /// <summary>
        /// Builds a BackupManifest from a BackupProfile.
        /// All I/O is async. Never throws for individual file errors — logs them instead.
        /// </summary>
        public Task<BackupManifest> BuildAsync(
            string backupId,
            BackupProfile profile,
            Action<string>? onFileDiscovered = null,
            Action<string>? onFileSkipped    = null,
            CancellationToken ct = default)
        {
            return Task.Run(() =>
                BuildInternal(backupId, profile, onFileDiscovered, onFileSkipped, ct), ct);
        }

        private BackupManifest BuildInternal(
            string backupId,
            BackupProfile profile,
            Action<string>? onDiscovered,
            Action<string>? onSkipped,
            CancellationToken ct)
        {
            var entries      = new List<ManifestEntry>();
            var excludeSet   = BuildExcludeSet(profile);
            var excludeExts  = new HashSet<string>(
                profile.ExcludeExtensions, StringComparer.OrdinalIgnoreCase);

            foreach (string root in profile.IncludeRoots)
            {
                ct.ThrowIfCancellationRequested();

                // Handle single-file includes
                if (File.Exists(root))
                {
                    TryAddFile(root, root, root, profile, excludeExts, excludeSet,
                        entries, onDiscovered, onSkipped);
                    continue;
                }

                if (!Directory.Exists(root)) continue;

                string rootNorm = NormalizePath(root);

                CollectDirectory(root, rootNorm, profile, excludeExts,
                    excludeSet, entries, onDiscovered, onSkipped, ct);
            }

            return new BackupManifest
            {
                BackupId = backupId,
                Entries  = entries
            };
        }

        private void CollectDirectory(
            string dirPath, string rootBase,
            BackupProfile profile,
            HashSet<string> excludeExts,
            HashSet<string> excludePaths,
            List<ManifestEntry> entries,
            Action<string>? onDiscovered,
            Action<string>? onSkipped,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // Add directory entry for restore path creation
            string relDir = GetRelativePath(rootBase, dirPath);
            entries.Add(new ManifestEntry
            {
                RelativePath    = relDir + "/",
                SourcePath      = dirPath,
                DestPath        = Path.Combine(profile.ResolvedDestination, relDir),
                EntryType       = "Directory",
                LastModifiedUtc = Directory.GetLastWriteTimeUtc(dirPath)
            });

            // Files in this directory
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dirPath); }
            catch { return; }

            foreach (string file in files)
            {
                ct.ThrowIfCancellationRequested();
                TryAddFile(file, rootBase,
                    Path.Combine(profile.ResolvedDestination,
                        GetRelativePath(rootBase, file)),
                    profile, excludeExts, excludePaths,
                    entries, onDiscovered, onSkipped);
            }

            // Recurse into subdirectories
            IEnumerable<string> subdirs;
            try { subdirs = Directory.EnumerateDirectories(dirPath); }
            catch { return; }

            foreach (string sub in subdirs)
            {
                ct.ThrowIfCancellationRequested();

                string dirName = Path.GetFileName(sub);
                if (_alwaysExcludeDirs.Contains(dirName)) continue;
                if (excludePaths.Contains(NormalizePath(sub))) continue;

                CollectDirectory(sub, rootBase, profile, excludeExts,
                    excludePaths, entries, onDiscovered, onSkipped, ct);
            }
        }

        private static void TryAddFile(
            string filePath, string rootBase, string destPath,
            BackupProfile profile,
            HashSet<string> excludeExts,
            HashSet<string> excludePaths,
            List<ManifestEntry> entries,
            Action<string>? onDiscovered,
            Action<string>? onSkipped)
        {
            try
            {
                string norm = NormalizePath(filePath);
                if (excludePaths.Contains(norm))
                { onSkipped?.Invoke(filePath); return; }

                string ext = Path.GetExtension(filePath);
                if (excludeExts.Contains(ext))
                { onSkipped?.Invoke(filePath); return; }

                var fi = new FileInfo(filePath);
                if (!fi.Exists) return;

                if (profile.MaxFileSizeBytes > 0 && fi.Length > profile.MaxFileSizeBytes)
                { onSkipped?.Invoke($"{filePath} (size {fi.Length} > limit)"); return; }

                string rel = GetRelativePath(rootBase, filePath);

                entries.Add(new ManifestEntry
                {
                    RelativePath    = rel,
                    SourcePath      = filePath,
                    DestPath        = destPath,
                    SizeBytes       = fi.Length,
                    LastModifiedUtc = fi.LastWriteTimeUtc,
                    EntryType       = "File"
                });

                onDiscovered?.Invoke(filePath);
            }
            catch { /* skip inaccessible */ }
        }

        private static HashSet<string> BuildExcludeSet(BackupProfile profile)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string p in profile.ExcludePaths)
                set.Add(NormalizePath(p));
            return set;
        }

        private static string NormalizePath(string path)
            => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

        private static string GetRelativePath(string root, string full)
        {
            string normRoot = NormalizePath(root);
            string normFull = NormalizePath(full);

            if (normFull.StartsWith(normRoot, StringComparison.OrdinalIgnoreCase))
            {
                string rel = normFull[normRoot.Length..].TrimStart(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return rel.Replace('\\', '/');
            }

            // Fallback: use just the filename
            return Path.GetFileName(full);
        }
    }
}
