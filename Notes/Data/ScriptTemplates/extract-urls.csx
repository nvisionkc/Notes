// Extract URLs
// Finds all URLs in the note content

var pattern = @"https?://[^\s<>""']+";
var matches = System.Text.RegularExpressions.Regex.Matches(NotePlainText, pattern);
var urls = matches.Select(m => m.Value).Distinct().ToList();

Print($"Found {urls.Count} URL(s)");
return urls;
