using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Notes.Services;

public class EncoderService : IEncoderService
{
    #region Base64

    public string Base64Encode(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes);
    }

    public string Base64Decode(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Handle URL-safe base64
        var normalized = input
            .Replace('-', '+')
            .Replace('_', '/');

        // Add padding if needed
        switch (normalized.Length % 4)
        {
            case 2: normalized += "=="; break;
            case 3: normalized += "="; break;
        }

        var bytes = Convert.FromBase64String(normalized);
        return Encoding.UTF8.GetString(bytes);
    }

    #endregion

    #region URL

    public string UrlEncode(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return Uri.EscapeDataString(input);
    }

    public string UrlDecode(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return Uri.UnescapeDataString(input);
    }

    #endregion

    #region HTML

    public string HtmlEncode(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return System.Net.WebUtility.HtmlEncode(input);
    }

    public string HtmlDecode(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return System.Net.WebUtility.HtmlDecode(input);
    }

    #endregion

    #region Hex

    public string HexEncode(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public string HexDecode(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Remove common prefixes and whitespace
        var cleaned = input
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("0x", "")
            .Replace("0X", "");

        var bytes = Convert.FromHexString(cleaned);
        return Encoding.UTF8.GetString(bytes);
    }

    #endregion

    #region Unicode

    public string UnicodeEscape(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var sb = new StringBuilder();
        foreach (char c in input)
        {
            if (c > 127)
            {
                sb.Append($"\\u{(int)c:X4}");
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public string UnicodeUnescape(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        return Regex.Replace(input, @"\\u([0-9A-Fa-f]{4})", match =>
        {
            var hex = match.Groups[1].Value;
            var codePoint = Convert.ToInt32(hex, 16);
            return ((char)codePoint).ToString();
        });
    }

    #endregion

    #region JWT

    public JwtDecodeResult DecodeJwt(string token)
    {
        var result = new JwtDecodeResult();

        if (string.IsNullOrWhiteSpace(token))
        {
            result.Error = "Token is empty";
            return result;
        }

        // Clean up the token
        token = token.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token.Substring(7);
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            result.Error = $"Invalid JWT format: expected 3 parts, got {parts.Length}";
            return result;
        }

        try
        {
            // Decode header
            var headerJson = Base64Decode(parts[0]);
            result.HeaderJson = FormatJson(headerJson);

            // Decode payload
            var payloadJson = Base64Decode(parts[1]);
            result.PayloadJson = FormatJson(payloadJson);

            // Store signature (just the base64)
            result.Signature = parts[2];

            // Parse payload for common claims
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("exp", out var expProp) && expProp.TryGetInt64(out var exp))
            {
                result.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(exp).DateTime;
                result.IsExpired = DateTime.UtcNow > result.ExpiresAt;
            }

            if (root.TryGetProperty("iat", out var iatProp) && iatProp.TryGetInt64(out var iat))
            {
                result.IssuedAt = DateTimeOffset.FromUnixTimeSeconds(iat).DateTime;
            }

            if (root.TryGetProperty("sub", out var subProp))
            {
                result.Subject = subProp.GetString();
            }

            if (root.TryGetProperty("iss", out var issProp))
            {
                result.Issuer = issProp.GetString();
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Error = $"Failed to decode JWT: {ex.Message}";
        }

        return result;
    }

    private static string FormatJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }

    #endregion

    #region Hashing

    public string HashMd5(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string HashSha256(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string HashSha512(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA512.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion
}
