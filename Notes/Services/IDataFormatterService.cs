namespace Notes.Services;

/// <summary>
/// Service for detecting, parsing, formatting, and transforming JSON and XML data
/// </summary>
public interface IDataFormatterService
{
    /// <summary>
    /// Detect whether content is JSON, XML, or unknown
    /// </summary>
    DataType DetectType(string content);

    /// <summary>
    /// Format JSON with indentation
    /// </summary>
    string FormatJson(string json);

    /// <summary>
    /// Minify JSON (remove whitespace)
    /// </summary>
    string MinifyJson(string json);

    /// <summary>
    /// Format XML with indentation
    /// </summary>
    string FormatXml(string xml);

    /// <summary>
    /// Minify XML (remove whitespace)
    /// </summary>
    string MinifyXml(string xml);

    /// <summary>
    /// Parse content into a tree structure for visualization
    /// </summary>
    DataNode ParseToTree(string content, DataType type);

    /// <summary>
    /// Validate JSON syntax
    /// </summary>
    DataValidationResult ValidateJson(string json);

    /// <summary>
    /// Validate XML syntax
    /// </summary>
    DataValidationResult ValidateXml(string xml);
}

/// <summary>
/// Type of data content
/// </summary>
public enum DataType
{
    Unknown,
    Json,
    Xml
}

/// <summary>
/// Tree node representing parsed data structure
/// </summary>
public class DataNode
{
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
    public DataNodeType Type { get; set; }
    public List<DataNode> Children { get; set; } = new();
    public string Path { get; set; } = string.Empty;
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// Get display value for the node
    /// </summary>
    public string DisplayValue => Type switch
    {
        DataNodeType.String => $"\"{Value}\"",
        DataNodeType.Number => Value?.ToString() ?? "0",
        DataNodeType.Boolean => Value?.ToString()?.ToLower() ?? "false",
        DataNodeType.Null => "null",
        DataNodeType.Object => $"{{ {Children.Count} properties }}",
        DataNodeType.Array => $"[ {Children.Count} items ]",
        DataNodeType.XmlElement => Children.Count > 0 ? $"<{Key}> ({Children.Count})" : $"<{Key}>",
        DataNodeType.XmlAttribute => $"@{Key}=\"{Value}\"",
        DataNodeType.XmlText => Value?.ToString() ?? string.Empty,
        _ => Value?.ToString() ?? string.Empty
    };
}

/// <summary>
/// Type of data node
/// </summary>
public enum DataNodeType
{
    Object,
    Array,
    String,
    Number,
    Boolean,
    Null,
    XmlElement,
    XmlAttribute,
    XmlText,
    XmlComment
}

/// <summary>
/// Result of data validation
/// </summary>
public class DataValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorLine { get; set; }
    public int? ErrorPosition { get; set; }

    public static DataValidationResult Success() => new() { IsValid = true };
    public static DataValidationResult Failure(string message, int? line = null, int? position = null) => new()
    {
        IsValid = false,
        ErrorMessage = message,
        ErrorLine = line,
        ErrorPosition = position
    };
}
