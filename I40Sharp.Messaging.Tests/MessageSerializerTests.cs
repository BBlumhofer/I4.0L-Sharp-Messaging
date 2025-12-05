using I40Sharp.Messaging.Core;
using I40Sharp.Messaging.Models;
using Xunit;

namespace I40Sharp.Messaging.Tests;

public class MessageSerializerTests
{
    private readonly MessageSerializer _serializer = new();
    
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
        var property = new Property
        {
            IdShort = "StepTitle",
            Value = "Load",
            ValueType = "xs:string"
        };
        
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
    public void Deserialize_WithSubmodelElementCollection_DeserializesCorrectly()
    {
        // Arrange
        var json = @"{
            ""frame"": {
                ""sender"": { ""identification"": { ""id"": ""P24"" }, ""role"": {} },
                ""receiver"": { ""identification"": { ""id"": ""RH2"" }, ""role"": {} },
                ""type"": ""consent"",
                ""conversationId"": ""test-123""
            },
            ""interactionElements"": [
                {
                    ""modelType"": ""SubmodelElementCollection"",
                    ""idShort"": ""Step0001"",
                    ""value"": [
                        {
                            ""modelType"": ""Property"",
                            ""idShort"": ""StepTitle"",
                            ""value"": ""Load"",
                            ""valueType"": ""xs:string""
                        }
                    ]
                }
            ]
        }";
        
        // Act
        var message = _serializer.Deserialize(json);
        
        // Assert
        Assert.NotNull(message);
        Assert.Single(message.InteractionElements);
        Assert.IsType<SubmodelElementCollection>(message.InteractionElements[0]);
        
        var collection = (SubmodelElementCollection)message.InteractionElements[0];
        Assert.Equal("Step0001", collection.IdShort);
        Assert.Single(collection.Value);
        Assert.IsType<Property>(collection.Value[0]);
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
}
