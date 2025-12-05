using I40Sharp.Messaging.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace I40Sharp.Messaging.Core;

/// <summary>
/// Serializer für I4.0 Messages mit AAS-konformer JSON-Struktur
/// </summary>
public class MessageSerializer
{
    private readonly JsonSerializerOptions _options;
    
    public MessageSerializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Converters = { new SubmodelElementConverter() }
        };
    }
    
    /// <summary>
    /// Serialisiert eine I40Message zu JSON
    /// </summary>
    public string Serialize(I40Message message)
    {
        return JsonSerializer.Serialize(message, _options);
    }
    
    /// <summary>
    /// Deserialisiert JSON zu einer I40Message
    /// </summary>
    public I40Message? Deserialize(string json)
    {
        var message = JsonSerializer.Deserialize<I40Message>(json, _options);
        if (message != null)
        {
            message.ReceivedAt = DateTime.UtcNow;
        }
        return message;
    }
    
    /// <summary>
    /// Validiert ob ein JSON String eine gültige I4.0 Message ist
    /// </summary>
    public bool IsValidMessage(string json)
    {
        try
        {
            var message = Deserialize(json);
            return message != null 
                   && !string.IsNullOrEmpty(message.Frame.Sender.Identification.Id)
                   && !string.IsNullOrEmpty(message.Frame.Receiver.Identification.Id)
                   && !string.IsNullOrEmpty(message.Frame.Type);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Custom JSON Converter für polymorphe SubmodelElement Hierarchie
/// </summary>
public class SubmodelElementConverter : JsonConverter<SubmodelElement>
{
    public override SubmodelElement? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        
        if (!root.TryGetProperty("modelType", out var modelTypeProperty))
            return null;
        
        var modelType = modelTypeProperty.GetString();
        var json = root.GetRawText();
        
        return modelType switch
        {
            "Property" => JsonSerializer.Deserialize<Property>(json, options),
            "SubmodelElementCollection" => JsonSerializer.Deserialize<SubmodelElementCollection>(json, options),
            "SubmodelElementList" => JsonSerializer.Deserialize<SubmodelElementList>(json, options),
            _ => JsonSerializer.Deserialize<SubmodelElement>(json, options)
        };
    }
    
    public override void Write(Utf8JsonWriter writer, SubmodelElement value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
