// Minify JSON
// Removes whitespace from JSON to create compact output

using System.Text.Json;

var doc = JsonDocument.Parse(NotePlainText);
return JsonSerializer.Serialize(doc.RootElement);
