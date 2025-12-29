using System.Collections;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Notes.Services.Scripting;

/// <summary>
/// Represents a single console output item with type information
/// </summary>
public class ConsoleOutputItem
{
    public object? Value { get; set; }
    public ResultType Type { get; set; }
    public string? TextValue { get; set; }
    public string? JsonValue { get; set; }
    public List<Dictionary<string, object?>>? TableValue { get; set; }
    public List<string>? TableColumns { get; set; }

    public static ConsoleOutputItem Create(object? value)
    {
        var item = new ConsoleOutputItem { Value = value };

        if (value == null)
        {
            item.Type = ResultType.Text;
            item.TextValue = "null";
            return item;
        }

        // Detect type
        if (value is string str)
        {
            var trimmed = str.TrimStart();
            if (trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(trimmed, @"^<(div|p|span|table|ul|ol|h[1-6]|article|section|header|footer|main|nav|aside|form)\b", RegexOptions.IgnoreCase))
            {
                item.Type = ResultType.Html;
                item.TextValue = str;
            }
            else
            {
                item.Type = ResultType.Text;
                item.TextValue = str;
            }
        }
        else if (value is IEnumerable enumerable and not string)
        {
            item.Type = ResultType.Collection;
            item.TableValue = new List<Dictionary<string, object?>>();

            foreach (var element in enumerable)
            {
                if (element == null) continue;

                var row = new Dictionary<string, object?>();
                var type = element.GetType();

                if (type.IsPrimitive || element is string || element is decimal)
                {
                    row["Value"] = element;
                }
                else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    var keyProp = type.GetProperty("Key");
                    var valueProp = type.GetProperty("Value");
                    row["Key"] = keyProp?.GetValue(element);
                    row["Value"] = valueProp?.GetValue(element);
                }
                else
                {
                    foreach (var prop in type.GetProperties())
                    {
                        try { row[prop.Name] = prop.GetValue(element); }
                        catch { row[prop.Name] = "<error>"; }
                    }
                }
                item.TableValue.Add(row);
            }

            item.TableColumns = item.TableValue.Count > 0
                ? item.TableValue[0].Keys.ToList()
                : new List<string>();

            // Also create JSON representation
            try
            {
                item.JsonValue = JsonSerializer.Serialize(value, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    MaxDepth = 10
                });
            }
            catch { item.JsonValue = value.ToString(); }
        }
        else
        {
            // Complex object
            item.Type = ResultType.Object;
            try
            {
                item.JsonValue = JsonSerializer.Serialize(value, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    MaxDepth = 10
                });
            }
            catch { item.JsonValue = value.ToString(); }
            item.TextValue = value.ToString();
        }

        return item;
    }
}

/// <summary>
/// Context available to scripts at runtime. This is what scripts can access.
/// </summary>
public partial class ScriptGlobals
{
    // === Current Note Context ===

    /// <summary>
    /// Current note content (HTML from rich text editor)
    /// </summary>
    public string NoteContent { get; set; } = string.Empty;

    /// <summary>
    /// Plain text version of current note
    /// </summary>
    public string NotePlainText { get; set; } = string.Empty;

    /// <summary>
    /// Current note title
    /// </summary>
    public string NoteTitle { get; set; } = string.Empty;

    // === Clipboard Context ===

    /// <summary>
    /// Current clipboard text (most recent)
    /// </summary>
    public string ClipboardText { get; set; } = string.Empty;

    /// <summary>
    /// Recent clipboard history (text items only)
    /// </summary>
    public List<string> ClipboardHistory { get; set; } = new();

    // === Output ===

    /// <summary>
    /// Set this to update the note content
    /// </summary>
    public string? OutputContent { get; set; }

    /// <summary>
    /// Console output from the script (typed items)
    /// </summary>
    public List<ConsoleOutputItem> ConsoleOutput { get; } = new();

    // === Helper Methods ===

    /// <summary>
    /// Print any value to script console with smart type detection
    /// </summary>
    public void Print(object? value) => ConsoleOutput.Add(ConsoleOutputItem.Create(value));

    /// <summary>
    /// Print formatted string
    /// </summary>
    public void Print(string format, params object[] args)
        => ConsoleOutput.Add(ConsoleOutputItem.Create(string.Format(format, args)));

    /// <summary>
    /// Strip HTML tags from content
    /// </summary>
    public string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        return HtmlTagRegex().Replace(html, " ")
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Trim();
    }

    /// <summary>
    /// Wrap plain text in HTML paragraphs
    /// </summary>
    public string ToHtml(string text)
    {
        if (string.IsNullOrEmpty(text)) return "<p></p>";
        var encoded = System.Net.WebUtility.HtmlEncode(text);
        return "<p>" + encoded.Replace("\n", "</p><p>") + "</p>";
    }

    /// <summary>
    /// Transform each line of text
    /// </summary>
    public string TransformLines(string text, Func<string, string> transform)
    {
        var lines = text.Split('\n');
        return string.Join("\n", lines.Select(transform));
    }

    /// <summary>
    /// Transform each line with index
    /// </summary>
    public string TransformLines(string text, Func<string, int, string> transform)
    {
        var lines = text.Split('\n');
        return string.Join("\n", lines.Select((line, i) => transform(line, i)));
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}
