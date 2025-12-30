// Timestamp Converter
// Shows current time in various formats, or converts Unix timestamp from note

var now = DateTime.UtcNow;
var unixSeconds = new DateTimeOffset(now).ToUnixTimeSeconds();
var unixMs = new DateTimeOffset(now).ToUnixTimeMilliseconds();

// Check if note contains a timestamp to convert
var input = NotePlainText.Trim();
if (long.TryParse(input, out var ts))
{
    DateTime converted;
    if (ts > 1000000000000) // milliseconds
        converted = DateTimeOffset.FromUnixTimeMilliseconds(ts).DateTime;
    else // seconds
        converted = DateTimeOffset.FromUnixTimeSeconds(ts).DateTime;

    return new {
        Input = ts,
        UTC = converted.ToString("yyyy-MM-dd HH:mm:ss"),
        Local = converted.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
        ISO8601 = converted.ToString("o")
    };
}

return new {
    Now_UTC = now.ToString("yyyy-MM-dd HH:mm:ss"),
    Now_Local = now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
    Unix_Seconds = unixSeconds,
    Unix_Milliseconds = unixMs,
    ISO8601 = now.ToString("o")
};
