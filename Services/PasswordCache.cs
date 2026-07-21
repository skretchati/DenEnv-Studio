using System;
using System.Security.Cryptography;

namespace DevEnvStudio.Services
{
    /// <summary>
    /// Singleton in-memory cache for the active backup password.
    /// The password is never persisted in cleartext — only its SHA-256 hash lives
    /// in SettingsService. On backup creation, the UI must call SetPassword()
    /// with the plaintext password the user retyped (verified against the hash).
    /// On restore, the UI calls SetPassword() after prompting the user.
    ///
    /// Lifetime: application session only. Cleared on shutdown or via Clear().
    /// </summary>
    public sealed class PasswordCache
    {
        private static readonly System.Lazy<PasswordCache> _instance =
            new(() => new PasswordCache(), true);
        public static PasswordCache Instance => _instance.Value;

        private string? _password;

        private PasswordCache() { }

        /// <summary>
        /// True when a plaintext password has been cached in memory.
        /// This does NOT check whether the password protection setting is enabled,
        /// because existing backups may be double-wrapped and need the password
        /// regardless of the current protection setting.
        /// </summary>
        public bool HasPassword =>
            !string.IsNullOrWhiteSpace(_password);

        /// <summary>Returns the cached plaintext password, or null if not set.</summary>
        public string? GetPassword() => _password;

        /// <summary>
        /// Stores the plaintext password in memory for the current session.
        /// Caller is responsible for verifying it against the stored hash first.
        /// </summary>
        public void SetPassword(string password) => _password = password;

        /// <summary>Clears the cached plaintext password.</summary>
        public void Clear() => _password = null;
    }
}