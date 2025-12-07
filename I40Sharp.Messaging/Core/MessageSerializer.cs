using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using BaSyx.Models.AdminShell;
using BaSyx.Models.Extensions;
using I40Sharp.Messaging.Models;

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
        };
        // Enable polymorphic AAS element (de)serialization
        _options.Converters.Add(new FullSubmodelElementConverter(new ConverterOptions()));
        _options.Converters.Add(new SubmodelElementConverter());
        _options.Converters.Add(new KeyInterfaceConverter());
        _options.Converters.Add(new ReferenceInterfaceConverter());
        _options.Converters.Add(new JsonStringEnumConverter());
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
/// Adapter that reuses the BaSyx FullSubmodelElementConverter for concrete SubmodelElement types.
/// </summary>
public class SubmodelElementConverter : JsonConverter<SubmodelElement>
{
    private readonly FullSubmodelElementConverter _innerConverter;

    public SubmodelElementConverter()
    {
        _innerConverter = new FullSubmodelElementConverter(new ConverterOptions());
    }

    public override SubmodelElement? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _innerConverter.Read(ref reader, typeof(ISubmodelElement), options) as SubmodelElement;
    }

    public override void Write(Utf8JsonWriter writer, SubmodelElement value, JsonSerializerOptions options)
    {
        _innerConverter.Write(writer, value, options);
    }
}

/// <summary>
/// Ensures BaSyx references stored as interfaces can be materialized.
/// </summary>
public class ReferenceInterfaceConverter : JsonConverter<IReference>
{
    public override IReference? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Reference>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, IReference value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}

/// <summary>
/// Allows BaSyx key interfaces to be deserialized.
/// </summary>
public class KeyInterfaceConverter : JsonConverter<IKey>
{
    public override IKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Key>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, IKey value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
