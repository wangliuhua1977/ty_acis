using System.Security.Cryptography;
using System.Text;

namespace TianyiVision.Acis.Services.Integrations.Ctyun;

internal static class CtyunRsaDecryptor
{
    public static bool IsHexCipherText(string? cipherText)
    {
        return TryDecodeHex(cipherText, out _);
    }

    public static string Decrypt(string cipherText, string privateKey)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new InvalidOperationException("RSA private key is empty.");
        }

        var cipherBytes = DecodeCipherBytes(cipherText);
        using var rsa = RSA.Create();
        ImportPrivateKey(rsa, privateKey);

        var plainBytes = DecryptCipherBytes(rsa, cipherBytes);
        return Encoding.UTF8.GetString(plainBytes).Trim();
    }

    private static byte[] DecodeCipherBytes(string cipherText)
    {
        return TryDecodeHex(cipherText, out var hexBytes)
            ? hexBytes
            : Convert.FromBase64String(NormalizeBase64(cipherText));
    }

    private static void ImportPrivateKey(RSA rsa, string privateKey)
    {
        var keyText = privateKey
            .Replace("-----BEGIN PRIVATE KEY-----", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-----END PRIVATE KEY-----", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-----BEGIN RSA PRIVATE KEY-----", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-----END RSA PRIVATE KEY-----", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();

        var keyBytes = Convert.FromBase64String(NormalizeBase64(keyText));

        try
        {
            rsa.ImportPkcs8PrivateKey(keyBytes, out _);
        }
        catch (CryptographicException)
        {
            rsa.ImportRSAPrivateKey(keyBytes, out _);
        }
    }

    private static byte[] DecryptCipherBytes(RSA rsa, byte[] cipherBytes)
    {
        if (cipherBytes.Length == 0)
        {
            return [];
        }

        var blockSize = rsa.KeySize / 8;
        if (blockSize <= 0)
        {
            throw new InvalidOperationException("RSA key size is invalid.");
        }

        if (cipherBytes.Length % blockSize != 0)
        {
            throw new CryptographicException(
                $"The RSA ciphertext length {cipherBytes.Length} is not a multiple of the key block size {blockSize}.");
        }

        if (cipherBytes.Length == blockSize)
        {
            return rsa.Decrypt(cipherBytes, RSAEncryptionPadding.Pkcs1);
        }

        using var plainStream = new MemoryStream(cipherBytes.Length);
        for (var offset = 0; offset < cipherBytes.Length; offset += blockSize)
        {
            var plainBlock = rsa.Decrypt(cipherBytes.AsSpan(offset, blockSize), RSAEncryptionPadding.Pkcs1);
            plainStream.Write(plainBlock, 0, plainBlock.Length);
        }

        return plainStream.ToArray();
    }

    private static bool TryDecodeHex(string? source, out byte[] bytes)
    {
        bytes = [];
        var normalized = source?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length % 2 != 0)
        {
            return false;
        }

        for (var index = 0; index < normalized.Length; index++)
        {
            if (!Uri.IsHexDigit(normalized[index]))
            {
                return false;
            }
        }

        bytes = Convert.FromHexString(normalized);
        return true;
    }

    private static string NormalizeBase64(string base64)
    {
        var normalized = base64.Trim()
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace(' ', '+');
        var padding = normalized.Length % 4;
        return padding == 0 ? normalized : normalized.PadRight(normalized.Length + (4 - padding), '=');
    }
}
