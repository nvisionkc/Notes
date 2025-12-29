using System.Collections;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Notes.Services.Scripting;

public enum ResultType
{
    None,
    Text,
    Html,
    Collection,
    Object
}

public class ScriptResult
{
    public bool Success { get; set; }
    public string? OutputContent { get; set; }
    public List<ConsoleOutputItem> ConsoleOutput { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public object? ReturnValue { get; set; }

    public ResultType ReturnValueType => DetectResultType(ReturnValue);

    public string? ReturnValueAsText => ReturnValue?.ToString();

    public string? ReturnValueAsJson
    {
        get
        {
            if (ReturnValue == null) return null;
            try
            {
                return JsonSerializer.Serialize(ReturnValue, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    MaxDepth = 10
                });
            }
            catch
            {
                return ReturnValue.ToString();
            }
        }
    }

    public List<Dictionary<string, object?>>? ReturnValueAsTable
    {
        get
        {
            if (ReturnValue == null) return null;
            if (ReturnValue is not IEnumerable enumerable) return null;
            if (ReturnValue is string) return null;

            var result = new List<Dictionary<string, object?>>();
            foreach (var item in enumerable)
            {
                if (item == null) continue;

                var row = new Dictionary<string, object?>();
                var type = item.GetType();

                // Handle primitive types
                if (type.IsPrimitive || item is string || item is decimal)
                {
                    row["Value"] = item;
                }
                // Handle KeyValuePair
                else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    var keyProp = type.GetProperty("Key");
                    var valueProp = type.GetProperty("Value");
                    row["Key"] = keyProp?.GetValue(item);
                    row["Value"] = valueProp?.GetValue(item);
                }
                // Handle complex objects
                else
                {
                    foreach (var prop in type.GetProperties())
                    {
                        try
                        {
                            row[prop.Name] = prop.GetValue(item);
                        }
                        catch
                        {
                            row[prop.Name] = "<error>";
                        }
                    }
                }

                result.Add(row);
            }
            return result;
        }
    }

    public List<string> TableColumns
    {
        get
        {
            var table = ReturnValueAsTable;
            if (table == null || table.Count == 0) return new List<string>();
            return table[0].Keys.ToList();
        }
    }

    private static ResultType DetectResultType(object? value)
    {
        if (value == null) return ResultType.None;

        // Check for string types
        if (value is string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return ResultType.Text;

            // Detect HTML - check for common HTML patterns
            var trimmed = str.TrimStart();
            if (trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(trimmed, @"^<(div|p|span|table|ul|ol|h[1-6]|article|section|header|footer|main|nav|aside|form)\b", RegexOptions.IgnoreCase))
            {
                return ResultType.Html;
            }

            return ResultType.Text;
        }

        // Check for collections (but not string which is IEnumerable<char>)
        if (value is IEnumerable && value is not string)
        {
            return ResultType.Collection;
        }

        // Everything else is an object
        return ResultType.Object;
    }
}
