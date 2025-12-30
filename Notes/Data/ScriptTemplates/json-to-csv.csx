// JSON Array to CSV
// Converts a JSON array of objects to CSV format

using System.Text.Json;

var doc = JsonDocument.Parse(NotePlainText);
var array = doc.RootElement.EnumerateArray().ToList();

if (array.Count == 0) return "No data";

// Get all unique keys from all objects
var keys = array
    .SelectMany(item => item.EnumerateObject().Select(p => p.Name))
    .Distinct()
    .ToList();

var sb = new System.Text.StringBuilder();

// Header row
sb.AppendLine(string.Join(",", keys.Select(k => $"\"{k}\"")));

// Data rows
foreach (var item in array)
{
    var values = keys.Select(k =>
    {
        if (item.TryGetProperty(k, out var prop))
        {
            var val = prop.ToString().Replace("\"", "\"\"");
            return $"\"{val}\"";
        }
        return "\"\"";
    });
    sb.AppendLine(string.Join(",", values));
}

return sb.ToString();
