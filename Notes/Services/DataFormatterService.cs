using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace Notes.Services;

/// <summary>
/// Implementation of data formatting service for JSON and XML
/// </summary>
public class DataFormatterService : IDataFormatterService
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions MinifiedOptions = new()
    {
        WriteIndented = false
    };

    public DataType DetectType(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return DataType.Unknown;

        var trimmed = content.TrimStart();

        // Check for JSON (starts with { or [)
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            try
            {
                JsonDocument.Parse(content);
                return DataType.Json;
            }
            catch
            {
                // Not valid JSON, continue checking
            }
        }

        // Check for XML (starts with < and contains valid XML)
        if (trimmed.StartsWith('<'))
        {
            try
            {
                XDocument.Parse(content);
                return DataType.Xml;
            }
            catch
            {
                // Not valid XML
            }
        }

        return DataType.Unknown;
    }

    public string FormatJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, IndentedOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Invalid JSON: {ex.Message}", ex);
        }
    }

    public string MinifyJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, MinifiedOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Invalid JSON: {ex.Message}", ex);
        }
    }

    public string FormatXml(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = !xml.TrimStart().StartsWith("<?xml")
            };

            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, settings))
            {
                doc.Save(writer);
            }
            return sb.ToString();
        }
        catch (XmlException ex)
        {
            throw new FormatException($"Invalid XML: {ex.Message}", ex);
        }
    }

    public string MinifyXml(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var settings = new XmlWriterSettings
            {
                Indent = false,
                NewLineHandling = NewLineHandling.None,
                OmitXmlDeclaration = !xml.TrimStart().StartsWith("<?xml")
            };

            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, settings))
            {
                doc.Save(writer);
            }
            return sb.ToString();
        }
        catch (XmlException ex)
        {
            throw new FormatException($"Invalid XML: {ex.Message}", ex);
        }
    }

    public DataNode ParseToTree(string content, DataType type)
    {
        return type switch
        {
            DataType.Json => ParseJsonToTree(content),
            DataType.Xml => ParseXmlToTree(content),
            _ => new DataNode { Key = "root", Type = DataNodeType.String, Value = content }
        };
    }

    private DataNode ParseJsonToTree(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return ParseJsonNode(node, "root", "$");
        }
        catch (JsonException ex)
        {
            return new DataNode
            {
                Key = "error",
                Type = DataNodeType.String,
                Value = $"Parse error: {ex.Message}"
            };
        }
    }

    private DataNode ParseJsonNode(JsonNode? node, string key, string path)
    {
        if (node == null)
        {
            return new DataNode
            {
                Key = key,
                Type = DataNodeType.Null,
                Value = null,
                Path = path
            };
        }

        switch (node)
        {
            case JsonObject obj:
                var objectNode = new DataNode
                {
                    Key = key,
                    Type = DataNodeType.Object,
                    Path = path
                };
                foreach (var prop in obj)
                {
                    var childPath = $"{path}.{prop.Key}";
                    objectNode.Children.Add(ParseJsonNode(prop.Value, prop.Key, childPath));
                }
                return objectNode;

            case JsonArray arr:
                var arrayNode = new DataNode
                {
                    Key = key,
                    Type = DataNodeType.Array,
                    Path = path
                };
                for (int i = 0; i < arr.Count; i++)
                {
                    var childPath = $"{path}[{i}]";
                    arrayNode.Children.Add(ParseJsonNode(arr[i], $"[{i}]", childPath));
                }
                return arrayNode;

            case JsonValue val:
                var element = val.GetValue<JsonElement>();
                return element.ValueKind switch
                {
                    JsonValueKind.String => new DataNode
                    {
                        Key = key,
                        Type = DataNodeType.String,
                        Value = element.GetString(),
                        Path = path
                    },
                    JsonValueKind.Number => new DataNode
                    {
                        Key = key,
                        Type = DataNodeType.Number,
                        Value = element.GetRawText(),
                        Path = path
                    },
                    JsonValueKind.True or JsonValueKind.False => new DataNode
                    {
                        Key = key,
                        Type = DataNodeType.Boolean,
                        Value = element.GetBoolean(),
                        Path = path
                    },
                    JsonValueKind.Null => new DataNode
                    {
                        Key = key,
                        Type = DataNodeType.Null,
                        Value = null,
                        Path = path
                    },
                    _ => new DataNode
                    {
                        Key = key,
                        Type = DataNodeType.String,
                        Value = element.GetRawText(),
                        Path = path
                    }
                };

            default:
                return new DataNode
                {
                    Key = key,
                    Type = DataNodeType.String,
                    Value = node.ToJsonString(),
                    Path = path
                };
        }
    }

    private DataNode ParseXmlToTree(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            if (doc.Root == null)
            {
                return new DataNode { Key = "root", Type = DataNodeType.XmlElement };
            }
            return ParseXmlElement(doc.Root, "/");
        }
        catch (XmlException ex)
        {
            return new DataNode
            {
                Key = "error",
                Type = DataNodeType.String,
                Value = $"Parse error: {ex.Message}"
            };
        }
    }

    private DataNode ParseXmlElement(XElement element, string parentPath)
    {
        var path = $"{parentPath}{element.Name.LocalName}";
        var node = new DataNode
        {
            Key = element.Name.LocalName,
            Type = DataNodeType.XmlElement,
            Path = path
        };

        // Add attributes
        foreach (var attr in element.Attributes())
        {
            node.Children.Add(new DataNode
            {
                Key = attr.Name.LocalName,
                Type = DataNodeType.XmlAttribute,
                Value = attr.Value,
                Path = $"{path}/@{attr.Name.LocalName}"
            });
        }

        // Add child elements and text nodes
        foreach (var child in element.Nodes())
        {
            switch (child)
            {
                case XElement childElement:
                    node.Children.Add(ParseXmlElement(childElement, $"{path}/"));
                    break;
                case XText text when !string.IsNullOrWhiteSpace(text.Value):
                    node.Children.Add(new DataNode
                    {
                        Key = "#text",
                        Type = DataNodeType.XmlText,
                        Value = text.Value.Trim(),
                        Path = $"{path}/text()"
                    });
                    break;
                case XComment comment:
                    node.Children.Add(new DataNode
                    {
                        Key = "#comment",
                        Type = DataNodeType.XmlComment,
                        Value = comment.Value,
                        Path = $"{path}/comment()"
                    });
                    break;
            }
        }

        // If only text content, set as value
        if (node.Children.Count == 1 && node.Children[0].Type == DataNodeType.XmlText)
        {
            node.Value = node.Children[0].Value;
            node.Children.Clear();
        }

        return node;
    }

    public DataValidationResult ValidateJson(string json)
    {
        try
        {
            JsonDocument.Parse(json);
            return DataValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return DataValidationResult.Failure(
                ex.Message,
                (int?)ex.LineNumber,
                (int?)ex.BytePositionInLine
            );
        }
    }

    public DataValidationResult ValidateXml(string xml)
    {
        try
        {
            XDocument.Parse(xml);
            return DataValidationResult.Success();
        }
        catch (XmlException ex)
        {
            return DataValidationResult.Failure(
                ex.Message,
                ex.LineNumber,
                ex.LinePosition
            );
        }
    }
}
