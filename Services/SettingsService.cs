using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DevEnvStudio.Services
{
    /// <summary>
    /// Persistent application settings stored as JSON.
    /// All values are written immediately on change.
    /// </summary>
    public sealed class SettingsService
    {
        private static readonly Lazy<SettingsService> _instance =
            new(() => new SettingsService(), true);
        public static SettingsService Instance => _instance.Value;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevEnvStudio", "settings.json");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private SettingsData _data;

        private SettingsService()
        {
            _data = Load();
        }

        // ── Settings properties ──────────────────────────────────────

        /// <summary>"en" or "ru"</summary>
        public string Language
        {
            get => _data.Language;
            set { if (_data.Language != value) { _data.Language = value; Save(); } }
        }

        /// <summary>Whether backup payload is encrypted with a user password.</summary>
        public bool PasswordProtectionEnabled
        {
            get => _data.PasswordProtectionEnabled;
            set { if (_data.PasswordProtectionEnabled != value) { _data.PasswordProtectionEnabled = value; Save(); } }
        }

        /// <summary>SHA-256 hash of the user's password (not the password itself).</summary>
        public string PasswordHash
        {
            get => _data.PasswordHash;
            set { if (_data.PasswordHash != value) { _data.PasswordHash = value; Save(); } }
        }

        /// <summary>Restore mode: "last" = always restore latest, "ask" = prompt every time.</summary>
        public string RestoreMode
        {
            get => _data.RestoreMode;
            set { if (_data.RestoreMode != value) { _data.RestoreMode = value; Save(); } }
        }

        /// <summary>Whether the crash diagnostic log file is written to Desktop.</summary>
        public bool EnableCrashLog
        {
            get => _data.EnableCrashLog;
            set { if (_data.EnableCrashLog != value) { _data.EnableCrashLog = value; Save(); } }
        }

        // ── Persistence ─────────────────────────────────────────────

        private static SettingsData Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<SettingsData>(json, JsonOpts)
                           ?? new SettingsData();
                }
            }
            catch { /* corrupt file — use defaults */ }
            return new SettingsData();
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                string json = JsonSerializer.Serialize(_data, JsonOpts);
                File.WriteAllText(SettingsPath, json);
            }
            catch { /* non-critical */ }
        }

        // ── Data container ──────────────────────────────────────────

        private sealed class SettingsData
        {
            public string Language { get; set; } = "en";
            public bool PasswordProtectionEnabled { get; set; }
            public string PasswordHash { get; set; } = string.Empty;
            public string RestoreMode { get; set; } = "ask";
            public bool EnableCrashLog { get; set; } = true;
        }
    }
}