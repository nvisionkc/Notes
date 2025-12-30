using System.Reflection;

namespace Notes.Services;

public class ScriptTemplateService : IScriptTemplateService
{
    private readonly IScriptingService _scriptingService;
    private readonly List<ScriptTemplateCategory> _categories;
    private readonly List<ScriptTemplate> _templates;

    public ScriptTemplateService(IScriptingService scriptingService)
    {
        _scriptingService = scriptingService;
        _categories = InitializeCategories();
        _templates = LoadTemplatesFromResources();
    }

    private static List<ScriptTemplateCategory> InitializeCategories()
    {
        return new List<ScriptTemplateCategory>
        {
            new() { Id = "text", Name = "Text Manipulation", Description = "Sort, filter, and transform text lines", Icon = "text", Order = 1 },
            new() { Id = "json", Name = "JSON Tools", Description = "Format, parse, and convert JSON data", Icon = "json", Order = 2 },
            new() { Id = "web", Name = "Web & API", Description = "HTTP requests and URL extraction", Icon = "web", Order = 3 },
            new() { Id = "generators", Name = "Generators", Description = "Generate UUIDs, timestamps, random data", Icon = "generate", Order = 4 },
            new() { Id = "conversion", Name = "Data Conversion", Description = "Convert between data formats", Icon = "convert", Order = 5 }
        };
    }

    private List<ScriptTemplate> LoadTemplatesFromResources()
    {
        var templates = new List<ScriptTemplate>();
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePrefix = "Notes.Data.ScriptTemplates.";

        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix) && n.EndsWith(".csx"))
            .ToList();

        foreach (var resourceName in resourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) continue;

                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();

                var fileName = resourceName.Substring(resourcePrefix.Length);
                fileName = fileName.Substring(0, fileName.Length - 4); // Remove .csx

                var template = ParseTemplate(fileName, content);
                if (template != null)
                {
                    templates.Add(template);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load template {resourceName}: {ex.Message}");
            }
        }

        return templates;
    }

    private ScriptTemplate? ParseTemplate(string fileName, string content)
    {
        // Extract name and description from first two comment lines
        var lines = content.Split('\n');
        string name = fileName;
        string description = "";

        if (lines.Length > 0 && lines[0].TrimStart().StartsWith("//"))
        {
            name = lines[0].TrimStart().Substring(2).Trim();
        }
        if (lines.Length > 1 && lines[1].TrimStart().StartsWith("//"))
        {
            description = lines[1].TrimStart().Substring(2).Trim();
        }

        // Determine category based on filename
        var categoryId = fileName switch
        {
            var f when f.StartsWith("sort") || f.StartsWith("dedupe") || f.StartsWith("reverse")
                || f.StartsWith("trim") || f.StartsWith("number") => "text",
            var f when f.StartsWith("format-json") || f.StartsWith("minify-json")
                || f.StartsWith("extract-json") || f.StartsWith("json-to") => "json",
            var f when f.StartsWith("http") || f.StartsWith("extract-url")
                || f.StartsWith("extract-email") => "web",
            var f when f.StartsWith("generate") => "generators",
            var f when f.StartsWith("csv-to") || f.StartsWith("lines-to") => "conversion",
            _ => "text"
        };

        return new ScriptTemplate
        {
            Id = fileName,
            Name = name,
            Description = description,
            CategoryId = categoryId,
            Content = content
        };
    }

    public List<ScriptTemplateCategory> GetCategories() => _categories.OrderBy(c => c.Order).ToList();

    public List<ScriptTemplate> GetTemplatesInCategory(string categoryId) =>
        _templates.Where(t => t.CategoryId == categoryId).OrderBy(t => t.Name).ToList();

    public List<ScriptTemplate> GetAllTemplates() => _templates.OrderBy(t => t.Name).ToList();

    public ScriptTemplate? GetTemplate(string templateId) =>
        _templates.FirstOrDefault(t => t.Id == templateId);

    public async Task<int> ImportTemplateAsync(string templateId)
    {
        var template = GetTemplate(templateId);
        if (template == null)
        {
            throw new ArgumentException($"Template not found: {templateId}");
        }

        // Create a new script with the template content
        var script = await _scriptingService.CreateScriptAsync();
        script.Name = template.Name;
        script.Code = template.Content;
        await _scriptingService.SaveScriptAsync(script);

        return script.Id;
    }
}
