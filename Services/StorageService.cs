using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DevEnvStudio.Services
{
    public sealed class DriveSnapshot
    {
        public string Letter      { get; init; } = string.Empty;
        public string Label       { get; init; } = string.Empty;
        public string DriveType   { get; init; } = string.Empty;
        public string Format      { get; init; } = string.Empty;
        public long   TotalBytes  { get; init; }
        public long   FreeBytes   { get; init; }
        public long   UsedBytes   => TotalBytes - FreeBytes;
        public double UsedPct     => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100.0 : 0;
        public string TotalFmt    => FormatBytes(TotalBytes);
        public string UsedFmt     => FormatBytes(UsedBytes);
        public string FreeFmt     => FormatBytes(FreeBytes);
        public string UsedPctFmt  => $"{UsedPct:F0}%";

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0)       return "0 B";
            if (bytes < 1024L * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024):F1} MB";
            if (bytes < 1024L * 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
            return $"{bytes / (1024.0 * 1024 * 1024 * 1024):F2} TB";
        }
    }

    public sealed class StorageService
    {
        private static readonly Lazy<StorageService> _instance =
            new(() => new StorageService(), true);

        public static StorageService Instance => _instance.Value;
        private StorageService() { }

        public Task<List<DriveSnapshot>> GetAllDrivesAsync()
            => Task.Run(ScanDrives);

        public Task<DriveSnapshot?> GetPrimaryDriveAsync()
            => Task.Run(() =>
            {
                var drives = ScanDrives();
                string sysRoot = Path.GetPathRoot(
                    Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\";
                return drives.FirstOrDefault(d =>
                    d.Letter.Equals(sysRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    ?? drives.FirstOrDefault();
            });

        private static List<DriveSnapshot> ScanDrives()
        {
            var result = new List<DriveSnapshot>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!drive.IsReady) continue;

                        result.Add(new DriveSnapshot
                        {
                            Letter    = drive.RootDirectory.FullName.TrimEnd('\\'),
                            Label     = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                                            ? drive.DriveType.ToString()
                                            : drive.VolumeLabel,
                            DriveType = drive.DriveType.ToString(),
                            Format    = drive.DriveFormat,
                            TotalBytes = drive.TotalSize,
                            FreeBytes  = drive.AvailableFreeSpace
                        });
                    }
                    catch { /* skip inaccessible */ }
                }
            }
            catch { /* ignore if DriveInfo.GetDrives() fails */ }

            return result;
        }
    }
}
