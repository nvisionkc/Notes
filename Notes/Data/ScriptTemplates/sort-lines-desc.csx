// Sort Lines (Descending)
// Sorts all lines in the current note in reverse alphabetical order

var lines = NotePlainText.Split('\n');
var sorted = lines.OrderByDescending(l => l).ToArray();
return string.Join("\n", sorted);
