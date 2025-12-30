// Reverse Lines
// Reverses the order of all lines

var lines = NotePlainText.Split('\n');
var reversed = lines.Reverse().ToArray();
return string.Join("\n", reversed);
