// Number Lines
// Adds line numbers to each line

var lines = NotePlainText.Split('\n');
var width = lines.Length.ToString().Length;
var numbered = lines.Select((l, i) => $"{(i + 1).ToString().PadLeft(width)}. {l}").ToArray();
return string.Join("\n", numbered);
