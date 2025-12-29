using System.Text.Json;
using Notes.Modules.Abstractions;
using Notes.Modules.Services;

namespace Notes.Services.IntelliSense;

/// <summary>
/// Service that provides completion data for Monaco editor
/// </summary>
public class CompletionService : ICompletionService, IDisposable
{
    private readonly IModuleManager? _moduleManager;
    private readonly RoslynCompletionService _roslynService;
    private CompletionData? _cachedData;
    private readonly List<ExtensionCompletion> _moduleExtensions = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public CompletionService(IModuleManager? moduleManager = null)
    {
        _moduleManager = moduleManager;
        _roslynService = new RoslynCompletionService();
    }

    public async Task<List<CompletionItem>> GetRoslynCompletionsAsync(
        string code,
        int position,
        CancellationToken cancellationToken = default)
    {
        return await _roslynService.GetCompletionsAsync(code, position, cancellationToken);
    }

    public void Dispose()
    {
        _roslynService?.Dispose();
    }

    public async Task<string> GetCompletionDataJsonAsync()
    {
        var data = await GetCompletionDataAsync();
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public Task<CompletionData> GetCompletionDataAsync()
    {
        if (_cachedData != null)
            return Task.FromResult(_cachedData);

        _cachedData = BuildCompletionData();
        return Task.FromResult(_cachedData);
    }

    public void RegisterModuleCompletions(ExtensionCompletion extension)
    {
        _moduleExtensions.Add(extension);
        _cachedData = null; // Invalidate cache
    }

    public void RefreshModuleCompletions()
    {
        _moduleExtensions.Clear();

        if (_moduleManager == null) return;

        foreach (var ext in _moduleManager.GetScriptExtensions())
        {
            var completion = new ExtensionCompletion
            {
                Prefix = ext.Prefix,
                Methods = ext.GetMethodMetadata().Select(m => new CompletionItem
                {
                    Label = m.Name,
                    Kind = "Method",
                    Detail = $"{m.ReturnType} {m.Name}({string.Join(", ", m.Parameters.Select(p => $"{p.Type} {p.Name}"))})",
                    Documentation = m.Description,
                    InsertText = BuildMethodInsertText(m),
                    IsSnippet = m.Parameters.Count > 0
                }).ToList()
            };

            _moduleExtensions.Add(completion);
        }

        _cachedData = null; // Invalidate cache
    }

    private static string BuildMethodInsertText(ScriptMethodMetadata method)
    {
        if (method.Parameters.Count == 0)
            return $"{method.Name}()";

        var paramSnippets = method.Parameters.Select((p, i) => $"${{{i + 1}:{p.Name}}}");
        return $"{method.Name}({string.Join(", ", paramSnippets)})";
    }

    private CompletionData BuildCompletionData()
    {
        var data = new CompletionData
        {
            Globals = GetScriptGlobals(),
            Types = GetBclTypes(),
            Extensions = _moduleExtensions.ToList(),
            Snippets = GetSnippets()
        };

        return data;
    }

    private static List<CompletionItem> GetScriptGlobals()
    {
        return new List<CompletionItem>
        {
            // Properties
            new() { Label = "NoteContent", Kind = "Property", Detail = "string", Documentation = "Current note content (HTML from rich text editor)" },
            new() { Label = "NotePlainText", Kind = "Property", Detail = "string", Documentation = "Plain text version of current note" },
            new() { Label = "NoteTitle", Kind = "Property", Detail = "string", Documentation = "Current note title" },
            new() { Label = "ClipboardText", Kind = "Property", Detail = "string", Documentation = "Current clipboard text (most recent)" },
            new() { Label = "ClipboardHistory", Kind = "Property", Detail = "List<string>", Documentation = "Recent clipboard history (text items only)" },
            new() { Label = "OutputContent", Kind = "Property", Detail = "string?", Documentation = "Set this to update the note content" },
            new() { Label = "Extensions", Kind = "Property", Detail = "dynamic", Documentation = "Module-provided extension objects (e.g., Extensions.Http)" },

            // Methods
            new() { Label = "Print", Kind = "Method", Detail = "void Print(object? value)", Documentation = "Print any value to script console with smart type detection", InsertText = "Print(${1:value})", IsSnippet = true },
            new() { Label = "StripHtml", Kind = "Method", Detail = "string StripHtml(string html)", Documentation = "Strip HTML tags from content", InsertText = "StripHtml(${1:html})", IsSnippet = true },
            new() { Label = "ToHtml", Kind = "Method", Detail = "string ToHtml(string text)", Documentation = "Wrap plain text in HTML paragraphs", InsertText = "ToHtml(${1:text})", IsSnippet = true },
            new() { Label = "TransformLines", Kind = "Method", Detail = "string TransformLines(string text, Func<string, string> transform)", Documentation = "Transform each line of text", InsertText = "TransformLines(${1:text}, line => ${2:line})", IsSnippet = true },
        };
    }

    private static List<TypeCompletion> GetBclTypes()
    {
        return new List<TypeCompletion>
        {
            // String type
            new()
            {
                Name = "string",
                Kind = "Class",
                Documentation = "Represents text as a sequence of UTF-16 code units",
                StaticMembers = new()
                {
                    new() { Label = "Empty", Kind = "Field", Detail = "string", Documentation = "Empty string constant" },
                    new() { Label = "IsNullOrEmpty", Kind = "Method", Detail = "bool IsNullOrEmpty(string? value)", InsertText = "string.IsNullOrEmpty(${1:value})", IsSnippet = true },
                    new() { Label = "IsNullOrWhiteSpace", Kind = "Method", Detail = "bool IsNullOrWhiteSpace(string? value)", InsertText = "string.IsNullOrWhiteSpace(${1:value})", IsSnippet = true },
                    new() { Label = "Join", Kind = "Method", Detail = "string Join(string separator, IEnumerable<string> values)", InsertText = "string.Join(${1:separator}, ${2:values})", IsSnippet = true },
                    new() { Label = "Concat", Kind = "Method", Detail = "string Concat(params string[] values)", InsertText = "string.Concat(${1:values})", IsSnippet = true },
                    new() { Label = "Format", Kind = "Method", Detail = "string Format(string format, params object[] args)", InsertText = "string.Format(${1:format}, ${2:args})", IsSnippet = true },
                },
                InstanceMembers = new()
                {
                    new() { Label = "Length", Kind = "Property", Detail = "int" },
                    new() { Label = "ToUpper", Kind = "Method", Detail = "string ToUpper()", InsertText = "ToUpper()" },
                    new() { Label = "ToLower", Kind = "Method", Detail = "string ToLower()", InsertText = "ToLower()" },
                    new() { Label = "Trim", Kind = "Method", Detail = "string Trim()", InsertText = "Trim()" },
                    new() { Label = "TrimStart", Kind = "Method", Detail = "string TrimStart()", InsertText = "TrimStart()" },
                    new() { Label = "TrimEnd", Kind = "Method", Detail = "string TrimEnd()", InsertText = "TrimEnd()" },
                    new() { Label = "Split", Kind = "Method", Detail = "string[] Split(char separator)", InsertText = "Split(${1:separator})", IsSnippet = true },
                    new() { Label = "Replace", Kind = "Method", Detail = "string Replace(string oldValue, string newValue)", InsertText = "Replace(${1:oldValue}, ${2:newValue})", IsSnippet = true },
                    new() { Label = "Contains", Kind = "Method", Detail = "bool Contains(string value)", InsertText = "Contains(${1:value})", IsSnippet = true },
                    new() { Label = "StartsWith", Kind = "Method", Detail = "bool StartsWith(string value)", InsertText = "StartsWith(${1:value})", IsSnippet = true },
                    new() { Label = "EndsWith", Kind = "Method", Detail = "bool EndsWith(string value)", InsertText = "EndsWith(${1:value})", IsSnippet = true },
                    new() { Label = "Substring", Kind = "Method", Detail = "string Substring(int startIndex, int? length)", InsertText = "Substring(${1:startIndex})", IsSnippet = true },
                    new() { Label = "IndexOf", Kind = "Method", Detail = "int IndexOf(string value)", InsertText = "IndexOf(${1:value})", IsSnippet = true },
                    new() { Label = "LastIndexOf", Kind = "Method", Detail = "int LastIndexOf(string value)", InsertText = "LastIndexOf(${1:value})", IsSnippet = true },
                    new() { Label = "PadLeft", Kind = "Method", Detail = "string PadLeft(int totalWidth)", InsertText = "PadLeft(${1:totalWidth})", IsSnippet = true },
                    new() { Label = "PadRight", Kind = "Method", Detail = "string PadRight(int totalWidth)", InsertText = "PadRight(${1:totalWidth})", IsSnippet = true },
                    new() { Label = "Insert", Kind = "Method", Detail = "string Insert(int startIndex, string value)", InsertText = "Insert(${1:startIndex}, ${2:value})", IsSnippet = true },
                    new() { Label = "Remove", Kind = "Method", Detail = "string Remove(int startIndex)", InsertText = "Remove(${1:startIndex})", IsSnippet = true },
                }
            },

            // DateTime
            new()
            {
                Name = "DateTime",
                Kind = "Struct",
                Documentation = "Represents an instant in time",
                StaticMembers = new()
                {
                    new() { Label = "Now", Kind = "Property", Detail = "DateTime", Documentation = "Current local date and time" },
                    new() { Label = "UtcNow", Kind = "Property", Detail = "DateTime", Documentation = "Current UTC date and time" },
                    new() { Label = "Today", Kind = "Property", Detail = "DateTime", Documentation = "Current date with time set to midnight" },
                    new() { Label = "MinValue", Kind = "Field", Detail = "DateTime" },
                    new() { Label = "MaxValue", Kind = "Field", Detail = "DateTime" },
                    new() { Label = "Parse", Kind = "Method", Detail = "DateTime Parse(string s)", InsertText = "DateTime.Parse(${1:s})", IsSnippet = true },
                    new() { Label = "TryParse", Kind = "Method", Detail = "bool TryParse(string s, out DateTime result)", InsertText = "DateTime.TryParse(${1:s}, out var ${2:result})", IsSnippet = true },
                },
                InstanceMembers = new()
                {
                    new() { Label = "Year", Kind = "Property", Detail = "int" },
                    new() { Label = "Month", Kind = "Property", Detail = "int" },
                    new() { Label = "Day", Kind = "Property", Detail = "int" },
                    new() { Label = "Hour", Kind = "Property", Detail = "int" },
                    new() { Label = "Minute", Kind = "Property", Detail = "int" },
                    new() { Label = "Second", Kind = "Property", Detail = "int" },
                    new() { Label = "DayOfWeek", Kind = "Property", Detail = "DayOfWeek" },
                    new() { Label = "DayOfYear", Kind = "Property", Detail = "int" },
                    new() { Label = "Date", Kind = "Property", Detail = "DateTime" },
                    new() { Label = "TimeOfDay", Kind = "Property", Detail = "TimeSpan" },
                    new() { Label = "Ticks", Kind = "Property", Detail = "long" },
                    new() { Label = "AddDays", Kind = "Method", Detail = "DateTime AddDays(double value)", InsertText = "AddDays(${1:value})", IsSnippet = true },
                    new() { Label = "AddHours", Kind = "Method", Detail = "DateTime AddHours(double value)", InsertText = "AddHours(${1:value})", IsSnippet = true },
                    new() { Label = "AddMinutes", Kind = "Method", Detail = "DateTime AddMinutes(double value)", InsertText = "AddMinutes(${1:value})", IsSnippet = true },
                    new() { Label = "AddSeconds", Kind = "Method", Detail = "DateTime AddSeconds(double value)", InsertText = "AddSeconds(${1:value})", IsSnippet = true },
                    new() { Label = "AddMonths", Kind = "Method", Detail = "DateTime AddMonths(int months)", InsertText = "AddMonths(${1:months})", IsSnippet = true },
                    new() { Label = "AddYears", Kind = "Method", Detail = "DateTime AddYears(int value)", InsertText = "AddYears(${1:value})", IsSnippet = true },
                    new() { Label = "ToString", Kind = "Method", Detail = "string ToString(string? format)", InsertText = "ToString(${1:format})", IsSnippet = true },
                    new() { Label = "ToShortDateString", Kind = "Method", Detail = "string ToShortDateString()", InsertText = "ToShortDateString()" },
                    new() { Label = "ToLongDateString", Kind = "Method", Detail = "string ToLongDateString()", InsertText = "ToLongDateString()" },
                    new() { Label = "ToShortTimeString", Kind = "Method", Detail = "string ToShortTimeString()", InsertText = "ToShortTimeString()" },
                    new() { Label = "ToUniversalTime", Kind = "Method", Detail = "DateTime ToUniversalTime()", InsertText = "ToUniversalTime()" },
                    new() { Label = "ToLocalTime", Kind = "Method", Detail = "DateTime ToLocalTime()", InsertText = "ToLocalTime()" },
                }
            },

            // TimeSpan
            new()
            {
                Name = "TimeSpan",
                Kind = "Struct",
                Documentation = "Represents a time interval",
                StaticMembers = new()
                {
                    new() { Label = "Zero", Kind = "Field", Detail = "TimeSpan" },
                    new() { Label = "FromDays", Kind = "Method", Detail = "TimeSpan FromDays(double value)", InsertText = "TimeSpan.FromDays(${1:value})", IsSnippet = true },
                    new() { Label = "FromHours", Kind = "Method", Detail = "TimeSpan FromHours(double value)", InsertText = "TimeSpan.FromHours(${1:value})", IsSnippet = true },
                    new() { Label = "FromMinutes", Kind = "Method", Detail = "TimeSpan FromMinutes(double value)", InsertText = "TimeSpan.FromMinutes(${1:value})", IsSnippet = true },
                    new() { Label = "FromSeconds", Kind = "Method", Detail = "TimeSpan FromSeconds(double value)", InsertText = "TimeSpan.FromSeconds(${1:value})", IsSnippet = true },
                    new() { Label = "FromMilliseconds", Kind = "Method", Detail = "TimeSpan FromMilliseconds(double value)", InsertText = "TimeSpan.FromMilliseconds(${1:value})", IsSnippet = true },
                    new() { Label = "Parse", Kind = "Method", Detail = "TimeSpan Parse(string s)", InsertText = "TimeSpan.Parse(${1:s})", IsSnippet = true },
                },
                InstanceMembers = new()
                {
                    new() { Label = "Days", Kind = "Property", Detail = "int" },
                    new() { Label = "Hours", Kind = "Property", Detail = "int" },
                    new() { Label = "Minutes", Kind = "Property", Detail = "int" },
                    new() { Label = "Seconds", Kind = "Property", Detail = "int" },
                    new() { Label = "Milliseconds", Kind = "Property", Detail = "int" },
                    new() { Label = "TotalDays", Kind = "Property", Detail = "double" },
                    new() { Label = "TotalHours", Kind = "Property", Detail = "double" },
                    new() { Label = "TotalMinutes", Kind = "Property", Detail = "double" },
                    new() { Label = "TotalSeconds", Kind = "Property", Detail = "double" },
                    new() { Label = "TotalMilliseconds", Kind = "Property", Detail = "double" },
                    new() { Label = "Add", Kind = "Method", Detail = "TimeSpan Add(TimeSpan ts)", InsertText = "Add(${1:ts})", IsSnippet = true },
                    new() { Label = "Subtract", Kind = "Method", Detail = "TimeSpan Subtract(TimeSpan ts)", InsertText = "Subtract(${1:ts})", IsSnippet = true },
                }
            },

            // Guid
            new()
            {
                Name = "Guid",
                Kind = "Struct",
                Documentation = "Represents a globally unique identifier",
                StaticMembers = new()
                {
                    new() { Label = "NewGuid", Kind = "Method", Detail = "Guid NewGuid()", Documentation = "Generate a new random GUID", InsertText = "Guid.NewGuid()" },
                    new() { Label = "Empty", Kind = "Field", Detail = "Guid", Documentation = "A read-only instance of Guid with all zeros" },
                    new() { Label = "Parse", Kind = "Method", Detail = "Guid Parse(string input)", InsertText = "Guid.Parse(${1:input})", IsSnippet = true },
                    new() { Label = "TryParse", Kind = "Method", Detail = "bool TryParse(string input, out Guid result)", InsertText = "Guid.TryParse(${1:input}, out var ${2:result})", IsSnippet = true },
                },
                InstanceMembers = new()
                {
                    new() { Label = "ToString", Kind = "Method", Detail = "string ToString(string? format)", InsertText = "ToString(${1:format})", IsSnippet = true },
                }
            },

            // Math
            new()
            {
                Name = "Math",
                Kind = "Class",
                Documentation = "Provides constants and static methods for trigonometric, logarithmic, and other common mathematical functions",
                StaticMembers = new()
                {
                    new() { Label = "PI", Kind = "Field", Detail = "double", Documentation = "Represents the ratio of a circle's circumference to its diameter" },
                    new() { Label = "E", Kind = "Field", Detail = "double", Documentation = "Represents the natural logarithmic base" },
                    new() { Label = "Abs", Kind = "Method", Detail = "T Abs<T>(T value)", InsertText = "Math.Abs(${1:value})", IsSnippet = true },
                    new() { Label = "Max", Kind = "Method", Detail = "T Max<T>(T val1, T val2)", InsertText = "Math.Max(${1:val1}, ${2:val2})", IsSnippet = true },
                    new() { Label = "Min", Kind = "Method", Detail = "T Min<T>(T val1, T val2)", InsertText = "Math.Min(${1:val1}, ${2:val2})", IsSnippet = true },
                    new() { Label = "Round", Kind = "Method", Detail = "double Round(double value, int? digits)", InsertText = "Math.Round(${1:value})", IsSnippet = true },
                    new() { Label = "Floor", Kind = "Method", Detail = "double Floor(double d)", InsertText = "Math.Floor(${1:d})", IsSnippet = true },
                    new() { Label = "Ceiling", Kind = "Method", Detail = "double Ceiling(double a)", InsertText = "Math.Ceiling(${1:a})", IsSnippet = true },
                    new() { Label = "Sqrt", Kind = "Method", Detail = "double Sqrt(double d)", InsertText = "Math.Sqrt(${1:d})", IsSnippet = true },
                    new() { Label = "Pow", Kind = "Method", Detail = "double Pow(double x, double y)", InsertText = "Math.Pow(${1:x}, ${2:y})", IsSnippet = true },
                    new() { Label = "Log", Kind = "Method", Detail = "double Log(double d)", InsertText = "Math.Log(${1:d})", IsSnippet = true },
                    new() { Label = "Log10", Kind = "Method", Detail = "double Log10(double d)", InsertText = "Math.Log10(${1:d})", IsSnippet = true },
                    new() { Label = "Sin", Kind = "Method", Detail = "double Sin(double a)", InsertText = "Math.Sin(${1:a})", IsSnippet = true },
                    new() { Label = "Cos", Kind = "Method", Detail = "double Cos(double d)", InsertText = "Math.Cos(${1:d})", IsSnippet = true },
                    new() { Label = "Tan", Kind = "Method", Detail = "double Tan(double a)", InsertText = "Math.Tan(${1:a})", IsSnippet = true },
                    new() { Label = "Clamp", Kind = "Method", Detail = "T Clamp<T>(T value, T min, T max)", InsertText = "Math.Clamp(${1:value}, ${2:min}, ${3:max})", IsSnippet = true },
                }
            },

            // Convert
            new()
            {
                Name = "Convert",
                Kind = "Class",
                Documentation = "Converts a base data type to another base data type",
                StaticMembers = new()
                {
                    new() { Label = "ToInt32", Kind = "Method", Detail = "int ToInt32(object value)", InsertText = "Convert.ToInt32(${1:value})", IsSnippet = true },
                    new() { Label = "ToInt64", Kind = "Method", Detail = "long ToInt64(object value)", InsertText = "Convert.ToInt64(${1:value})", IsSnippet = true },
                    new() { Label = "ToDouble", Kind = "Method", Detail = "double ToDouble(object value)", InsertText = "Convert.ToDouble(${1:value})", IsSnippet = true },
                    new() { Label = "ToBoolean", Kind = "Method", Detail = "bool ToBoolean(object value)", InsertText = "Convert.ToBoolean(${1:value})", IsSnippet = true },
                    new() { Label = "ToString", Kind = "Method", Detail = "string ToString(object value)", InsertText = "Convert.ToString(${1:value})", IsSnippet = true },
                    new() { Label = "ToBase64String", Kind = "Method", Detail = "string ToBase64String(byte[] inArray)", InsertText = "Convert.ToBase64String(${1:bytes})", IsSnippet = true },
                    new() { Label = "FromBase64String", Kind = "Method", Detail = "byte[] FromBase64String(string s)", InsertText = "Convert.FromBase64String(${1:s})", IsSnippet = true },
                }
            },

            // int (Int32)
            new()
            {
                Name = "int",
                Kind = "Struct",
                StaticMembers = new()
                {
                    new() { Label = "Parse", Kind = "Method", Detail = "int Parse(string s)", InsertText = "int.Parse(${1:s})", IsSnippet = true },
                    new() { Label = "TryParse", Kind = "Method", Detail = "bool TryParse(string s, out int result)", InsertText = "int.TryParse(${1:s}, out var ${2:result})", IsSnippet = true },
                    new() { Label = "MaxValue", Kind = "Field", Detail = "int" },
                    new() { Label = "MinValue", Kind = "Field", Detail = "int" },
                },
                InstanceMembers = new()
                {
                    new() { Label = "ToString", Kind = "Method", Detail = "string ToString(string? format)", InsertText = "ToString(${1:format})", IsSnippet = true },
                }
            },

            // double
            new()
            {
                Name = "double",
                Kind = "Struct",
                StaticMembers = new()
                {
                    new() { Label = "Parse", Kind = "Method", Detail = "double Parse(string s)", InsertText = "double.Parse(${1:s})", IsSnippet = true },
                    new() { Label = "TryParse", Kind = "Method", Detail = "bool TryParse(string s, out double result)", InsertText = "double.TryParse(${1:s}, out var ${2:result})", IsSnippet = true },
                    new() { Label = "IsNaN", Kind = "Method", Detail = "bool IsNaN(double d)", InsertText = "double.IsNaN(${1:d})", IsSnippet = true },
                    new() { Label = "IsInfinity", Kind = "Method", Detail = "bool IsInfinity(double d)", InsertText = "double.IsInfinity(${1:d})", IsSnippet = true },
                    new() { Label = "MaxValue", Kind = "Field", Detail = "double" },
                    new() { Label = "MinValue", Kind = "Field", Detail = "double" },
                    new() { Label = "NaN", Kind = "Field", Detail = "double" },
                    new() { Label = "PositiveInfinity", Kind = "Field", Detail = "double" },
                    new() { Label = "NegativeInfinity", Kind = "Field", Detail = "double" },
                },
                InstanceMembers = new()
                {
                    new() { Label = "ToString", Kind = "Method", Detail = "string ToString(string? format)", InsertText = "ToString(${1:format})", IsSnippet = true },
                }
            },

            // List<T>
            new()
            {
                Name = "List",
                Kind = "Class",
                Documentation = "Represents a strongly typed list of objects",
                Constructors = new()
                {
                    new() { Label = "List<T>", Kind = "Constructor", Detail = "new List<T>()", InsertText = "new List<${1:T}>()", IsSnippet = true },
                    new() { Label = "List<T>(capacity)", Kind = "Constructor", Detail = "new List<T>(int capacity)", InsertText = "new List<${1:T}>(${2:capacity})", IsSnippet = true },
                },
                InstanceMembers = new()
                {
                    new() { Label = "Count", Kind = "Property", Detail = "int" },
                    new() { Label = "Add", Kind = "Method", Detail = "void Add(T item)", InsertText = "Add(${1:item})", IsSnippet = true },
                    new() { Label = "AddRange", Kind = "Method", Detail = "void AddRange(IEnumerable<T> collection)", InsertText = "AddRange(${1:collection})", IsSnippet = true },
                    new() { Label = "Remove", Kind = "Method", Detail = "bool Remove(T item)", InsertText = "Remove(${1:item})", IsSnippet = true },
                    new() { Label = "RemoveAt", Kind = "Method", Detail = "void RemoveAt(int index)", InsertText = "RemoveAt(${1:index})", IsSnippet = true },
                    new() { Label = "Clear", Kind = "Method", Detail = "void Clear()", InsertText = "Clear()" },
                    new() { Label = "Contains", Kind = "Method", Detail = "bool Contains(T item)", InsertText = "Contains(${1:item})", IsSnippet = true },
                    new() { Label = "IndexOf", Kind = "Method", Detail = "int IndexOf(T item)", InsertText = "IndexOf(${1:item})", IsSnippet = true },
                    new() { Label = "Insert", Kind = "Method", Detail = "void Insert(int index, T item)", InsertText = "Insert(${1:index}, ${2:item})", IsSnippet = true },
                    new() { Label = "Sort", Kind = "Method", Detail = "void Sort()", InsertText = "Sort()" },
                    new() { Label = "Reverse", Kind = "Method", Detail = "void Reverse()", InsertText = "Reverse()" },
                    new() { Label = "ToArray", Kind = "Method", Detail = "T[] ToArray()", InsertText = "ToArray()" },
                    new() { Label = "Find", Kind = "Method", Detail = "T? Find(Predicate<T> match)", InsertText = "Find(${1:x} => ${2:condition})", IsSnippet = true },
                    new() { Label = "FindAll", Kind = "Method", Detail = "List<T> FindAll(Predicate<T> match)", InsertText = "FindAll(${1:x} => ${2:condition})", IsSnippet = true },
                    new() { Label = "Exists", Kind = "Method", Detail = "bool Exists(Predicate<T> match)", InsertText = "Exists(${1:x} => ${2:condition})", IsSnippet = true },
                    new() { Label = "ForEach", Kind = "Method", Detail = "void ForEach(Action<T> action)", InsertText = "ForEach(${1:x} => ${2:action})", IsSnippet = true },
                }
            },

            // Dictionary<TKey, TValue>
            new()
            {
                Name = "Dictionary",
                Kind = "Class",
                Documentation = "Represents a collection of key/value pairs",
                Constructors = new()
                {
                    new() { Label = "Dictionary<TKey, TValue>", Kind = "Constructor", Detail = "new Dictionary<TKey, TValue>()", InsertText = "new Dictionary<${1:TKey}, ${2:TValue}>()", IsSnippet = true },
                },
                InstanceMembers = new()
                {
                    new() { Label = "Count", Kind = "Property", Detail = "int" },
                    new() { Label = "Keys", Kind = "Property", Detail = "KeyCollection" },
                    new() { Label = "Values", Kind = "Property", Detail = "ValueCollection" },
                    new() { Label = "Add", Kind = "Method", Detail = "void Add(TKey key, TValue value)", InsertText = "Add(${1:key}, ${2:value})", IsSnippet = true },
                    new() { Label = "Remove", Kind = "Method", Detail = "bool Remove(TKey key)", InsertText = "Remove(${1:key})", IsSnippet = true },
                    new() { Label = "Clear", Kind = "Method", Detail = "void Clear()", InsertText = "Clear()" },
                    new() { Label = "ContainsKey", Kind = "Method", Detail = "bool ContainsKey(TKey key)", InsertText = "ContainsKey(${1:key})", IsSnippet = true },
                    new() { Label = "ContainsValue", Kind = "Method", Detail = "bool ContainsValue(TValue value)", InsertText = "ContainsValue(${1:value})", IsSnippet = true },
                    new() { Label = "TryGetValue", Kind = "Method", Detail = "bool TryGetValue(TKey key, out TValue value)", InsertText = "TryGetValue(${1:key}, out var ${2:value})", IsSnippet = true },
                }
            },

            // HashSet<T>
            new()
            {
                Name = "HashSet",
                Kind = "Class",
                Documentation = "Represents a set of unique values",
                Constructors = new()
                {
                    new() { Label = "HashSet<T>", Kind = "Constructor", Detail = "new HashSet<T>()", InsertText = "new HashSet<${1:T}>()", IsSnippet = true },
                },
                InstanceMembers = new()
                {
                    new() { Label = "Count", Kind = "Property", Detail = "int" },
                    new() { Label = "Add", Kind = "Method", Detail = "bool Add(T item)", InsertText = "Add(${1:item})", IsSnippet = true },
                    new() { Label = "Remove", Kind = "Method", Detail = "bool Remove(T item)", InsertText = "Remove(${1:item})", IsSnippet = true },
                    new() { Label = "Clear", Kind = "Method", Detail = "void Clear()", InsertText = "Clear()" },
                    new() { Label = "Contains", Kind = "Method", Detail = "bool Contains(T item)", InsertText = "Contains(${1:item})", IsSnippet = true },
                    new() { Label = "UnionWith", Kind = "Method", Detail = "void UnionWith(IEnumerable<T> other)", InsertText = "UnionWith(${1:other})", IsSnippet = true },
                    new() { Label = "IntersectWith", Kind = "Method", Detail = "void IntersectWith(IEnumerable<T> other)", InsertText = "IntersectWith(${1:other})", IsSnippet = true },
                    new() { Label = "ExceptWith", Kind = "Method", Detail = "void ExceptWith(IEnumerable<T> other)", InsertText = "ExceptWith(${1:other})", IsSnippet = true },
                }
            },

            // StringBuilder
            new()
            {
                Name = "StringBuilder",
                Namespace = "System.Text",
                Kind = "Class",
                Documentation = "Represents a mutable string of characters",
                Constructors = new()
                {
                    new() { Label = "StringBuilder", Kind = "Constructor", Detail = "new StringBuilder()", InsertText = "new StringBuilder()", IsSnippet = false },
                    new() { Label = "StringBuilder(string)", Kind = "Constructor", Detail = "new StringBuilder(string value)", InsertText = "new StringBuilder(${1:value})", IsSnippet = true },
                },
                InstanceMembers = new()
                {
                    new() { Label = "Length", Kind = "Property", Detail = "int" },
                    new() { Label = "Append", Kind = "Method", Detail = "StringBuilder Append(object value)", InsertText = "Append(${1:value})", IsSnippet = true },
                    new() { Label = "AppendLine", Kind = "Method", Detail = "StringBuilder AppendLine(string? value)", InsertText = "AppendLine(${1:value})", IsSnippet = true },
                    new() { Label = "Insert", Kind = "Method", Detail = "StringBuilder Insert(int index, object value)", InsertText = "Insert(${1:index}, ${2:value})", IsSnippet = true },
                    new() { Label = "Remove", Kind = "Method", Detail = "StringBuilder Remove(int startIndex, int length)", InsertText = "Remove(${1:startIndex}, ${2:length})", IsSnippet = true },
                    new() { Label = "Replace", Kind = "Method", Detail = "StringBuilder Replace(string oldValue, string newValue)", InsertText = "Replace(${1:oldValue}, ${2:newValue})", IsSnippet = true },
                    new() { Label = "Clear", Kind = "Method", Detail = "StringBuilder Clear()", InsertText = "Clear()" },
                    new() { Label = "ToString", Kind = "Method", Detail = "string ToString()", InsertText = "ToString()" },
                }
            },

            // Regex
            new()
            {
                Name = "Regex",
                Namespace = "System.Text.RegularExpressions",
                Kind = "Class",
                Documentation = "Represents a regular expression",
                StaticMembers = new()
                {
                    new() { Label = "IsMatch", Kind = "Method", Detail = "bool IsMatch(string input, string pattern)", InsertText = "Regex.IsMatch(${1:input}, ${2:pattern})", IsSnippet = true },
                    new() { Label = "Match", Kind = "Method", Detail = "Match Match(string input, string pattern)", InsertText = "Regex.Match(${1:input}, ${2:pattern})", IsSnippet = true },
                    new() { Label = "Matches", Kind = "Method", Detail = "MatchCollection Matches(string input, string pattern)", InsertText = "Regex.Matches(${1:input}, ${2:pattern})", IsSnippet = true },
                    new() { Label = "Replace", Kind = "Method", Detail = "string Replace(string input, string pattern, string replacement)", InsertText = "Regex.Replace(${1:input}, ${2:pattern}, ${3:replacement})", IsSnippet = true },
                    new() { Label = "Split", Kind = "Method", Detail = "string[] Split(string input, string pattern)", InsertText = "Regex.Split(${1:input}, ${2:pattern})", IsSnippet = true },
                    new() { Label = "Escape", Kind = "Method", Detail = "string Escape(string str)", InsertText = "Regex.Escape(${1:str})", IsSnippet = true },
                },
                Constructors = new()
                {
                    new() { Label = "Regex(pattern)", Kind = "Constructor", Detail = "new Regex(string pattern)", InsertText = "new Regex(${1:pattern})", IsSnippet = true },
                    new() { Label = "Regex(pattern, options)", Kind = "Constructor", Detail = "new Regex(string pattern, RegexOptions options)", InsertText = "new Regex(${1:pattern}, ${2:RegexOptions.None})", IsSnippet = true },
                },
                InstanceMembers = new()
                {
                    new() { Label = "IsMatch", Kind = "Method", Detail = "bool IsMatch(string input)", InsertText = "IsMatch(${1:input})", IsSnippet = true },
                    new() { Label = "Match", Kind = "Method", Detail = "Match Match(string input)", InsertText = "Match(${1:input})", IsSnippet = true },
                    new() { Label = "Matches", Kind = "Method", Detail = "MatchCollection Matches(string input)", InsertText = "Matches(${1:input})", IsSnippet = true },
                    new() { Label = "Replace", Kind = "Method", Detail = "string Replace(string input, string replacement)", InsertText = "Replace(${1:input}, ${2:replacement})", IsSnippet = true },
                    new() { Label = "Split", Kind = "Method", Detail = "string[] Split(string input)", InsertText = "Split(${1:input})", IsSnippet = true },
                }
            },

            // File
            new()
            {
                Name = "File",
                Namespace = "System.IO",
                Kind = "Class",
                Documentation = "Provides static methods for file operations",
                StaticMembers = new()
                {
                    new() { Label = "Exists", Kind = "Method", Detail = "bool Exists(string path)", InsertText = "File.Exists(${1:path})", IsSnippet = true },
                    new() { Label = "ReadAllText", Kind = "Method", Detail = "string ReadAllText(string path)", InsertText = "File.ReadAllText(${1:path})", IsSnippet = true },
                    new() { Label = "ReadAllLines", Kind = "Method", Detail = "string[] ReadAllLines(string path)", InsertText = "File.ReadAllLines(${1:path})", IsSnippet = true },
                    new() { Label = "ReadAllBytes", Kind = "Method", Detail = "byte[] ReadAllBytes(string path)", InsertText = "File.ReadAllBytes(${1:path})", IsSnippet = true },
                    new() { Label = "WriteAllText", Kind = "Method", Detail = "void WriteAllText(string path, string contents)", InsertText = "File.WriteAllText(${1:path}, ${2:contents})", IsSnippet = true },
                    new() { Label = "WriteAllLines", Kind = "Method", Detail = "void WriteAllLines(string path, IEnumerable<string> contents)", InsertText = "File.WriteAllLines(${1:path}, ${2:lines})", IsSnippet = true },
                    new() { Label = "WriteAllBytes", Kind = "Method", Detail = "void WriteAllBytes(string path, byte[] bytes)", InsertText = "File.WriteAllBytes(${1:path}, ${2:bytes})", IsSnippet = true },
                    new() { Label = "AppendAllText", Kind = "Method", Detail = "void AppendAllText(string path, string contents)", InsertText = "File.AppendAllText(${1:path}, ${2:contents})", IsSnippet = true },
                    new() { Label = "Copy", Kind = "Method", Detail = "void Copy(string sourceFileName, string destFileName)", InsertText = "File.Copy(${1:source}, ${2:dest})", IsSnippet = true },
                    new() { Label = "Move", Kind = "Method", Detail = "void Move(string sourceFileName, string destFileName)", InsertText = "File.Move(${1:source}, ${2:dest})", IsSnippet = true },
                    new() { Label = "Delete", Kind = "Method", Detail = "void Delete(string path)", InsertText = "File.Delete(${1:path})", IsSnippet = true },
                    new() { Label = "GetCreationTime", Kind = "Method", Detail = "DateTime GetCreationTime(string path)", InsertText = "File.GetCreationTime(${1:path})", IsSnippet = true },
                    new() { Label = "GetLastWriteTime", Kind = "Method", Detail = "DateTime GetLastWriteTime(string path)", InsertText = "File.GetLastWriteTime(${1:path})", IsSnippet = true },
                }
            },

            // Path
            new()
            {
                Name = "Path",
                Namespace = "System.IO",
                Kind = "Class",
                Documentation = "Performs operations on String instances that contain file or directory path information",
                StaticMembers = new()
                {
                    new() { Label = "Combine", Kind = "Method", Detail = "string Combine(params string[] paths)", InsertText = "Path.Combine(${1:path1}, ${2:path2})", IsSnippet = true },
                    new() { Label = "GetFileName", Kind = "Method", Detail = "string GetFileName(string path)", InsertText = "Path.GetFileName(${1:path})", IsSnippet = true },
                    new() { Label = "GetFileNameWithoutExtension", Kind = "Method", Detail = "string GetFileNameWithoutExtension(string path)", InsertText = "Path.GetFileNameWithoutExtension(${1:path})", IsSnippet = true },
                    new() { Label = "GetExtension", Kind = "Method", Detail = "string GetExtension(string path)", InsertText = "Path.GetExtension(${1:path})", IsSnippet = true },
                    new() { Label = "GetDirectoryName", Kind = "Method", Detail = "string? GetDirectoryName(string path)", InsertText = "Path.GetDirectoryName(${1:path})", IsSnippet = true },
                    new() { Label = "GetFullPath", Kind = "Method", Detail = "string GetFullPath(string path)", InsertText = "Path.GetFullPath(${1:path})", IsSnippet = true },
                    new() { Label = "ChangeExtension", Kind = "Method", Detail = "string ChangeExtension(string path, string extension)", InsertText = "Path.ChangeExtension(${1:path}, ${2:extension})", IsSnippet = true },
                    new() { Label = "GetTempPath", Kind = "Method", Detail = "string GetTempPath()", InsertText = "Path.GetTempPath()" },
                    new() { Label = "GetTempFileName", Kind = "Method", Detail = "string GetTempFileName()", InsertText = "Path.GetTempFileName()" },
                    new() { Label = "HasExtension", Kind = "Method", Detail = "bool HasExtension(string path)", InsertText = "Path.HasExtension(${1:path})", IsSnippet = true },
                    new() { Label = "IsPathRooted", Kind = "Method", Detail = "bool IsPathRooted(string path)", InsertText = "Path.IsPathRooted(${1:path})", IsSnippet = true },
                }
            },

            // Directory
            new()
            {
                Name = "Directory",
                Namespace = "System.IO",
                Kind = "Class",
                Documentation = "Exposes static methods for creating, moving, and enumerating through directories and subdirectories",
                StaticMembers = new()
                {
                    new() { Label = "Exists", Kind = "Method", Detail = "bool Exists(string path)", InsertText = "Directory.Exists(${1:path})", IsSnippet = true },
                    new() { Label = "CreateDirectory", Kind = "Method", Detail = "DirectoryInfo CreateDirectory(string path)", InsertText = "Directory.CreateDirectory(${1:path})", IsSnippet = true },
                    new() { Label = "Delete", Kind = "Method", Detail = "void Delete(string path, bool recursive)", InsertText = "Directory.Delete(${1:path}, ${2:false})", IsSnippet = true },
                    new() { Label = "GetFiles", Kind = "Method", Detail = "string[] GetFiles(string path)", InsertText = "Directory.GetFiles(${1:path})", IsSnippet = true },
                    new() { Label = "GetDirectories", Kind = "Method", Detail = "string[] GetDirectories(string path)", InsertText = "Directory.GetDirectories(${1:path})", IsSnippet = true },
                    new() { Label = "GetCurrentDirectory", Kind = "Method", Detail = "string GetCurrentDirectory()", InsertText = "Directory.GetCurrentDirectory()" },
                    new() { Label = "SetCurrentDirectory", Kind = "Method", Detail = "void SetCurrentDirectory(string path)", InsertText = "Directory.SetCurrentDirectory(${1:path})", IsSnippet = true },
                    new() { Label = "Move", Kind = "Method", Detail = "void Move(string sourceDirName, string destDirName)", InsertText = "Directory.Move(${1:source}, ${2:dest})", IsSnippet = true },
                }
            },

            // JsonSerializer
            new()
            {
                Name = "JsonSerializer",
                Namespace = "System.Text.Json",
                Kind = "Class",
                Documentation = "Provides functionality to serialize/deserialize objects to/from JSON",
                StaticMembers = new()
                {
                    new() { Label = "Serialize", Kind = "Method", Detail = "string Serialize<T>(T value)", InsertText = "JsonSerializer.Serialize(${1:value})", IsSnippet = true },
                    new() { Label = "Deserialize", Kind = "Method", Detail = "T? Deserialize<T>(string json)", InsertText = "JsonSerializer.Deserialize<${1:T}>(${2:json})", IsSnippet = true },
                }
            },

            // HttpClient
            new()
            {
                Name = "HttpClient",
                Namespace = "System.Net.Http",
                Kind = "Class",
                Documentation = "Provides a class for sending HTTP requests and receiving HTTP responses",
                Constructors = new()
                {
                    new() { Label = "HttpClient", Kind = "Constructor", Detail = "new HttpClient()", InsertText = "new HttpClient()" },
                },
                InstanceMembers = new()
                {
                    new() { Label = "BaseAddress", Kind = "Property", Detail = "Uri?" },
                    new() { Label = "DefaultRequestHeaders", Kind = "Property", Detail = "HttpRequestHeaders" },
                    new() { Label = "Timeout", Kind = "Property", Detail = "TimeSpan" },
                    new() { Label = "GetAsync", Kind = "Method", Detail = "Task<HttpResponseMessage> GetAsync(string requestUri)", InsertText = "GetAsync(${1:requestUri})", IsSnippet = true },
                    new() { Label = "GetStringAsync", Kind = "Method", Detail = "Task<string> GetStringAsync(string requestUri)", InsertText = "GetStringAsync(${1:requestUri})", IsSnippet = true },
                    new() { Label = "GetByteArrayAsync", Kind = "Method", Detail = "Task<byte[]> GetByteArrayAsync(string requestUri)", InsertText = "GetByteArrayAsync(${1:requestUri})", IsSnippet = true },
                    new() { Label = "PostAsync", Kind = "Method", Detail = "Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)", InsertText = "PostAsync(${1:requestUri}, ${2:content})", IsSnippet = true },
                    new() { Label = "PutAsync", Kind = "Method", Detail = "Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content)", InsertText = "PutAsync(${1:requestUri}, ${2:content})", IsSnippet = true },
                    new() { Label = "DeleteAsync", Kind = "Method", Detail = "Task<HttpResponseMessage> DeleteAsync(string requestUri)", InsertText = "DeleteAsync(${1:requestUri})", IsSnippet = true },
                    new() { Label = "SendAsync", Kind = "Method", Detail = "Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)", InsertText = "SendAsync(${1:request})", IsSnippet = true },
                }
            },

            // Environment
            new()
            {
                Name = "Environment",
                Kind = "Class",
                Documentation = "Provides information about, and means to manipulate, the current environment and platform",
                StaticMembers = new()
                {
                    new() { Label = "NewLine", Kind = "Property", Detail = "string", Documentation = "Gets the newline string for this environment" },
                    new() { Label = "CurrentDirectory", Kind = "Property", Detail = "string" },
                    new() { Label = "MachineName", Kind = "Property", Detail = "string" },
                    new() { Label = "UserName", Kind = "Property", Detail = "string" },
                    new() { Label = "OSVersion", Kind = "Property", Detail = "OperatingSystem" },
                    new() { Label = "ProcessorCount", Kind = "Property", Detail = "int" },
                    new() { Label = "TickCount", Kind = "Property", Detail = "int" },
                    new() { Label = "GetEnvironmentVariable", Kind = "Method", Detail = "string? GetEnvironmentVariable(string variable)", InsertText = "Environment.GetEnvironmentVariable(${1:variable})", IsSnippet = true },
                    new() { Label = "GetFolderPath", Kind = "Method", Detail = "string GetFolderPath(SpecialFolder folder)", InsertText = "Environment.GetFolderPath(Environment.SpecialFolder.${1:Desktop})", IsSnippet = true },
                    new() { Label = "GetCommandLineArgs", Kind = "Method", Detail = "string[] GetCommandLineArgs()", InsertText = "Environment.GetCommandLineArgs()" },
                }
            },

            // Console
            new()
            {
                Name = "Console",
                Kind = "Class",
                Documentation = "Represents the standard input, output, and error streams (use Print() instead in scripts)",
                StaticMembers = new()
                {
                    new() { Label = "WriteLine", Kind = "Method", Detail = "void WriteLine(object value)", Documentation = "Use Print() instead in scripts", InsertText = "Console.WriteLine(${1:value})", IsSnippet = true },
                    new() { Label = "Write", Kind = "Method", Detail = "void Write(object value)", InsertText = "Console.Write(${1:value})", IsSnippet = true },
                }
            },
        };
    }

    private static List<SnippetCompletion> GetSnippets()
    {
        return new List<SnippetCompletion>
        {
            new() { Label = "var", Detail = "Variable declaration", InsertText = "var ${1:name} = ${2:value};" },
            new() { Label = "foreach", Detail = "foreach loop", InsertText = "foreach (var ${1:item} in ${2:collection})\n{\n\t${3}\n}" },
            new() { Label = "for", Detail = "for loop", InsertText = "for (int ${1:i} = 0; ${1:i} < ${2:length}; ${1:i}++)\n{\n\t${3}\n}" },
            new() { Label = "if", Detail = "if statement", InsertText = "if (${1:condition})\n{\n\t${2}\n}" },
            new() { Label = "ifelse", Detail = "if-else statement", InsertText = "if (${1:condition})\n{\n\t${2}\n}\nelse\n{\n\t${3}\n}" },
            new() { Label = "while", Detail = "while loop", InsertText = "while (${1:condition})\n{\n\t${2}\n}" },
            new() { Label = "do", Detail = "do-while loop", InsertText = "do\n{\n\t${1}\n} while (${2:condition});" },
            new() { Label = "switch", Detail = "switch statement", InsertText = "switch (${1:expression})\n{\n\tcase ${2:value}:\n\t\t${3}\n\t\tbreak;\n\tdefault:\n\t\tbreak;\n}" },
            new() { Label = "try", Detail = "try-catch block", InsertText = "try\n{\n\t${1}\n}\ncatch (Exception ${2:ex})\n{\n\t${3}\n}" },
            new() { Label = "trycf", Detail = "try-catch-finally block", InsertText = "try\n{\n\t${1}\n}\ncatch (Exception ${2:ex})\n{\n\t${3}\n}\nfinally\n{\n\t${4}\n}" },
            new() { Label = "using", Detail = "using statement", InsertText = "using (var ${1:resource} = ${2:expression})\n{\n\t${3}\n}" },
            new() { Label = "async", Detail = "async method body", InsertText = "async ${1:Task} ${2:MethodName}()\n{\n\t${3}\n}" },
            new() { Label = "await", Detail = "await expression", InsertText = "await ${1:expression}" },
            new() { Label = "lambda", Detail = "lambda expression", InsertText = "(${1:x}) => ${2:expression}" },
            new() { Label = "prop", Detail = "auto property", InsertText = "public ${1:string} ${2:Name} { get; set; }" },
            new() { Label = "ctor", Detail = "constructor", InsertText = "public ${1:ClassName}()\n{\n\t${2}\n}" },
        };
    }
}
