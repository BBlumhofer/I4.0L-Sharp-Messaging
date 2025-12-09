using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using BaSyx.Models.AdminShell;

namespace BaSyx.Models.Extensions
{
    /// <summary>
    /// JSON converter for BaSyx IReference; copied from BaSyx.Models.Extensions to ensure availability with current package version.
    /// </summary>
    public class ReferenceJsonConverter : JsonConverter<IReference>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(IReference).IsAssignableFrom(typeToConvert);
        }

        public override IReference? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;

            ReferenceType referenceType = ReferenceType.Undefined;
            if (root.TryGetProperty("type", out JsonElement typeElement))
            {
                if (typeElement.ValueKind == JsonValueKind.String && Enum.TryParse(typeElement.GetString(), true, out ReferenceType parsedType))
                    referenceType = parsedType;
                else if (typeElement.ValueKind == JsonValueKind.Number && typeElement.TryGetInt32(out int enumValue))
                    referenceType = (ReferenceType)enumValue;
            }

            List<IKey> keys = new List<IKey>();
            if (root.TryGetProperty("keys", out JsonElement keysElement) && keysElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement keyElement in keysElement.EnumerateArray())
                {
                    if (keyElement.ValueKind != JsonValueKind.Object)
                        continue;

                    KeyType keyType = KeyType.Undefined;
                    string? value = null;

                    if (keyElement.TryGetProperty("type", out JsonElement keyTypeElement))
                    {
                        if (keyTypeElement.ValueKind == JsonValueKind.String && Enum.TryParse(keyTypeElement.GetString(), true, out KeyType parsedKeyType))
                            keyType = parsedKeyType;
                        else if (keyTypeElement.ValueKind == JsonValueKind.Number && keyTypeElement.TryGetInt32(out int keyTypeInt))
                            keyType = (KeyType)keyTypeInt;
                    }

                    if (keyElement.TryGetProperty("value", out JsonElement valueElement) && valueElement.ValueKind == JsonValueKind.String)
                        value = valueElement.GetString();

                    if (!string.IsNullOrEmpty(value))
                        keys.Add(new Key(keyType, value));
                }
            }

            Reference reference = new Reference(keys) { Type = referenceType };

            if (root.TryGetProperty("referredSemanticId", out JsonElement semanticElement) && semanticElement.ValueKind != JsonValueKind.Null)
            {
                IReference? referred = semanticElement.Deserialize<IReference>(options);
                reference.ReferredSemanticId = referred;
            }

            return reference;
        }

        public override void Write(Utf8JsonWriter writer, IReference value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            writer.WriteString("type", value.Type.ToString());

            if (value.Keys != null)
            {
                writer.WritePropertyName("keys");
                writer.WriteStartArray();

                foreach (IKey key in value.Keys)
                {
                    if (key == null)
                        continue;

                    writer.WriteStartObject();
                    writer.WriteString("type", key.Type.ToString());
                    if (!string.IsNullOrEmpty(key.Value))
                        writer.WriteString("value", key.Value);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (value.ReferredSemanticId != null)
            {
                writer.WritePropertyName("referredSemanticId");
                JsonSerializer.Serialize(writer, value.ReferredSemanticId, options);
            }

            writer.WriteEndObject();
        }
    }
}
