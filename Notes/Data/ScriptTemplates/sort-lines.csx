// Sort Lines
// Sorts all lines in the current note alphabetically

var lines = NotePlainText.Split('\n');
var sorted = lines.OrderBy(l => l).ToArray();
return string.Join("\n", sorted);
