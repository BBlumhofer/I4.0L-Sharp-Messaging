using System.Text.Json.Serialization;

namespace I40Sharp.Messaging.Models;

/// <summary>
/// Repräsentiert einen AAS Reference Key
/// </summary>
public class Key
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Repräsentiert eine AAS Semantic Reference
/// </summary>
public class SemanticId
{
    [JsonPropertyName("keys")]
    public List<Key> Keys { get; set; } = new();
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ExternalReference";
}

/// <summary>
/// Basis-Klasse für alle AAS SubmodelElements
/// </summary>
public class SubmodelElement
{
    [JsonPropertyName("modelType")]
    public string ModelType { get; set; } = string.Empty;
    
    [JsonPropertyName("idShort")]
    public string IdShort { get; set; } = string.Empty;
    
    [JsonPropertyName("semanticId")]
    public SemanticId? SemanticId { get; set; }
    
    [JsonPropertyName("description")]
    public List<LangStringTextType>? Description { get; set; }
}

/// <summary>
/// Repräsentiert eine AAS Property
/// </summary>
public class Property : SubmodelElement
{
    public Property()
    {
        ModelType = "Property";
    }
    
    [JsonPropertyName("value")]
    public string? Value { get; set; }
    
    [JsonPropertyName("valueType")]
    public string ValueType { get; set; } = "xs:string";
}

/// <summary>
/// Repräsentiert eine AAS SubmodelElementCollection
/// </summary>
public class SubmodelElementCollection : SubmodelElement
{
    public SubmodelElementCollection()
    {
        ModelType = "SubmodelElementCollection";
    }
    
    [JsonPropertyName("value")]
    public List<SubmodelElement> Value { get; set; } = new();
}

/// <summary>
/// Repräsentiert eine AAS SubmodelElementList
/// </summary>
public class SubmodelElementList : SubmodelElement
{
    public SubmodelElementList()
    {
        ModelType = "SubmodelElementList";
    }
    
    [JsonPropertyName("value")]
    public List<SubmodelElement> Value { get; set; } = new();
    
    [JsonPropertyName("typeValueListElement")]
    public string? TypeValueListElement { get; set; }
}

/// <summary>
/// Repräsentiert einen mehrsprachigen Text
/// </summary>
public class LangStringTextType
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "de";
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
