// Format JSON
// Pretty-prints JSON with indentation

using System.Text.Json;

var options = new JsonSerializerOptions { WriteIndented = true };
var doc = JsonDocument.Parse(NotePlainText);
return JsonSerializer.Serialize(doc.RootElement, options);
