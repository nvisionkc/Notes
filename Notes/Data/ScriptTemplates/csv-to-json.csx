// CSV to JSON
// Converts CSV data to a JSON array of objects

using System.Text.Json;

var lines = NotePlainText.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
if (lines.Count < 2) return "Need header row and at least one data row";

// Simple CSV parsing (handles quoted values)
List<string> ParseCsvLine(string line)
{
    var result = new List<string>();
    var current = new System.Text.StringBuilder();
    bool inQuotes = false;

    foreach (char c in line)
    {
        if (c == '"') inQuotes = !inQuotes;
        else if (c == ',' && !inQuotes)
        {
            result.Add(current.ToString().Trim());
            current.Clear();
        }
        else current.Append(c);
    }
    result.Add(current.ToString().Trim());
    return result;
}

var headers = ParseCsvLine(lines[0]);
var data = lines.Skip(1)
    .Select(line =>
    {
        var values = ParseCsvLine(line);
        var obj = new Dictionary<string, string>();
        for (int i = 0; i < headers.Count && i < values.Count; i++)
            obj[headers[i]] = values[i];
        return obj;
    })
    .ToList();

var options = new JsonSerializerOptions { WriteIndented = true };
return JsonSerializer.Serialize(data, options);
