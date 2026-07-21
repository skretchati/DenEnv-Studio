using System;
using System.Diagnostics;

namespace DevEnvStudio.Services.Backup;

internal static class ProcessRunner
{
    private const int DefaultTimeoutMs = 5000;

    public static string Run(
        string fileName,
        string arguments,
        int timeoutMs = DefaultTimeoutMs)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            if (process == null)
                return string.Empty;

            if (!process.WaitForExit(timeoutMs))
            {
                process.Kill(true);
                return string.Empty;
            }

            string stdout =
                process.StandardOutput.ReadToEnd();

            string stderr =
                process.StandardError.ReadToEnd();

            return string.IsNullOrWhiteSpace(stdout)
                ? stderr.Trim()
                : stdout.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}