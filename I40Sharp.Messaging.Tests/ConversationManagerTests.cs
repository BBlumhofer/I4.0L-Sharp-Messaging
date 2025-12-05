using I40Sharp.Messaging.Core;
using I40Sharp.Messaging.Models;
using Xunit;

namespace I40Sharp.Messaging.Tests;

public class ConversationManagerTests
{
    [Fact]
    public void CreateConversation_ReturnsUniqueId()
    {
        // Arrange
        var manager = new ConversationManager();
        
        // Act
        var id1 = manager.CreateConversation();
        var id2 = manager.CreateConversation();
        
        // Assert
        Assert.NotEqual(id1, id2);
        Assert.True(manager.ConversationExists(id1));
        Assert.True(manager.ConversationExists(id2));
    }
    
    [Fact]
    public void AddMessage_CreatesConversationIfNotExists()
    {
        // Arrange
        var manager = new ConversationManager();
        var message = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.INFORM)
            .Build();
        
        var conversationId = message.Frame.ConversationId;
        
        // Act
        manager.AddMessage(message);
        
        // Assert
        Assert.True(manager.ConversationExists(conversationId));
    }
    
    [Fact]
    public void GetMessages_ReturnsAllMessagesInConversation()
    {
        // Arrange
        var manager = new ConversationManager();
        var conversationId = "test-conversation";
        
        var message1 = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.CALL_FOR_PROPOSAL)
            .WithConversationId(conversationId)
            .Build();
        
        var message2 = new I40MessageBuilder()
            .From("RH2")
            .To("P24")
            .WithType(I40MessageTypes.PROPOSAL)
            .WithConversationId(conversationId)
            .Build();
        
        manager.AddMessage(message1);
        manager.AddMessage(message2);
        
        // Act
        var messages = manager.GetMessages(conversationId);
        
        // Assert
        Assert.Equal(2, messages.Count);
        Assert.Equal(I40MessageTypes.CALL_FOR_PROPOSAL, messages[0].Frame.Type);
        Assert.Equal(I40MessageTypes.PROPOSAL, messages[1].Frame.Type);
    }
    
    [Fact]
    public void GetLastMessage_ReturnsLastMessage()
    {
        // Arrange
        var manager = new ConversationManager();
        var conversationId = "test-conversation";
        
        var message1 = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.CALL_FOR_PROPOSAL)
            .WithConversationId(conversationId)
            .Build();
        
        var message2 = new I40MessageBuilder()
            .From("RH2")
            .To("P24")
            .WithType(I40MessageTypes.PROPOSAL)
            .WithConversationId(conversationId)
            .Build();
        
        manager.AddMessage(message1);
        manager.AddMessage(message2);
        
        // Act
        var lastMessage = manager.GetLastMessage(conversationId);
        
        // Assert
        Assert.NotNull(lastMessage);
        Assert.Equal(I40MessageTypes.PROPOSAL, lastMessage.Frame.Type);
    }
    
    [Fact]
    public void CompleteConversation_MarksConversationAsCompleted()
    {
        // Arrange
        var manager = new ConversationManager();
        var conversationId = manager.CreateConversation();
        
        // Act
        manager.CompleteConversation(conversationId);
        
        // Assert
        Assert.Equal(0, manager.GetActiveConversationCount());
    }
    
    [Fact]
    public void CleanupExpiredConversations_RemovesCompletedConversations()
    {
        // Arrange
        var manager = new ConversationManager();
        var conversationId = manager.CreateConversation();
        manager.CompleteConversation(conversationId);
        
        // Act
        manager.CleanupExpiredConversations();
        
        // Assert
        Assert.False(manager.ConversationExists(conversationId));
    }
    
    [Fact]
    public void GetActiveConversationCount_ReturnsCorrectCount()
    {
        // Arrange
        var manager = new ConversationManager();
        var id1 = manager.CreateConversation();
        var id2 = manager.CreateConversation();
        var id3 = manager.CreateConversation();
        
        manager.CompleteConversation(id1);
        
        // Act
        var activeCount = manager.GetActiveConversationCount();
        
        // Assert
        Assert.Equal(2, activeCount);
    }
}
