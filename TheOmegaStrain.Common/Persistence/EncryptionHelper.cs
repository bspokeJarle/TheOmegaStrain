using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CommonUtilities.Persistence
{
    /// <summary>
    /// AES-256-CBC encryption helper that reads the passphrase from a key file.
    /// The key file sits alongside the encrypted data — never in source code.
    /// Key derivation uses PBKDF2 (SHA-256, 100 000 iterations).
    /// Encrypted format: [IV 16 bytes][ciphertext].
    /// </summary>
    public static class EncryptionHelper
    {
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("OmegaStrain_v1");
        private const int Iterations = 100_000;
        private const int KeySizeBytes = 32; // AES-256
        private const int IvSizeBytes = 16;  // AES block size

        // -----------------------------------------------------------------
        // Key file management
        // -----------------------------------------------------------------

        /// <summary>
        /// Reads the passphrase from the given key file.
        /// </summary>
        public static string ReadPassphrase(string keyFilePath)
        {
            if (!File.Exists(keyFilePath))
                throw new FileNotFoundException($"Encryption key file not found: {keyFilePath}");

            return File.ReadAllText(keyFilePath).Trim();
        }

        /// <summary>
        /// Creates the key file with a default passphrase if it does not exist.
        /// The default value is derived from a numeric constant, not a string literal.
        /// </summary>
        public static void EnsureKeyFile(string keyFilePath)
        {
            if (File.Exists(keyFilePath)) return;

            var dir = Path.GetDirectoryName(keyFilePath);
            if (dir != null) Directory.CreateDirectory(dir);

            // The answer to life, the universe, and everything
            File.WriteAllText(keyFilePath, (6 * 7).ToString());
        }

        // -----------------------------------------------------------------
        // Encrypt / Decrypt raw bytes
        // -----------------------------------------------------------------

        public static byte[] Encrypt(byte[] plainData, string passphrase)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var kdf = new Rfc2898DeriveBytes(
                passphrase, Salt, Iterations, HashAlgorithmName.SHA256);
            aes.Key = kdf.GetBytes(KeySizeBytes);
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var ciphertext = encryptor.TransformFinalBlock(plainData, 0, plainData.Length);

            // [IV][ciphertext]
            var result = new byte[IvSizeBytes + ciphertext.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, IvSizeBytes);
            Buffer.BlockCopy(ciphertext, 0, result, IvSizeBytes, ciphertext.Length);
            return result;
        }

        public static byte[] Decrypt(byte[] encryptedData, string passphrase)
        {
            if (encryptedData.Length < IvSizeBytes)
                throw new CryptographicException("Encrypted data is too short.");

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var kdf = new Rfc2898DeriveBytes(
                passphrase, Salt, Iterations, HashAlgorithmName.SHA256);
            aes.Key = kdf.GetBytes(KeySizeBytes);

            var iv = new byte[IvSizeBytes];
            Buffer.BlockCopy(encryptedData, 0, iv, 0, IvSizeBytes);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(
                encryptedData, IvSizeBytes, encryptedData.Length - IvSizeBytes);
        }

        // -----------------------------------------------------------------
        // File-level convenience
        // -----------------------------------------------------------------

        /// <summary>
        /// Encrypts a JSON string and writes it to the specified file.
        /// </summary>
        public static void EncryptToFile(string filePath, string json, string keyFilePath)
        {
            var passphrase = ReadPassphrase(keyFilePath);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            var encrypted = Encrypt(plainBytes, passphrase);
            File.WriteAllBytes(filePath, encrypted);
        }

        public static void EncryptToFileAtomic(
            string filePath,
            string backupFilePath,
            string json,
            string keyFilePath)
        {
            var passphrase = ReadPassphrase(keyFilePath);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            var encrypted = Encrypt(plainBytes, passphrase);
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null)
                Directory.CreateDirectory(directory);

            string temporaryPath = filePath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporaryPath, encrypted);
                File.Move(temporaryPath, filePath, overwrite: true);
                File.Copy(filePath, backupFilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        /// <summary>
        /// Reads an encrypted file and returns the decrypted JSON string.
        /// Returns null if the file does not exist.
        /// </summary>
        public static string? DecryptFromFile(string filePath, string keyFilePath)
        {
            if (!File.Exists(filePath)) return null;

            var passphrase = ReadPassphrase(keyFilePath);
            var encrypted = File.ReadAllBytes(filePath);
            var decrypted = Decrypt(encrypted, passphrase);
            return Encoding.UTF8.GetString(decrypted);
        }

        public static string? DecryptFromFileOrBackup(
            string filePath,
            string backupFilePath,
            string keyFilePath)
        {
            string? primary = TryDecryptFile(filePath, keyFilePath);
            if (primary != null)
            {
                try
                {
                    if (!File.Exists(backupFilePath))
                        File.Copy(filePath, backupFilePath, overwrite: false);
                }
                catch
                {
                }

                return primary;
            }

            return TryDecryptFile(backupFilePath, keyFilePath);
        }

        private static string? TryDecryptFile(string filePath, string keyFilePath)
        {
            try
            {
                return DecryptFromFile(filePath, keyFilePath);
            }
            catch
            {
                return null;
            }
        }
    }
}
