using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevEnvStudio.Services.Backup;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services
{
    public sealed class BackupItem
    {
        public string   Name        { get; init; } = string.Empty;
        public string   FullPath    { get; init; } = string.Empty;
        public string   Environment { get; init; } = string.Empty;
        public string   Date        { get; init; } = string.Empty;
        public DateTime DateRaw     { get; init; }
        public long     SizeBytes   { get; init; }
        public string   SizeFmt     => FormatBytes(SizeBytes);
        public string   IconType    => ResolveIcon(Name, FullPath);
        public string   Status      { get; init; } = "Success";

        // ── DevBackup integration ───────────────────────────────────
        /// <summary>True when the file is a validated .devbackup container.</summary>
        public bool             IsDevBackup { get; init; }
        /// <summary>Populated for .devbackup files after header read. Null for plain ZIPs.</summary>
        public BackupMetadata?  Metadata    { get; set; }
        /// <summary>Unencrypted manifest from the .devbackup header, used for restore preview.</summary>
        public BackupManifest?  Manifest    { get; set; }
        /// <summary>Null = not yet validated, true = valid, false = tampered/corrupt.</summary>
        public bool?            IsValid     { get; set; }
        /// <summary>True when the backup has password-protected session key.</summary>
        public bool             IsPasswordProtected { get; set; }
        /// <summary>Root that restore will write into when no user override is selected.</summary>
        public string           RestoreRoot { get; set; } =
            global::System.Environment.GetFolderPath(
                global::System.Environment.SpecialFolder.UserProfile);
        /// <summary>Objects shown as they will be restored, not as archive-internal entries.</summary>
        public IReadOnlyList<ArchivePreviewEntry> PreviewEntries { get; set; }
            = Array.Empty<ArchivePreviewEntry>();

        // ── Computed preview properties (from unencrypted manifest) ────
        /// <summary>Human-readable summary of archive contents (e.g. "143 files, 12.5 MB").</summary>
        public string ContentSummary
        {
            get
            {
                if (Manifest is not { Entries.Count: > 0 } m) return "—";
                long total = 0;
                int fileCount = 0;
                foreach (var e in m.Entries)
                {
                    if (e.EntryType == "File")
                    {
                        total += e.SizeBytes;
                        fileCount++;
                    }
                }
                int dirCount = m.Entries.Count - fileCount;
                string sizePart = FormatBytes(total);
                string countPart = dirCount > 0
                    ? $"{fileCount} files, {dirCount} dirs"
                    : $"{fileCount} files";
                return $"{countPart} • {sizePart}";
            }
        }

        /// <summary>Number of entries in the unencrypted manifest (0 if unavailable).</summary>
        public int ManifestFileCount => Manifest?.FileCount ?? 0;

        /// <summary>Short description for tooltip/subtitle display.</summary>
        public string Subtitle
        {
            get
            {
                if (!IsDevBackup) return Environment;
                if (Metadata is { } m)
                    return $"{m.ProfileName} • {m.CreatedAtLocal}";
                return Environment;
            }
        }

        private static string FormatBytes(long b)
        {
            if (b < 1024L * 1024)             return $"{b / 1024.0:F1} KB";
            if (b < 1024L * 1024 * 1024)      return $"{b / (1024.0 * 1024):F1} MB";
            if (b < 1024L * 1024 * 1024 * 1024) return $"{b / (1024.0 * 1024 * 1024):F2} GB";
            return $"{b / (1024.0 * 1024 * 1024 * 1024):F2} TB";
        }

        private static string ResolveIcon(string name, string path)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("registry"))             return "Registry";
            if (lower.Contains("vs") || lower.Contains("vscode") || lower.Contains("code"))
                                                         return "Config";
            if (lower.Contains("sdk") || lower.Contains("tool") || lower.Contains("dev"))
                                                         return "Tools";
            if (lower.Contains("project") || lower.Contains("src") || lower.Contains("repo"))
                                                         return "Archive";
            if (lower.Contains("workstation") || lower.Contains("system") || lower.Contains("full"))
                                                         return "Desktop";
            return "Archive";
        }
    }

    public sealed class ArchivePreviewEntry
    {
        public string RestorePath { get; init; } = string.Empty;
        public string FileSize    { get; init; } = string.Empty;
        public string FileType    { get; init; } = string.Empty;
        public bool   IsFolder    { get; init; }
    }

    public sealed class ScanProgress
    {
        public string CurrentPath   { get; init; } = string.Empty;
        public int    FoundCount    { get; init; }
        public string Phase         { get; init; } = string.Empty;
    }

    public sealed class BackupScannerService
    {
        private static readonly Lazy<BackupScannerService> _instance =
            new(() => new BackupScannerService(), true);

        public static BackupScannerService Instance => _instance.Value;
        private BackupScannerService() { }

        private static readonly HashSet<string> _archiveExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".devbackup"   // ← DevEnvStudio native format
        };

        private static readonly HashSet<string> _backupKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "backup", "backups", "bak", "archive", "archives", "snapshot",
            "restore", "envbackup", "devenv", "devbackup"
        };

        // minimum size in bytes to count as a real backup (100 KB)
        private const long MinSizeBytes = 100 * 1024;

        public async Task ScanAsync(
            Action<BackupItem>   onFound,
            Action<ScanProgress> onProgress,
            CancellationToken    ct = default)
        {
            var searchRoots = BuildSearchRoots();
            int found = 0;

            foreach (var (root, phase) in searchRoots)
            {
                if (ct.IsCancellationRequested) return;
                if (!Directory.Exists(root) && !File.Exists(root)) continue;

                LogService.Instance.Info($"Scanning: {root}");
                onProgress(new ScanProgress { CurrentPath = root, FoundCount = found, Phase = phase });

                await Task.Run(() =>
                    RecursiveScan(root, phase, onFound, onProgress, ref found, ct, 0),
                    ct).ConfigureAwait(false);
            }

            LogService.Instance.Info($"Scan complete. {found} backup(s) found.");
        }

        public BackupItem CreateItemFromFile(FileInfo fileInfo, string environment)
            => BuildItem(
                fileInfo.Name,
                fileInfo.FullName,
                environment,
                fileInfo.Length,
                fileInfo.LastWriteTime);

        private static List<(string path, string phase)> BuildSearchRoots()
        {
            var roots = new List<(string, string)>();

            // 1. Desktop
            roots.Add((
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Desktop"));

            // 2. Downloads
            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            roots.Add((downloads, "Downloads"));

            // 3. Documents
            roots.Add((
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Documents"));

            // 4. USB / Removable drives
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!drive.IsReady) continue;
                        if (drive.DriveType == DriveType.Removable ||
                            drive.DriveType == DriveType.Network)
                        {
                            roots.Add((drive.RootDirectory.FullName, "Removable Drive"));
                        }
                    }
                    catch { /* skip */ }
                }
            }
            catch { /* skip */ }

            // 5. Non-system fixed drives (D:, E:, etc.)
            try
            {
                string sysRoot = Path.GetPathRoot(
                    Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\";

                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!drive.IsReady) continue;
                        if (drive.DriveType != DriveType.Fixed) continue;
                        if (drive.RootDirectory.FullName.Equals(sysRoot,
                            StringComparison.OrdinalIgnoreCase)) continue;

                        roots.Add((drive.RootDirectory.FullName, $"Drive {drive.Name.TrimEnd('\\')}"));
                    }
                    catch { /* skip */ }
                }
            }
            catch { /* skip */ }

            // 6. System drive root (last, shallow)
            string sysRootPath = Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\";
            roots.Add((sysRootPath, "System Drive"));

            return roots;
        }

        private void RecursiveScan(
            string               path,
            string               phase,
            Action<BackupItem>   onFound,
            Action<ScanProgress> onProgress,
            ref int              found,
            CancellationToken    ct,
            int                  depth)
        {
            if (ct.IsCancellationRequested) return;

            // Cap depth: Desktop/Downloads = deep, system drive root = shallow
            int maxDepth = phase is "System Drive" ? 2 : 6;
            if (depth > maxDepth) return;

            // Check files at this level
            try
            {
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    if (ct.IsCancellationRequested) return;
                    TryAddFile(file, phase, onFound, onProgress, ref found);
                }
            }
            catch (UnauthorizedAccessException) { return; }
            catch (IOException) { return; }
            catch { return; }

            // Recurse into subdirectories
            IEnumerable<string> dirs;
            try
            {
                dirs = Directory.EnumerateDirectories(path);
            }
            catch { return; }

            foreach (var dir in dirs)
            {
                if (ct.IsCancellationRequested) return;

                // Skip system / hidden noise
                string dirName = Path.GetFileName(dir);
                if (ShouldSkipDirectory(dirName)) continue;

                // Throttle progress reports
                if (found % 3 == 0)
                    onProgress(new ScanProgress
                        { CurrentPath = dir, FoundCount = found, Phase = phase });

                RecursiveScan(dir, phase, onFound, onProgress, ref found, ct, depth + 1);
            }
        }

        private void TryAddFile(
            string               filePath,
            string               phase,
            Action<BackupItem>   onFound,
            Action<ScanProgress> onProgress,
            ref int              found)
        {
            try
            {
                string ext = Path.GetExtension(filePath);
                if (!_archiveExts.Contains(ext)) return;

                var fi = new FileInfo(filePath);
                if (!fi.Exists) return;
                if (fi.Length < MinSizeBytes) return;

                found++;
                var item = CreateItemFromFile(fi, phase);
                onFound(item);
                LogService.Instance.Info(
                    $"Found backup: {fi.Name} ({DriveSnapshot.FormatBytes(fi.Length)})");
            }
            catch { /* skip */ }
        }

        private void TryAddFolder(
            string               dirPath,
            string               phase,
            Action<BackupItem>   onFound,
            Action<ScanProgress> onProgress,
            ref int              found)
        {
            try
            {
                var di = new DirectoryInfo(dirPath);
                if (!di.Exists) return;

                // Estimate folder size (fast: first 500 files)
                long size = 0;
                int  cnt  = 0;
                foreach (var f in di.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try { size += f.Length; } catch { /* skip */ }
                    if (++cnt > 500) break;
                }
                if (size < MinSizeBytes) return;

                found++;
                var item = BuildItem(di.Name, dirPath, phase, size, di.LastWriteTime);
                onFound(item);
                LogService.Instance.Info(
                    $"Found backup folder: {di.Name} (~{DriveSnapshot.FormatBytes(size)})");
            }
            catch { /* skip */ }
        }

        private static BackupItem BuildItem(
            string   name,
            string   path,
            string   phase,
            long     sizeBytes,
            DateTime date)
        {
            bool isDevBackup = string.Equals(
                Path.GetExtension(name), ".devbackup",
                StringComparison.OrdinalIgnoreCase);

            var item = new BackupItem
            {
                Name        = name,
                FullPath    = path,
                Environment = phase,
                Date        = date.ToString("MMM dd, yyyy HH:mm"),
                DateRaw     = date,
                SizeBytes   = sizeBytes,
                Status      = "Success",
                IsDevBackup = isDevBackup
            };

            // Enrich .devbackup items with unencrypted header sections before UI binding.
            if (isDevBackup)
                EnrichDevBackupItem(item);

            return item;
        }

        private static void EnrichDevBackupItem(BackupItem item)
        {
            try
            {
                var header = BackupContainerService.Instance
                    .ReadHeaderAsync(item.FullPath, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                item.Metadata = header.Metadata;
                item.Manifest = header.Manifest;
                item.PreviewEntries = BuildPreviewEntries(header.Manifest, item.RestoreRoot);
                item.IsValid  = true;
                item.IsPasswordProtected = header.Metadata.IsPasswordProtected;

                LogService.Instance.Info(
                    $"[Scanner] DevBackup: {header.Metadata.BackupName} — " +
                    $"{header.Manifest.FileCount} files, " +
                    $"created {header.Metadata.CreatedAtLocal} on {header.Metadata.MachineName}");
            }
            catch (Exception ex)
            {
                item.IsValid = false;
                LogService.Instance.Warn(
                    $"[Scanner] Could not read .devbackup header for {item.Name}: {ex.Message}");
            }
        }

        internal static IReadOnlyList<ArchivePreviewEntry> BuildPreviewEntries(
            BackupManifest manifest,
            string? destinationOverride = null)
        {
            string restoreRoot = ResolveRestoreRoot(destinationOverride);

            return manifest.Entries
                .Select(e => ToRestorePreviewObject(e, restoreRoot))
                .Where(e => e != null)
                .GroupBy(e => e!.RestorePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => BuildGroupedPreviewEntry(g.Select(e => e!).ToList()))
                .OrderBy(e => e.IsFolder ? 0 : 1)
                .ThenBy(e => e.RestorePath, StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();
        }

        private static string ResolveRestoreRoot(string? destinationOverride)
        {
            if (!string.IsNullOrWhiteSpace(destinationOverride))
                return destinationOverride;

            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private static string GetRestorePath(ManifestEntry entry, string restoreRoot)
        {
            string relativePath = entry.RelativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            return Path.GetFullPath(Path.Combine(restoreRoot, relativePath));
        }

        private sealed class RestorePreviewObject
        {
            public string RestorePath { get; init; } = string.Empty;
            public string RelativeRoot { get; init; } = string.Empty;
            public long SizeBytes { get; init; }
            public bool IsFile { get; init; }
        }

        private static RestorePreviewObject? ToRestorePreviewObject(
            ManifestEntry entry,
            string restoreRoot)
        {
            string relativePath = NormalizeRelativePath(entry.RelativePath);
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            string relativeRoot = GetTopLevelRestoreObject(relativePath);
            if (string.IsNullOrWhiteSpace(relativeRoot))
                return null;

            return new RestorePreviewObject
            {
                RelativeRoot = relativeRoot,
                RestorePath = Path.GetFullPath(Path.Combine(restoreRoot, relativeRoot)),
                SizeBytes = entry.EntryType == "File" ? entry.SizeBytes : 0,
                IsFile = entry.EntryType == "File"
                    && relativePath.Equals(relativeRoot, StringComparison.OrdinalIgnoreCase)
            };
        }

        private static ArchivePreviewEntry BuildGroupedPreviewEntry(
            IReadOnlyList<RestorePreviewObject> objects)
        {
            var first = objects[0];
            bool isFile = objects.Any(o => o.IsFile);
            long totalBytes = objects.Sum(o => o.SizeBytes);

            return new ArchivePreviewEntry
            {
                RestorePath = first.RestorePath,
                FileSize = totalBytes > 0 ? FormatPreviewBytes(totalBytes) : "—",
                FileType = isFile ? GetDisplayType(first.RelativeRoot) : "Folder",
                IsFolder = !isFile
            };
        }

        private static string NormalizeRelativePath(string relativePath)
            => relativePath
                .Replace('\\', '/')
                .Trim('/');

        private static string GetTopLevelRestoreObject(string relativePath)
        {
            int slash = relativePath.IndexOf('/');
            return slash < 0 ? relativePath : relativePath[..slash];
        }

        private static string GetDisplayType(ManifestEntry entry)
        {
            if (entry.EntryType != "File")
                return "Folder";

            return GetDisplayType(entry.RelativePath);
        }

        private static string GetDisplayType(string path)
        {
            string ext = Path.GetExtension(path).TrimStart('.');
            return string.IsNullOrWhiteSpace(ext)
                ? "File"
                : ext.ToUpperInvariant();
        }

        private static string FormatPreviewBytes(long b)
        {
            if (b < 1024L)               return $"{b} B";
            if (b < 1024L * 1024)        return $"{b / 1024.0:F1} KB";
            if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):F1} MB";
            return $"{b / (1024.0 * 1024 * 1024):F2} GB";
        }

        private static bool IsBackupFolderName(string name)
        {
            string lower = name.ToLowerInvariant();
            return _backupKeywords.Any(k => lower.Contains(k));
        }

        private static readonly HashSet<string> _skipDirs = new(StringComparer.OrdinalIgnoreCase)
        {
            "windows", "winnt", "system32", "syswow64", "program files",
            "program files (x86)", "programdata", "winsxs",
            "$recycle.bin", "system volume information",
            "node_modules", ".git", ".vs", "__pycache__",
            "bin", "obj", "debug", "release",
            "recovery", "perflogs"
        };

        private static bool ShouldSkipDirectory(string name)
            => _skipDirs.Contains(name) || name.StartsWith('$');
    }
}
