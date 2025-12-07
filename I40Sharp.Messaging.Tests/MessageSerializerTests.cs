using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using BaSyx.Models.AdminShell;
using I40Sharp.Messaging.Core;
using I40Sharp.Messaging.Models;
using Xunit;

namespace I40Sharp.Messaging.Tests;

public class MessageSerializerTests
{
    private readonly MessageSerializer _serializer = new();

    private static string GetProjectRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
    }

    private static string GetOutputFolder()
    {
        var projectRoot = GetProjectRoot();
        var outDir = Path.Combine(projectRoot, "TestOutputs", "MessageSamples");
        Directory.CreateDirectory(outDir);
        return outDir;
    }
    
    [Fact]
    public void Serialize_ValidMessage_ReturnsJson()
    {
        // Arrange
        var message = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.CONSENT)
            .WithConversationId("test-conversation")
            .Build();
        
        // Act
        var json = _serializer.Serialize(message);
        
        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"sender\"", json);
        Assert.Contains("\"receiver\"", json);
        Assert.Contains("P24", json);
        Assert.Contains("RH2", json);
        Assert.Contains("consent", json);
    }
    
    [Fact]
    public void Deserialize_ValidJson_ReturnsMessage()
    {
        // Arrange
        var json = @"{
            ""frame"": {
                ""sender"": {
                    ""identification"": { ""id"": ""P24"" },
                    ""role"": { ""name"": """" }
                },
                ""receiver"": {
                    ""identification"": { ""id"": ""RH2"" },
                    ""role"": { ""name"": """" }
                },
                ""type"": ""consent"",
                ""conversationId"": ""test-123""
            },
            ""interactionElements"": []
        }";
        
        // Act
        var message = _serializer.Deserialize(json);
        
        // Assert
        Assert.NotNull(message);
        Assert.Equal("P24", message.Frame.Sender.Identification.Id);
        Assert.Equal("RH2", message.Frame.Receiver.Identification.Id);
        Assert.Equal("consent", message.Frame.Type);
        Assert.Equal("test-123", message.Frame.ConversationId);
    }
    
    [Fact]
    public void Roundtrip_MessageSerializeDeserialize_PreservesData()
    {
        // Arrange
        var property = I40MessageBuilder.CreateStringProperty("StepTitle", "Load");
        
        var originalMessage = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.REQUIREMENT)
            .WithConversationId("roundtrip-test")
            .AddElement(property)
            .Build();
        
        // Act
        var json = _serializer.Serialize(originalMessage);
        var deserializedMessage = _serializer.Deserialize(json);
        
        // Assert
        Assert.NotNull(deserializedMessage);
        Assert.Equal(originalMessage.Frame.Sender.Identification.Id, 
                     deserializedMessage.Frame.Sender.Identification.Id);
        Assert.Equal(originalMessage.Frame.Receiver.Identification.Id, 
                     deserializedMessage.Frame.Receiver.Identification.Id);
        Assert.Equal(originalMessage.Frame.Type, deserializedMessage.Frame.Type);
        Assert.Equal(originalMessage.Frame.ConversationId, deserializedMessage.Frame.ConversationId);
        Assert.Single(deserializedMessage.InteractionElements);
    }

    [Fact]
    public void Serialize_LogMessage_WritesJsonFile()
    {
        const string ExpectedMessageId = "99cff271-0955-4861-bdc3-872a1d0847c9";
        const string ExpectedTimestamp = "2025-12-07T11:31:36.4550147Z";

        // Arrange
        var messageBuilder = new I40MessageBuilder()
            .From("Module2_Execution_Agent", "ExecutionAgent")
            .To("Broadcast", "System")
            .WithType(I40MessageTypes.INFORM)
            .WithConversationId("log-sample-test")
            .WithMessageId(ExpectedMessageId);
        // Use the LogMessage class from AAS-Sharp-Client to create the interaction element
        var log = new AasSharpClient.Models.Messages.LogMessage(
            AasSharpClient.Models.Messages.LogMessage.LogLevel.Info,
            "Example log",
            "ExecutionAgent",
            "Running");

        // Add the LogMessage (a SubmodelElementCollection) as a single interaction element
        messageBuilder.AddElement(log);
        var message = messageBuilder.Build();

        // Verify: the message should contain exactly one interaction element
        Assert.Single(message.InteractionElements);
        // The outer element must be a SubmodelElementCollection (the Log collection)
        Assert.IsAssignableFrom<SubmodelElementCollection>(message.InteractionElements[0]);

        var logCollection = (SubmodelElementCollection)message.InteractionElements[0];
        // Ensure the collection contains elements (LogLevel, Message, Timestamp, AgentRole, AgentState)
        Assert.NotEmpty(logCollection.Value.Value);

        // Override dynamic values to keep snapshot deterministic
        SetPropertyValue(logCollection, "Timestamp", ExpectedTimestamp);

        // Each inner element must implement IProperty and must have a non-null value
        foreach (var inner in logCollection.Value.Value)
        {
            var prop = Assert.IsAssignableFrom<IProperty>(inner);
            Assert.NotNull(prop.Value);
            Assert.NotNull(prop.Value.Value);
        }

        // Act
        var json = _serializer.Serialize(message);
        var outFile = Path.Combine(GetOutputFolder(), "LogMessage.json");
        File.WriteAllText(outFile, json);

        // Assert
        Assert.True(File.Exists(outFile));
        Assert.Contains("\"LogLevel\"", json);

        using var document = JsonDocument.Parse(json);
        var interactionElements = document.RootElement.GetProperty("interactionElements");
        Assert.Equal(JsonValueKind.Array, interactionElements.ValueKind);
        var logElement = Assert.Single(interactionElements.EnumerateArray());
        Assert.Equal("SubmodelElementCollection", logElement.GetProperty("modelType").GetString());
        var children = logElement.GetProperty("value");
        Assert.Equal(JsonValueKind.Array, children.ValueKind);
        Assert.Equal(5, children.GetArrayLength());

        foreach (var child in children.EnumerateArray())
        {
            Assert.Equal("Property", child.GetProperty("modelType").GetString());
            Assert.True(child.TryGetProperty("value", out var valueElement));
            Assert.Equal(JsonValueKind.String, valueElement.ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(valueElement.GetString()));
        }

        var expectedFile = Path.Combine(GetProjectRoot(), "ExpectedOutputs", "ExpectedLogMessage.json");
        Assert.True(File.Exists(expectedFile));
        var expectedJson = File.ReadAllText(expectedFile);
        AssertJsonEquals(expectedJson, json);
    }
    
    [Fact]
    public void Deserialize_WithSubmodelElementCollection_DeserializesCorrectly()
    {
        // Arrange: build a message with a collection and serialize via our serializer
        var collection = new SubmodelElementCollection("Step0001");
        collection.Value.Value.Add(I40MessageBuilder.CreateStringProperty("StepTitle", "Load"));

        var original = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.CONSENT)
            .WithConversationId("test-123")
            .AddElement(collection)
            .Build();

        var json = _serializer.Serialize(original);
        File.WriteAllText(Path.Combine(GetOutputFolder(), "DebugCollectionMessage.json"), json);
        
        // Act
        var message = _serializer.Deserialize(json);
        
        // Assert
        Assert.NotNull(message);
        Assert.Single(message.InteractionElements);
        Assert.IsType<SubmodelElementCollection>(message.InteractionElements[0]);
        
        var deserializedCollection = (SubmodelElementCollection)message.InteractionElements[0];
        Assert.Equal("Step0001", deserializedCollection.IdShort);
        Assert.Single(deserializedCollection.Value.Value);
        Assert.IsType<Property>(deserializedCollection.Value.Value.First());
    }

    [Fact]
    public void Deserialize_MessageWithSemanticReferences_Succeeds()
    {
        // Arrange: submodel elements with semantic ids produce IReference payloads
        var semanticRef = new Reference(new[] { new Key(KeyType.GlobalReference, "https://example.org/logmessage") })
        {
            Type = ReferenceType.ExternalReference
        };

        var property = I40MessageBuilder.CreateStringProperty("ActionState", "Running");
        property.SemanticId = semanticRef;

        var collection = new SubmodelElementCollection("ActionResponse")
        {
            SemanticId = semanticRef
        };
        collection.Value.Value.Add(property);

        var original = new I40MessageBuilder()
            .From("P24", "ExecutionAgent")
            .To("RH2", "PlanningAgent")
            .WithType(I40MessageTypes.INFORM)
            .WithConversationId("semantic-test")
            .AddElement(collection)
            .Build();

        var json = _serializer.Serialize(original);

        // Act
        var deserialized = _serializer.Deserialize(json);

        // Assert
        Assert.NotNull(deserialized);
        var deserializedCollection = Assert.IsType<SubmodelElementCollection>(deserialized.InteractionElements.Single());
        Assert.NotNull(deserializedCollection.SemanticId);
        Assert.Equal(semanticRef.Type, deserializedCollection.SemanticId.Type);

        var deserializedProperty = Assert.IsType<Property>(deserializedCollection.Value.Value.First());
        Assert.NotNull(deserializedProperty.SemanticId);
        Assert.Equal(semanticRef.Type, deserializedProperty.SemanticId.Type);
    }
    
    [Fact]
    public void IsValidMessage_ValidJson_ReturnsTrue()
    {
        // Arrange
        var json = @"{
            ""frame"": {
                ""sender"": { ""identification"": { ""id"": ""P24"" }, ""role"": {} },
                ""receiver"": { ""identification"": { ""id"": ""RH2"" }, ""role"": {} },
                ""type"": ""consent"",
                ""conversationId"": ""test-123""
            },
            ""interactionElements"": []
        }";
        
        // Act
        var isValid = _serializer.IsValidMessage(json);
        
        // Assert
        Assert.True(isValid);
    }
    
    [Fact]
    public void IsValidMessage_InvalidJson_ReturnsFalse()
    {
        // Arrange
        var json = "{ invalid json }";
        
        // Act
        var isValid = _serializer.IsValidMessage(json);
        
        // Assert
        Assert.False(isValid);
    }

        private static void SetPropertyValue(SubmodelElementCollection collection, string idShort, string value)
        {
            var property = (IProperty)collection.Value.Value.Values.Single(e => e.IdShort == idShort);
            property.SetValueScope(new PropertyValue(new ElementValue<string>(value))).GetAwaiter().GetResult();
        }

        private static void AssertJsonEquals(string expectedJson, string actualJson)
        {
            using var expectedDoc = JsonDocument.Parse(expectedJson);
            using var actualDoc = JsonDocument.Parse(actualJson);

            Assert.True(
                JsonEquals(expectedDoc.RootElement, actualDoc.RootElement),
                $"Serialized LogMessage does not match expected sample.{Environment.NewLine}Expected:{Environment.NewLine}{expectedJson}{Environment.NewLine}Actual:{Environment.NewLine}{actualJson}");
        }

        private static bool JsonEquals(JsonElement expected, JsonElement actual)
        {
            if (expected.ValueKind != actual.ValueKind)
            {
                return false;
            }

            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    var expectedProperties = expected.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                    var actualProperties = actual.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                    if (expectedProperties.Length != actualProperties.Length)
                    {
                        return false;
                    }

                    for (var i = 0; i < expectedProperties.Length; i++)
                    {
                        if (!string.Equals(expectedProperties[i].Name, actualProperties[i].Name, StringComparison.Ordinal))
                        {
                            return false;
                        }

                        if (!JsonEquals(expectedProperties[i].Value, actualProperties[i].Value))
                        {
                            return false;
                        }
                    }

                    return true;
                case JsonValueKind.Array:
                    var expectedItems = expected.EnumerateArray().ToArray();
                    var actualItems = actual.EnumerateArray().ToArray();
                    if (expectedItems.Length != actualItems.Length)
                    {
                        return false;
                    }

                    for (var i = 0; i < expectedItems.Length; i++)
                    {
                        if (!JsonEquals(expectedItems[i], actualItems[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                case JsonValueKind.String:
                    return string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal);
                case JsonValueKind.Number:
                    return expected.GetDouble().Equals(actual.GetDouble());
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return expected.GetBoolean() == actual.GetBoolean();
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return true;
                default:
                    return expected.GetRawText() == actual.GetRawText();
            }
        }
}
