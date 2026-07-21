using System;
using System.Management;
using System.Threading.Tasks;

namespace DevEnvStudio.Services
{
    public sealed class SystemSnapshot
    {
        public string OsVersion    { get; init; } = string.Empty;
        public string CpuName      { get; init; } = string.Empty;
        public string RamTotal     { get; init; } = string.Empty;
        public string StorageUsed  { get; init; } = string.Empty;
        public string StorageTotal { get; init; } = string.Empty;
        public string StorageFree  { get; init; } = string.Empty;
        public double StoragePct   { get; init; }
        public string DriveLetter  { get; init; } = string.Empty;
        public string DriveType    { get; init; } = string.Empty;
    }

    public sealed class SystemInfoService
    {
        private static readonly Lazy<SystemInfoService> _instance =
            new(() => new SystemInfoService(), true);

        public static SystemInfoService Instance => _instance.Value;

        private SystemInfoService() { }

        public Task<SystemSnapshot> GetSnapshotAsync()
            => Task.Run(CollectSnapshot);

        private SystemSnapshot CollectSnapshot()
        {
            string os      = GetOs();
            string cpu     = GetCpu();
            string ram     = GetRam();
            var    storage = GetPrimaryStorage();

            LogService.Instance.Info($"System scan complete. OS: {os}, CPU: {cpu}, RAM: {ram}");

            return new SystemSnapshot
            {
                OsVersion    = os,
                CpuName      = cpu,
                RamTotal     = ram,
                StorageUsed  = storage.used,
                StorageTotal = storage.total,
                StorageFree  = storage.free,
                StoragePct   = storage.pct,
                DriveLetter  = storage.letter,
                DriveType    = storage.driveType
            };
        }

        private static string GetOs()
        {
            try
            {
                using var s = new ManagementObjectSearcher(
                    "SELECT Caption,BuildNumber FROM Win32_OperatingSystem");
                foreach (ManagementObject o in s.Get())
                {
                    var caption = o["Caption"]?.ToString() ?? string.Empty;
                    // trim "Microsoft " prefix
                    caption = caption.Replace("Microsoft ", "").Trim();
                    return caption;
                }
            }
            catch { /* fall through */ }
            return Environment.OSVersion.VersionString;
        }

        private static string GetCpu()
        {
            try
            {
                using var s = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_Processor");
                foreach (ManagementObject o in s.Get())
                    return o["Name"]?.ToString()?.Trim() ?? "Unknown";
            }
            catch { /* fall through */ }
            return "Unknown CPU";
        }

        private static string GetRam()
        {
            try
            {
                using var s = new ManagementObjectSearcher(
                    "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject o in s.Get())
                {
                    if (o["TotalPhysicalMemory"] is ulong bytes)
                    {
                        double gb = bytes / (1024.0 * 1024 * 1024);
                        return $"{Math.Round(gb)} GB";
                    }
                }
            }
            catch { /* fall through */ }
            return "Unknown";
        }

        private static (string used, string total, string free,
                        double pct, string letter, string driveType) GetPrimaryStorage()
        {
            try
            {
                // pick system drive first, then largest fixed drive
                string sysRoot = System.IO.Path.GetPathRoot(
                    Environment.GetFolderPath(Environment.SpecialFolder.System))
                    ?? "C:\\";

                System.IO.DriveInfo? best = null;
                long bestSize = 0;

                foreach (var drive in System.IO.DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    if (drive.DriveType != System.IO.DriveType.Fixed) continue;

                    bool isSys = drive.RootDirectory.FullName
                        .Equals(sysRoot, StringComparison.OrdinalIgnoreCase);

                    if (isSys && best == null)
                    {
                        best = drive;
                        bestSize = drive.TotalSize;
                    }
                    else if (drive.TotalSize > bestSize && best == null)
                    {
                        best = drive;
                        bestSize = drive.TotalSize;
                    }
                }

                if (best != null)
                {
                    double totalGb = best.TotalSize  / (1024.0 * 1024 * 1024);
                    double freeGb  = best.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                    double usedGb  = totalGb - freeGb;
                    double pct     = totalGb > 0 ? usedGb / totalGb * 100 : 0;

                    string Fmt(double gb) =>
                        gb >= 1024 ? $"{gb / 1024:F2} TB" : $"{gb:F0} GB";

                    string driveType = best.DriveFormat.Contains("NTFS", StringComparison.OrdinalIgnoreCase)
                        ? "Local Drive (NTFS)"
                        : "Local Drive";

                    return (Fmt(usedGb), Fmt(totalGb), Fmt(freeGb),
                            Math.Round(pct, 1),
                            best.RootDirectory.FullName.TrimEnd('\\'),
                            driveType);
                }
            }
            catch { /* fall through */ }

            return ("N/A", "N/A", "N/A", 0, "C:", "Local Drive");
        }
    }
}
