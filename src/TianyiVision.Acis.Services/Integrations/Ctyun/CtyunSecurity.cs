using System.Security.Cryptography;
using System.Text;

namespace TianyiVision.Acis.Services.Integrations.Ctyun;

internal static class CtyunSecurity
{
    public static string BuildSignature(IReadOnlyList<KeyValuePair<string, string>> requestData, string appSecret)
    {
        return BuildSignature(
            GetRequiredValue(requestData, "appId"),
            GetRequiredValue(requestData, "clientType"),
            GetRequiredValue(requestData, "params"),
            GetRequiredValue(requestData, "timestamp"),
            GetRequiredValue(requestData, "version"),
            appSecret);
    }

    public static string BuildSignature(
        string appId,
        string clientType,
        string encryptedParams,
        string timestamp,
        string version,
        string appSecret)
    {
        var signatureSource = string.Concat(
            appId?.Trim() ?? string.Empty,
            clientType?.Trim() ?? string.Empty,
            encryptedParams?.Trim() ?? string.Empty,
            timestamp?.Trim() ?? string.Empty,
            version?.Trim() ?? string.Empty);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureSource));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string EncryptParams(IReadOnlyList<KeyValuePair<string, string>> parameters, string appSecret)
    {
        var plain = BuildPrivateParameterString(parameters);
        var keyBytes = Encoding.UTF8.GetBytes(appSecret);
        return XXTea.EncryptToHex(Encoding.UTF8.GetBytes(plain), keyBytes);
    }

    public static string BuildPrivateParameterString(IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        return string.Join(
            "&",
            parameters
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .Select(item => $"{item.Key}={item.Value}"));
    }

    private static string GetRequiredValue(IReadOnlyList<KeyValuePair<string, string>> requestData, string key)
    {
        return requestData.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)).Value
               ?? string.Empty;
    }

    private static class XXTea
    {
        public static string EncryptToHex(byte[] plainData, byte[] key)
        {
            return Convert.ToHexString(Encrypt(plainData, key)).ToLowerInvariant();
        }

        private static byte[] Encrypt(byte[] plainData, byte[] key)
        {
            return ToByteArray(Encrypt(ToIntArray(plainData, true), ToIntArray(key, false)), false);
        }

        private static uint[] Encrypt(uint[] v, uint[] k)
        {
            var n = v.Length - 1;
            if (n < 1)
            {
                return v;
            }

            if (k.Length < 4)
            {
                Array.Resize(ref k, 4);
            }

            uint z = v[n];
            uint y = v[0];
            const uint delta = 0x9E3779B9;
            uint sum = 0;
            var q = 6 + 52 / (n + 1);

            while (q-- > 0)
            {
                sum += delta;
                var e = (sum >> 2) & 3;
                for (var p = 0; p < n; p++)
                {
                    y = v[p + 1];
                    v[p] += MX(sum, y, z, p, e, k);
                    z = v[p];
                }

                y = v[0];
                v[n] += MX(sum, y, z, n, e, k);
                z = v[n];
            }

            return v;
        }

        private static uint MX(uint sum, uint y, uint z, int p, uint e, uint[] k)
        {
            return ((z >> 5) ^ (y << 2)) + ((y >> 3) ^ (z << 4)) ^ (sum ^ y) + (k[(p & 3) ^ e] ^ z);
        }

        private static uint[] ToIntArray(byte[] data, bool includeLength)
        {
            var n = (data.Length & 3) == 0 ? data.Length >> 2 : (data.Length >> 2) + 1;
            var result = includeLength ? new uint[n + 1] : new uint[n];
            if (includeLength)
            {
                result[n] = (uint)data.Length;
            }

            for (var i = 0; i < data.Length; i++)
            {
                result[i >> 2] |= (uint)data[i] << ((i & 3) << 3);
            }

            return result;
        }

        private static byte[] ToByteArray(uint[] data, bool includeLength)
        {
            var n = data.Length << 2;
            if (includeLength)
            {
                var m = (int)data[^1];
                if (m > n || m < 0)
                {
                    return [];
                }

                n = m;
            }

            var result = new byte[n];
            for (var i = 0; i < n; i++)
            {
                result[i] = (byte)((data[i >> 2] >> ((i & 3) << 3)) & 0xFF);
            }

            return result;
        }
    }
}
