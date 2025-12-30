// Trim Whitespace
// Trims leading and trailing whitespace from each line

var lines = NotePlainText.Split('\n');
var trimmed = lines.Select(l => l.Trim()).ToArray();
return string.Join("\n", trimmed);
