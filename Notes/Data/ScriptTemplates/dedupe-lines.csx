// Deduplicate Lines
// Removes duplicate lines while preserving order

var lines = NotePlainText.Split('\n');
var unique = lines.Distinct().ToArray();
Print($"Removed {lines.Length - unique.Length} duplicate(s)");
return string.Join("\n", unique);
