// Extract Emails
// Finds all email addresses in the note content

var pattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";
var matches = System.Text.RegularExpressions.Regex.Matches(NotePlainText, pattern);
var emails = matches.Select(m => m.Value).Distinct().ToList();

Print($"Found {emails.Count} email(s)");
return emails;
