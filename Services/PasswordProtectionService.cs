using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DevEnvStudio.Services
{
    /// <summary>
    /// Password-based additional encryption layer.
    /// The user password is hashed with SHA-256; the hash is stored in settings.
    /// The password itself is used to derive an extra wrapping key (PBKDF2-SHA256),
    /// which DOUBLE-wraps the session key: AppSecret → WrappingKey(A) → PasswordKey(B) → SessionKey.
    /// </summary>
    public sealed class PasswordProtectionService
    {
        private static readonly Lazy<PasswordProtectionService> _instance =
            new(() => new PasswordProtectionService(), true);
        public static PasswordProtectionService Instance => _instance.Value;

        private static readonly byte[] Pbkdf2Salt =
            Encoding.UTF8.GetBytes("DevEnvStudio-PBKDF2-Salt-v1");

        private const int Pbkdf2Iterations = 200_000;
        private const int AesKeySize       = 32;

        // ── Password hash (for verification only, never stored raw) ──

        /// <summary>
        /// Returns the SHA-256 hash of the given password.
        /// This hash is stored in SettingsService.PasswordHash.
        /// </summary>
        public string HashPassword(string password)
        {
            byte[] raw = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(raw).ToLowerInvariant();
        }

        /// <summary>
        /// Returns true if the given password matches the stored hash.
        /// </summary>
        public bool VerifyPassword(string password)
        {
            return HashPassword(password) == SettingsService.Instance.PasswordHash;
        }

        // ── Password-derived key for double-wrapping ──────────────────

        /// <summary>
        /// Derives a 256-bit key from the user password using PBKDF2-SHA256.
        /// Used for double-wrapping the session key.
        /// </summary>
        public byte[] DerivePasswordKey(string password)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                Pbkdf2Salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                AesKeySize);
        }

        public void WipeKey(byte[] key) => Array.Clear(key, 0, key.Length);

        // ── Double-wrapped session key ────────────────────────────────
        // Layer A: AppSecret → wrappingKey (via HKDF, existing)
        // Layer B: password  → passwordKey (via PBKDF2, this service)
        // Final:   SessionKey wrapped first with wrappingKey, then with passwordKey

        /// <summary>
        /// Applies the second layer of wrapping on an already-app-wrapped session key blob.
        /// Input:  appWrappedBlob = nonce(12) ‖ AES-GCM(wrappingKey, sessionKey) ‖ tag(16)
        /// Output: nonce2(12) ‖ AES-GCM(passwordKey, appWrappedBlob) ‖ tag2(16)
        /// </summary>
        public byte[] DoubleWrapKey(byte[] appWrappedBlob, string password)
        {
            byte[] passwordKey = DerivePasswordKey(password);
            try
            {
                byte[] nonce      = new byte[12];
                byte[] ciphertext = new byte[appWrappedBlob.Length];
                byte[] tag        = new byte[16];

                RandomNumberGenerator.Fill(nonce);

                using var aes = new AesGcm(passwordKey, 16);
                aes.Encrypt(nonce, appWrappedBlob, ciphertext, tag);

                byte[] result = new byte[12 + ciphertext.Length + 16];
                Array.Copy(nonce,        0, result, 0,                         12);
                Array.Copy(ciphertext,   0, result, 12,                        ciphertext.Length);
                Array.Copy(tag,          0, result, 12 + ciphertext.Length,    16);
                return result;
            }
            finally
            {
                WipeKey(passwordKey);
            }
        }

        /// <summary>
        /// Removes the password layer from a double-wrapped key blob,
        /// returning the app-wrapped blob.
        /// </summary>
        public byte[] UnwrapPasswordLayer(byte[] doubleWrappedBlob, string password)
        {
            if (doubleWrappedBlob.Length < 12 + 16)
                throw new CryptographicException("Double-wrapped blob too short.");

            byte[] passwordKey = DerivePasswordKey(password);
            try
            {
                byte[] nonce      = new byte[12];
                byte[] ciphertext = new byte[doubleWrappedBlob.Length - 12 - 16];
                byte[] tag        = new byte[16];

                Array.Copy(doubleWrappedBlob, 0, nonce, 0, 12);
                Array.Copy(doubleWrappedBlob, 12, ciphertext, 0, ciphertext.Length);
                Array.Copy(doubleWrappedBlob, 12 + ciphertext.Length, tag, 0, 16);

                byte[] plaintext = new byte[ciphertext.Length];
                using var aes = new AesGcm(passwordKey, 16);
                aes.Decrypt(nonce, ciphertext, tag, plaintext);

                return plaintext;
            }
            finally
            {
                WipeKey(passwordKey);
            }
        }
    }
}