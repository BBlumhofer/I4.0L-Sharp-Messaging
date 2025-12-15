using System.Text.Json;
using System.Text.Json.Serialization;
using I40Sharp.Messaging.Models;
using BaSyx.Models.AdminShell;
using BaSyx.Models.Extensions;

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
            // Be tolerant for externally-published JSON payloads.
            // (System.Text.Json is case-sensitive by default.)
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Converters =
            {
                // Tolerate simplified external Property payloads (e.g. mosquitto_pub)
                // while keeping BaSyx polymorphic handling for SubmodelElements.
                new TolerantPropertyConverter(),
                new BaSyx.Models.Extensions.FullSubmodelElementConverter(new BaSyx.Models.Extensions.ConverterOptions()),
                new BaSyx.Models.Extensions.ReferenceJsonConverter(),
                new JsonStringEnumConverter()
            }
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
/// Custom JSON Converter für tolerante Property-Deserialisierung.
/// Unterstützt vereinfachte Payloads wie:
/// { idShort, modelType:"Property", valueType:"string", value:"Assemble" }
/// </summary>
public class TolerantPropertyConverter : JsonConverter<Property>
{
    public override Property? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var jsonObj = root.GetRawText();

        try
        {
            // Only apply tolerance when the payload is clearly a simplified Property with a primitive `value`.
            // Otherwise, fall back to the default BaSyx deserialization.
            if (root.TryGetProperty("value", out var valueEl) && valueEl.ValueKind is JsonValueKind.String
                or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null)
            {
                var idShort = root.TryGetProperty("idShort", out var idShortEl) ? idShortEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(idShort))
                {
                    return JsonSerializer.Deserialize<Property>(jsonObj, CreateFallbackOptions(options));
                }

                object? primitive = null;
                primitive = valueEl.ValueKind switch
                {
                    JsonValueKind.String => valueEl.GetString(),
                    JsonValueKind.Number => valueEl.TryGetInt64(out var l) ? l : (valueEl.TryGetDouble(out var d) ? d : null),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => null
                };

                var property = new Property(idShort, new DataType(DataObjectType.String));

                // Try to set kind if provided
                if (root.TryGetProperty("kind", out var kindEl) && kindEl.ValueKind == JsonValueKind.String)
                {
                    var k = kindEl.GetString();
                    if (string.Equals(k, "Instance", StringComparison.OrdinalIgnoreCase))
                        property.Kind = ModelingKind.Instance;
                    else if (string.Equals(k, "Template", StringComparison.OrdinalIgnoreCase))
                        property.Kind = ModelingKind.Template;
                }

                if (primitive is not null)
                {
                    property.Value = new PropertyValue<string>(primitive.ToString() ?? string.Empty);
                    return property;
                }
            }

            return JsonSerializer.Deserialize<Property>(jsonObj, CreateFallbackOptions(options));
        }
        catch
        {
            return JsonSerializer.Deserialize<Property>(jsonObj, CreateFallbackOptions(options));
        }
    }

    public override void Write(Utf8JsonWriter writer, Property value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, value.GetType(), CreateFallbackOptions(options));
    }

    private static JsonSerializerOptions CreateFallbackOptions(JsonSerializerOptions options)
    {
        // Avoid infinite recursion when we call JsonSerializer.Deserialize<Property>(..., options)
        // from inside this converter.
        var clone = new JsonSerializerOptions(options);
        for (var i = clone.Converters.Count - 1; i >= 0; i--)
        {
            if (clone.Converters[i] is TolerantPropertyConverter)
                clone.Converters.RemoveAt(i);
        }

        return clone;
    }
}
