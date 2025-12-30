namespace Notes.Services;

public interface IEncoderService
{
    // Base64
    string Base64Encode(string input);
    string Base64Decode(string input);

    // URL
    string UrlEncode(string input);
    string UrlDecode(string input);

    // HTML
    string HtmlEncode(string input);
    string HtmlDecode(string input);

    // Hex
    string HexEncode(string input);
    string HexDecode(string input);

    // Unicode
    string UnicodeEscape(string input);
    string UnicodeUnescape(string input);

    // JWT
    JwtDecodeResult DecodeJwt(string token);

    // Hashing
    string HashMd5(string input);
    string HashSha256(string input);
    string HashSha512(string input);
}

public class JwtDecodeResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? HeaderJson { get; set; }
    public string? PayloadJson { get; set; }
    public string? Signature { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public string? Subject { get; set; }
    public string? Issuer { get; set; }
}
