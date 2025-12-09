using I40Sharp.Messaging.Models;
using I40Sharp.Messaging.Core;
using BaSyx.Models.AdminShell;
using Xunit;

namespace I40Sharp.Messaging.Tests;

public class MessageBuilderTests
{
    [Fact]
    public void Build_ValidMessage_ReturnsMessage()
    {
        // Arrange
        var builder = new I40MessageBuilder();
        
        // Act
        var message = builder
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.CALL_FOR_PROPOSAL)
            .Build();
        
        // Assert
        Assert.NotNull(message);
        Assert.Equal("P24", message.Frame.Sender.Identification.Id);
        Assert.Equal("RH2", message.Frame.Receiver.Identification.Id);
        Assert.Equal(I40MessageTypes.CALL_FOR_PROPOSAL, message.Frame.Type);
        Assert.NotEmpty(message.Frame.ConversationId);
    }
    
    [Fact]
    public void Build_WithoutSender_ThrowsException()
    {
        // Arrange
        var builder = new I40MessageBuilder();
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            builder.To("RH2").WithType(I40MessageTypes.INFORM).Build());
    }
    
    [Fact]
    public void Build_WithoutReceiver_ThrowsException()
    {
        // Arrange
        var builder = new I40MessageBuilder();
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            builder.From("P24").WithType(I40MessageTypes.INFORM).Build());
    }
    
    [Fact]
    public void Build_WithoutType_ThrowsException()
    {
        // Arrange
        var builder = new I40MessageBuilder();
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            builder.From("P24").To("RH2").Build());
    }
    
    [Fact]
    public void Build_WithCustomConversationId_UsesCustomId()
    {
        // Arrange
        var builder = new I40MessageBuilder();
        var customId = "custom-conversation-123";
        
        // Act
        var message = builder
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.PROPOSAL)
            .WithConversationId(customId)
            .Build();
        
        // Assert
        Assert.Equal(customId, message.Frame.ConversationId);
    }
    
    [Fact]
    public void Build_WithReplyTo_SetsReplyTo()
    {
        // Arrange
        var builder = new I40MessageBuilder();
        var replyToId = "original-message-id";
        
        // Act
        var message = builder
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.PROPOSAL)
            .Build();
        
        // Note: ReplyTo removed from protocol; test removed.
    }
    
    [Fact]
    public void Build_WithElements_AddsElements()
    {
        // Arrange
        var builder = new I40MessageBuilder();
        var property = I40MessageBuilder.CreateStringProperty("TestProperty", "TestValue");
        
        // Act
        var message = builder
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.INFORM)
            .AddElement(property)
            .Build();
        
        // Assert
        Assert.Single(message.InteractionElements);
        Assert.IsAssignableFrom<Property>(message.InteractionElements[0]);
        Assert.Equal("TestProperty", message.InteractionElements[0].IdShort);
    }
}
