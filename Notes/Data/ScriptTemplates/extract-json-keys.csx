// Extract JSON Keys
// Lists all top-level keys from a JSON object

using System.Text.Json;

var doc = JsonDocument.Parse(NotePlainText);
var keys = doc.RootElement.EnumerateObject().Select(p => p.Name);
return keys;
