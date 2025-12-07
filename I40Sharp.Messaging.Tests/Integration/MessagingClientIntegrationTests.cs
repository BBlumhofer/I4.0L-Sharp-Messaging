using I40Sharp.Messaging;
using I40Sharp.Messaging.Core;
using I40Sharp.Messaging.Models;
using I40Sharp.Messaging.Transport;
using BaSyx.Models.AdminShell;
using Xunit;

namespace I40Sharp.Messaging.Tests.Integration;

/// <summary>
/// Integrationstests für den MessagingClient mit echtem MQTT Broker
/// Voraussetzung: MQTT Broker läuft auf localhost:1883
/// </summary>
public class MessagingClientIntegrationTests : IDisposable
{
    private readonly string _broker = "localhost";
    private readonly int _port = 1883;
    private readonly List<MessagingClient> _clients = new();
    
    [Fact]
    public async Task ConnectAsync_ConnectsToMqttBroker()
    {
        // Arrange
        var transport = new MqttTransport(_broker, _port, "test-client-connect");
        var client = new MessagingClient(transport);
        _clients.Add(client);
        
        // Act
        await client.ConnectAsync();
        await Task.Delay(500); // Warte auf Verbindung
        
        // Assert
        Assert.True(client.IsConnected);
        
        // Cleanup
        await client.DisconnectAsync();
    }
    
    [Fact]
    public async Task PublishAsync_SendsMessageSuccessfully()
    {
        // Arrange
        var transport = new MqttTransport(_broker, _port, "test-client-publish");
        var client = new MessagingClient(transport);
        _clients.Add(client);
        
        await client.ConnectAsync();
        await Task.Delay(500);
        
        var message = new I40MessageBuilder()
            .From("TestSender")
            .To("TestReceiver")
            .WithType(I40MessageTypes.INFORM)
            .Build();
        
        // Act
        await client.PublishAsync(message);
        
        // Assert - Kein Fehler bedeutet Erfolg
        Assert.True(client.IsConnected);
        
        // Cleanup
        await client.DisconnectAsync();
    }
    
    [Fact]
    public async Task MessageExchange_TwoClients_ReceivesMessage()
    {
        // Arrange
        var transport1 = new MqttTransport(_broker, _port, "test-client-sender");
        var transport2 = new MqttTransport(_broker, _port, "test-client-receiver");
        
        var sender = new MessagingClient(transport1, "test/integration");
        var receiver = new MessagingClient(transport2, "test/integration");
        
        _clients.Add(sender);
        _clients.Add(receiver);
        
        I40Message? receivedMessage = null;
        var messageReceived = new TaskCompletionSource<bool>();
        
        receiver.OnMessageType(I40MessageTypes.CALL_FOR_PROPOSAL, msg =>
        {
            receivedMessage = msg;
            messageReceived.TrySetResult(true);
        });
        
        await sender.ConnectAsync();
        await receiver.ConnectAsync();
        await Task.Delay(1000); // Warte auf Verbindungen
        
        var message = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.CALL_FOR_PROPOSAL)
            .AddElement(new Property<string>("TestProperty", "TestValue"))
            .Build();
        
        // Act
        await sender.PublishAsync(message, "test/integration");
        
        // Warte auf Nachrichtenempfang (max 5 Sekunden)
        var completed = await Task.WhenAny(
            messageReceived.Task, 
            Task.Delay(5000)
        ) == messageReceived.Task;
        
        // Assert
        Assert.True(completed, "Nachricht wurde nicht innerhalb von 5 Sekunden empfangen");
        Assert.NotNull(receivedMessage);
        Assert.Equal("P24", receivedMessage?.Frame.Sender.Identification.Id);
        Assert.Equal("RH2", receivedMessage?.Frame.Receiver.Identification.Id);
        Assert.Equal(I40MessageTypes.CALL_FOR_PROPOSAL, receivedMessage?.Frame.Type);
        Assert.Single(receivedMessage?.InteractionElements ?? new List<ISubmodelElement>());
        
        // Cleanup
        await sender.DisconnectAsync();
        await receiver.DisconnectAsync();
    }
    
    [Fact]
    public async Task ConversationTracking_TracksMultipleMessages()
    {
        // Arrange
        var transport = new MqttTransport(_broker, _port, "test-client-conversation");
        var client = new MessagingClient(transport, "test/conversation");
        _clients.Add(client);
        
        await client.ConnectAsync();
        await Task.Delay(500);
        
        var conversationId = client.CreateConversation();
        
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
        
        // Act
        await client.PublishAsync(message1, "test/conversation");
        await Task.Delay(200);
        await client.PublishAsync(message2, "test/conversation");
        await Task.Delay(500); // Warte auf MQTT Echo
        
        var messages = client.GetConversationMessages(conversationId);
        
        // Assert
        // Der Client empfängt seine eigenen Nachrichten über MQTT zurück,
        // daher erwarten wir 4 Nachrichten (2 gesendet + 2 empfangen)
        Assert.Equal(4, messages.Count);
        Assert.All(messages, msg => Assert.Equal(conversationId, msg.Frame.ConversationId));
        
        // Prüfe, dass beide Message Types vorhanden sind
        var cfpMessages = messages.Where(m => m.Frame.Type == I40MessageTypes.CALL_FOR_PROPOSAL).ToList();
        var proposalMessages = messages.Where(m => m.Frame.Type == I40MessageTypes.PROPOSAL).ToList();
        Assert.Equal(2, cfpMessages.Count); // Einmal gesendet, einmal empfangen
        Assert.Equal(2, proposalMessages.Count); // Einmal gesendet, einmal empfangen
        
        // Cleanup
        await client.DisconnectAsync();
    }
    
    [Fact]
    public async Task CallbackFiltering_InvokesCorrectCallbacks()
    {
        // Arrange
        var transport = new MqttTransport(_broker, _port, "test-client-callbacks");
        var client = new MessagingClient(transport, "test/callbacks");
        _clients.Add(client);
        
        var globalCallbackInvoked = false;
        var typeCallbackInvoked = false;
        var senderCallbackInvoked = false;
        
        client.OnMessage(msg => globalCallbackInvoked = true);
        client.OnMessageType(I40MessageTypes.PROPOSAL, msg => typeCallbackInvoked = true);
        client.OnSender("P24", msg => senderCallbackInvoked = true);
        
        await client.ConnectAsync();
        await Task.Delay(500);
        
        var message = new I40MessageBuilder()
            .From("P24")
            .To("RH2")
            .WithType(I40MessageTypes.PROPOSAL)
            .Build();
        
        // Act
        await client.PublishAsync(message, "test/callbacks");
        await Task.Delay(1000); // Warte auf Callback-Ausführung
        
        // Assert
        Assert.True(globalCallbackInvoked);
        Assert.True(typeCallbackInvoked);
        Assert.True(senderCallbackInvoked);
        
        // Cleanup
        await client.DisconnectAsync();
    }
    
    public void Dispose()
    {
        foreach (var client in _clients)
        {
            try
            {
                client.DisconnectAsync().Wait(1000);
                client.Dispose();
            }
            catch
            {
                // Ignoriere Fehler beim Cleanup
            }
        }
    }
}
