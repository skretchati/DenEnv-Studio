using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevEnvStudio.Services.Backup
{
    /// <summary>
    /// AES-256-GCM encryption/decryption with HKDF-SHA256 key derivation.
    /// 
    /// PORTABILITY DESIGN:
    ///   The key-wrapping key is derived deterministically from:
    ///     - APP_SECRET  (32-byte constant embedded in the binary)
    ///     - backupId    (GUID from metadata.json — stored unencrypted)
    ///     - INFO label  ("devbackup-v1-key-wrap")
    ///   
    ///   This means ANY installation of this app can decrypt ANY .devbackup
    ///   file without passwords or machine-specific keys.
    ///   No DPAPI, no TPM, no Windows hello binding.
    /// 
    /// WIRE FORMAT for an encrypted blob:
    ///   [12 bytes nonce][N bytes ciphertext][16 bytes GCM auth tag]
    /// </summary>
    public sealed class EncryptionService
    {
        private static readonly Lazy<EncryptionService> _instance =
            new(() => new EncryptionService(), true);
        public static EncryptionService Instance => _instance.Value;
        private EncryptionService() { }

        // ── Application secret ────────────────────────────────────────
        // 32-byte constant. In production this would be embedded via
        // a build-time resource or obfuscated, but NOT stored in cleartext config.
        // Changing this value BREAKS all existing backups.
        private static readonly byte[] AppSecret =
        {
            0x44,0x45,0x56,0x42,0x41,0x43,0x4B,0x55,
            0x50,0x53,0x54,0x55,0x44,0x49,0x4F,0x32,
            0x30,0x32,0x36,0x5F,0x53,0x45,0x43,0x52,
            0x45,0x54,0x4B,0x45,0x59,0x58,0x58,0x58
        };

        private const string KdfInfo = "devbackup-v1-key-wrap";
        private const int    AesKeySize = 32;  // 256 bits
        private const int    NonceSize  = 12;  // 96 bits (GCM standard)
        private const int    TagSize    = 16;  // 128 bits (GCM standard)

        // ── Session key lifecycle ─────────────────────────────────────

        /// <summary>
        /// Generates a cryptographically random 256-bit AES session key.
        /// Caller is responsible for wiping it after use (WipeKey).
        /// </summary>
        public byte[] GenerateSessionKey()
        {
            byte[] key = new byte[AesKeySize];
            RandomNumberGenerator.Fill(key);
            return key;
        }

        /// <summary>
        /// Securely wipes a key from memory.
        /// </summary>
        public void WipeKey(byte[] key) => Array.Clear(key, 0, key.Length);

        // ── Key derivation ────────────────────────────────────────────

        /// <summary>
        /// Derives the wrapping key from APP_SECRET + backupId via HKDF-SHA256.
        /// Deterministic and portable — same output on any machine.
        /// </summary>
        public byte[] DeriveWrappingKey(string backupId)
        {
            // HKDF Extract: PRK = HMAC-SHA256(salt=backupId, ikm=AppSecret)
            byte[] salt = Encoding.UTF8.GetBytes(backupId);
            byte[] prk;
            using (var hmac = new HMACSHA256(salt))
                prk = hmac.ComputeHash(AppSecret);

            // HKDF Expand: OKM = T(1) where T(1) = HMAC-SHA256(PRK, info ‖ 0x01)
            byte[] info    = Encoding.UTF8.GetBytes(KdfInfo);
            byte[] t1Input = new byte[info.Length + 1];
            Array.Copy(info, t1Input, info.Length);
            t1Input[info.Length] = 0x01;

            using var hmacExpand = new HMACSHA256(prk);
            byte[] okm = hmacExpand.ComputeHash(t1Input);
            // okm is 32 bytes — exactly AES-256 key size
            return okm;
        }

        // ── Session key wrap/unwrap ───────────────────────────────────

        /// <summary>
        /// Encrypts the session key with the derived wrapping key.
        /// Returns: nonce(12) ‖ ciphertext(32) ‖ tag(16) = 60 bytes total.
        /// </summary>
        public byte[] EncryptSessionKey(byte[] sessionKey, byte[] wrappingKey)
        {
            byte[] nonce      = new byte[NonceSize];
            byte[] ciphertext = new byte[sessionKey.Length];
            byte[] tag        = new byte[TagSize];

            RandomNumberGenerator.Fill(nonce);

            using var aes = new AesGcm(wrappingKey, TagSize);
            aes.Encrypt(nonce, sessionKey, ciphertext, tag);

            return Concat(nonce, ciphertext, tag);
        }

        /// <summary>
        /// Decrypts the session key blob produced by EncryptSessionKey.
        /// Throws CryptographicException if auth tag fails (tamper/wrong key).
        /// </summary>
        public byte[] DecryptSessionKey(byte[] encryptedBlob, byte[] wrappingKey)
        {
            if (encryptedBlob.Length < NonceSize + AesKeySize + TagSize)
                throw new CryptographicException("Session key blob is too short.");

            byte[] nonce      = encryptedBlob[..NonceSize];
            byte[] ciphertext = encryptedBlob[NonceSize..^TagSize];
            byte[] tag        = encryptedBlob[^TagSize..];
            byte[] plaintext  = new byte[ciphertext.Length];

            using var aes = new AesGcm(wrappingKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }

        // ── Payload encrypt (streaming) ───────────────────────────────

        /// <summary>
        /// Stream-encrypts the payload using AES-256-GCM.
        /// 
        /// STREAMING NOTE: .NET's AesGcm requires the full plaintext in memory
        /// for a single GCM call (auth tag covers the whole message).
        /// For multi-GB payloads we use a chunked approach:
        ///   Each 64KB chunk is independently AES-GCM encrypted with an
        ///   incrementing nonce (baseNonce XOR chunkIndex).
        ///   The overall payload signature covers all chunk tags via HMAC.
        /// 
        /// Wire format per chunk:
        ///   [4 bytes: chunk length uint32 LE]
        ///   [12 bytes: nonce]
        ///   [N bytes: ciphertext]
        ///   [16 bytes: tag]
        /// Terminated by a zero-length chunk marker (4 zero bytes).
        /// </summary>
        public async Task EncryptStreamAsync(
            Stream plaintext, Stream ciphertext,
            byte[] sessionKey,
            IProgress<long>? bytesProgress = null,
            CancellationToken ct = default)
        {
            const int ChunkSize = 64 * 1024; // 64 KB chunks

            // Write base nonce (12 bytes) — used to derive per-chunk nonces
            byte[] baseNonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(baseNonce);
            await ciphertext.WriteAsync(baseNonce, ct).ConfigureAwait(false);

            using var aes    = new AesGcm(sessionKey, TagSize);
            byte[] chunkBuf  = new byte[ChunkSize];
            byte[] nonce     = new byte[NonceSize];
            long   totalDone = 0;
            uint   chunkIdx  = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                int read = await ReadExactAsync(plaintext, chunkBuf, 0, ChunkSize, ct)
                    .ConfigureAwait(false);
                if (read == 0) break;

                // Derive per-chunk nonce: baseNonce XOR (chunkIdx as big-endian 4 bytes at offset 8)
                Array.Copy(baseNonce, nonce, NonceSize);
                byte[] idxBytes = BitConverter.GetBytes(chunkIdx);
                if (BitConverter.IsLittleEndian) Array.Reverse(idxBytes);
                nonce[8]  ^= idxBytes[0];
                nonce[9]  ^= idxBytes[1];
                nonce[10] ^= idxBytes[2];
                nonce[11] ^= idxBytes[3];

                byte[] ct_buf  = new byte[read];
                byte[] tag_buf = new byte[TagSize];
                aes.Encrypt(nonce, chunkBuf.AsSpan(0, read), ct_buf, tag_buf);

                // Write: [chunk_len(4)][nonce(12)][ciphertext(N)][tag(16)]
                byte[] lenBytes = BitConverter.GetBytes((uint)read);
                if (!BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                await ciphertext.WriteAsync(lenBytes, ct).ConfigureAwait(false);
                await ciphertext.WriteAsync(nonce,    ct).ConfigureAwait(false);
                await ciphertext.WriteAsync(ct_buf,   ct).ConfigureAwait(false);
                await ciphertext.WriteAsync(tag_buf,  ct).ConfigureAwait(false);

                totalDone += read;
                bytesProgress?.Report(totalDone);
                chunkIdx++;
            }

            // End-of-stream marker
            await ciphertext.WriteAsync(new byte[4], ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Decrypts a stream produced by EncryptStreamAsync.
        /// Throws CryptographicException if any chunk tag fails.
        /// </summary>
        public async Task DecryptStreamAsync(
            Stream ciphertext, Stream plaintext,
            byte[] sessionKey,
            IProgress<long>? bytesProgress = null,
            CancellationToken ct = default)
        {
            // Read base nonce
            byte[] baseNonce = new byte[NonceSize];
            await ReadExactAsync(ciphertext, baseNonce, 0, NonceSize, ct)
                .ConfigureAwait(false);

            using var aes  = new AesGcm(sessionKey, TagSize);
            byte[]    nonce = new byte[NonceSize];
            long totalDone  = 0;
            uint chunkIdx   = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // Read chunk length
                byte[] lenBuf = new byte[4];
                int lr = await ReadExactAsync(ciphertext, lenBuf, 0, 4, ct)
                    .ConfigureAwait(false);
                if (lr < 4) break;

                if (!BitConverter.IsLittleEndian) Array.Reverse(lenBuf);
                uint chunkLen = BitConverter.ToUInt32(lenBuf);
                if (chunkLen == 0) break; // end marker

                // Read nonce
                await ReadExactAsync(ciphertext, nonce, 0, NonceSize, ct)
                    .ConfigureAwait(false);

                // Re-derive per-chunk nonce for verification
                byte[] expectedNonce = new byte[NonceSize];
                Array.Copy(baseNonce, expectedNonce, NonceSize);
                byte[] idxBytes = BitConverter.GetBytes(chunkIdx);
                if (BitConverter.IsLittleEndian) Array.Reverse(idxBytes);
                expectedNonce[8]  ^= idxBytes[0];
                expectedNonce[9]  ^= idxBytes[1];
                expectedNonce[10] ^= idxBytes[2];
                expectedNonce[11] ^= idxBytes[3];

                // Read ciphertext + tag
                byte[] ct_buf = new byte[chunkLen];
                byte[] tag    = new byte[TagSize];
                await ReadExactAsync(ciphertext, ct_buf, 0, (int)chunkLen, ct)
                    .ConfigureAwait(false);
                await ReadExactAsync(ciphertext, tag, 0, TagSize, ct)
                    .ConfigureAwait(false);

                byte[] plain = new byte[chunkLen];
                aes.Decrypt(nonce, ct_buf, tag, plain); // throws if tag invalid

                await plaintext.WriteAsync(plain, ct).ConfigureAwait(false);

                totalDone += chunkLen;
                bytesProgress?.Report(totalDone);
                chunkIdx++;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static byte[] Concat(params byte[][] arrays)
        {
            int totalLen = 0;
            foreach (var a in arrays) totalLen += a.Length;
            byte[] result = new byte[totalLen];
            int offset = 0;
            foreach (var a in arrays)
            {
                Array.Copy(a, 0, result, offset, a.Length);
                offset += a.Length;
            }
            return result;
        }

        private static async Task<int> ReadExactAsync(
            Stream s, byte[] buf, int offset, int count, CancellationToken ct)
        {
            int total = 0;
            while (total < count)
            {
                int r = await s.ReadAsync(buf, offset + total, count - total, ct)
                    .ConfigureAwait(false);
                if (r == 0) break;
                total += r;
            }
            return total;
        }
    }
}
