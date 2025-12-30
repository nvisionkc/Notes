// Lines to JSON Array
// Converts each line into a JSON array of strings

using System.Text.Json;

var lines = NotePlainText
    .Split('\n')
    .Where(l => !string.IsNullOrWhiteSpace(l))
    .Select(l => l.Trim())
    .ToList();

var options = new JsonSerializerOptions { WriteIndented = true };
return JsonSerializer.Serialize(lines, options);
