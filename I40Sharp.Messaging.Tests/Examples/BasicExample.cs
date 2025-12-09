using System.Linq;
using I40Sharp.Messaging;
using I40Sharp.Messaging.Core;
using I40Sharp.Messaging.Models;
using I40Sharp.Messaging.Transport;
using BaSyx.Models.AdminShell;

namespace I40Sharp.Messaging.Examples;

/// <summary>
/// Beispielanwendung für den I4.0 Sharp Messaging Client
/// Demonstriert die grundlegende Verwendung des Clients
/// </summary>
public class BasicExample
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== I4.0 Sharp Messaging Client - Beispiel ===\n");
        
        // MQTT Transport erstellen
        var transport = new MqttTransport("localhost", 1883, "example-client");
        var client = new MessagingClient(transport);
        
        // Events registrieren
        client.Connected += (s, e) => Console.WriteLine("✓ Verbunden mit MQTT Broker");
        client.Disconnected += (s, e) => Console.WriteLine("✗ Verbindung getrennt");
        
        // Callbacks registrieren
        client.OnMessage(msg => 
        {
            Console.WriteLine($"📨 Nachricht empfangen: {msg.Frame.Type}");
            Console.WriteLine($"   Von: {msg.Frame.Sender.Identification.Id}");
            Console.WriteLine($"   An: {msg.Frame.Receiver.Identification.Id}");
            Console.WriteLine($"   Conversation: {msg.Frame.ConversationId}\n");
        });
        
        client.OnMessageType(I40MessageTypes.PROPOSAL, msg =>
        {
            Console.WriteLine($"💡 Proposal empfangen von {msg.Frame.Sender.Identification.Id}");
        });
        
        try
        {
            // Verbinden
            Console.WriteLine("Verbinde mit MQTT Broker...");
            await client.ConnectAsync();
            await Task.Delay(1000);
            
            // Beispiel 1: Einfache Nachricht senden
            Console.WriteLine("\n--- Beispiel 1: Einfache Nachricht ---");
            var simpleMessage = new I40MessageBuilder()
                .From("ProductHolon_P24")
                .To("ResourceHolon_RH2")
                .WithType(I40MessageTypes.INFORM)
                .Build();
            
            await client.PublishAsync(simpleMessage);
            Console.WriteLine("✓ Nachricht gesendet");
            await Task.Delay(500);
            
            // Beispiel 2: Call for Proposal mit Daten
            Console.WriteLine("\n--- Beispiel 2: Call for Proposal ---");
            var conversationId = client.CreateConversation();
            
            var stepProperty = I40MessageBuilder.CreateStringProperty("StepTitle", "Assemble");
            stepProperty.SemanticId = new Reference(
                new Key(KeyType.GlobalReference, "https://smartfactory.de/semantics/Step/Title"));
            
            var stepCollection = new SubmodelElementCollection("Step0001")
            {
                SemanticId = new Reference(
                    new Key(KeyType.GlobalReference, "https://smartfactory.de/semantics/Step"))
            };
            stepCollection.Value.Value.Add(stepProperty);
            
            var cfpMessage = new I40MessageBuilder()
                .From("ProductHolon_P24")
                .To("ResourceHolon_RH2")
                .WithType(I40MessageTypes.CALL_FOR_PROPOSAL)
                .WithConversationId(conversationId)
                .AddElement(stepCollection)
                .Build();
            
            await client.PublishAsync(cfpMessage);
            Console.WriteLine("✓ Call for Proposal gesendet");
            await Task.Delay(500);
            
            // Beispiel 3: Proposal als Antwort
            Console.WriteLine("\n--- Beispiel 3: Proposal Antwort ---");
            var proposalMessage = new I40MessageBuilder()
                .From("ResourceHolon_RH2")
                .To("ProductHolon_P24")
                .WithType(I40MessageTypes.PROPOSAL)
                .WithConversationId(conversationId)
                .AddElement(new Property<double>("EstimatedCost", 42.5))
                .Build();
            
            await client.PublishAsync(proposalMessage);
            Console.WriteLine("✓ Proposal gesendet");
            await Task.Delay(500);
            
            // Conversation-Historie anzeigen
            Console.WriteLine("\n--- Conversation Historie ---");
            var conversationMessages = client.GetConversationMessages(conversationId);
            Console.WriteLine($"Nachrichten in Conversation '{conversationId}': {conversationMessages.Count}");
            foreach (var msg in conversationMessages)
            {
                Console.WriteLine($"  - {msg.Frame.Type} von {msg.Frame.Sender.Identification.Id}");
            }
            
            // Warte auf weitere Nachrichten
            Console.WriteLine("\n⏳ Warte 5 Sekunden auf weitere Nachrichten...");
            await Task.Delay(5000);
            
            // Conversation abschließen
            client.CompleteConversation(conversationId);
            client.CleanupExpiredConversations();
            
            Console.WriteLine("\n✓ Beispiel abgeschlossen");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fehler: {ex.Message}");
        }
        finally
        {
            // Verbindung trennen
            await client.DisconnectAsync();
            client.Dispose();
            Console.WriteLine("\n👋 Auf Wiedersehen!");
        }
    }
}
