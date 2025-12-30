using I40Sharp.Messaging.Core;
using I40Sharp.Messaging.Models;
using Xunit;

namespace I40Sharp.Messaging.Tests;

public class CallbackRegistryTests
{
    [Fact]
    public void RegisterGlobalCallback_InvokesOnAnyMessage()
    {
        // Arrange
        var registry = new CallbackRegistry();
        var callbackInvoked = false;
        
        registry.RegisterGlobalCallback(msg => callbackInvoked = true);
        
        var message = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.INFORM)
            .Build();
        
        // Act
        registry.InvokeCallbacks(message, "/testTopic");
        
        // Assert
        Assert.True(callbackInvoked);
    }
    
    [Fact]
    public void RegisterMessageTypeCallback_InvokesOnlyForMatchingType()
    {
        // Arrange
        var registry = new CallbackRegistry();
        var callbackInvoked = false;
        
        registry.RegisterMessageTypeCallback(I40MessageTypes.PROPOSAL, msg => callbackInvoked = true);
        
        var proposalMessage = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.PROPOSAL)
            .Build();
        
        var informMessage = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.INFORM)
            .Build();
        
        // Act
        registry.InvokeCallbacks(proposalMessage, "/testTopic");
        Assert.True(callbackInvoked);
        
        callbackInvoked = false;
        registry.InvokeCallbacks(informMessage, "/testTopic");
        
        // Assert
        Assert.False(callbackInvoked);
    }
    
    [Fact]
    public void RegisterSenderCallback_InvokesOnlyForMatchingSender()
    {
        // Arrange
        var registry = new CallbackRegistry();
        var callbackInvoked = false;
        
        registry.RegisterSenderCallback("P24", msg => callbackInvoked = true);
        
        var matchingMessage = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.INFORM)
            .Build();
        
        var nonMatchingMessage = new I40MessageBuilder()
            .From("P25")
            .To("RH2")
            .WithType(I40MessageTypes.INFORM)
            .Build();
        
        // Act
        registry.InvokeCallbacks(matchingMessage, "/testTopic");
        Assert.True(callbackInvoked);
        
        callbackInvoked = false;
        registry.InvokeCallbacks(nonMatchingMessage, "/testTopic");
        
        // Assert
        Assert.False(callbackInvoked);
    }
    
    [Fact]
    public void RegisterConversationCallback_InvokesOnlyForMatchingConversation()
    {
        // Arrange
        var registry = new CallbackRegistry();
        var callbackInvoked = false;
        var conversationId = "test-conversation-123";
        
        registry.RegisterConversationCallback(conversationId, msg => callbackInvoked = true);
        
        var matchingMessage = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.INFORM)
            .WithConversationId(conversationId)
            .Build();
        
        var nonMatchingMessage = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.INFORM)
            .Build();
        
        // Act
        registry.InvokeCallbacks(matchingMessage, "/testTopic");
        Assert.True(callbackInvoked);
        
        callbackInvoked = false;
        registry.InvokeCallbacks(nonMatchingMessage, "/testTopic");
        
        // Assert
        Assert.False(callbackInvoked);
    }
    
    [Fact]
    public void InvokeCallbacks_MultipleCallbacks_InvokesAll()
    {
        // Arrange
        var registry = new CallbackRegistry();
        var callback1Invoked = false;
        var callback2Invoked = false;
        
        registry.RegisterGlobalCallback(msg => callback1Invoked = true);
        registry.RegisterMessageTypeCallback(I40MessageTypes.INFORM, msg => callback2Invoked = true);
        
        var message = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.INFORM)
            .Build();
        
        // Act
        registry.InvokeCallbacks(message, "/testTopic");
        
        // Assert
        Assert.True(callback1Invoked);
        Assert.True(callback2Invoked);
    }
    
    [Fact]
    public void ClearCallbacks_RemovesAllCallbacks()
    {
        // Arrange
        var registry = new CallbackRegistry();
        var callbackInvoked = false;
        
        registry.RegisterGlobalCallback(msg => callbackInvoked = true);
        registry.ClearCallbacks();
        
        var message = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.INFORM)
            .Build();
        
        // Act
        registry.InvokeCallbacks(message, "/testTopic");
        
        // Assert
        Assert.False(callbackInvoked);
    }
}
