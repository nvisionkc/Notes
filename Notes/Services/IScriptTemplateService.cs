namespace Notes.Services;

public interface IScriptTemplateService
{
    List<ScriptTemplateCategory> GetCategories();
    List<ScriptTemplate> GetTemplatesInCategory(string categoryId);
    List<ScriptTemplate> GetAllTemplates();
    ScriptTemplate? GetTemplate(string templateId);
    Task<int> ImportTemplateAsync(string templateId);
}

public class ScriptTemplateCategory
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int Order { get; set; }
}

public class ScriptTemplate
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string CategoryId { get; set; }
    public required string Content { get; set; }
    public List<string> Tags { get; set; } = new();
}
