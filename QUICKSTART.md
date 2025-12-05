# I4.0 Sharp Messaging Client - Quick Start Guide

## 🚀 Schnellstart

### 1. MQTT Broker starten

```bash
# Option A: Mit Docker Compose (empfohlen)
cd ../playground-v3
docker-compose up -d mosquitto

# Option B: Manuell mit Mosquitto
mosquitto -c mosquitto.conf
```

### 2. Projekt bauen

```bash
cd I40Sharp.Messaging
dotnet build
```

### 3. Tests ausführen

```bash
# Unit Tests (kein MQTT Broker erforderlich)
cd ../I40Sharp.Messaging.Tests
dotnet test --filter "FullyQualifiedName!~Integration"

# Integrationstests (MQTT Broker erforderlich auf localhost:1883)
dotnet test --filter "FullyQualifiedName~Integration"

# Alle Tests
dotnet test
```

### 4. Oder mit dem Test-Skript

```bash
cd ..
./run-tests.sh
```

## 📝 Basis-Beispiel

```csharp
using I40Sharp.Messaging;
using I40Sharp.Messaging.Core;
using I40Sharp.Messaging.Models;
using I40Sharp.Messaging.Transport;

// 1. Transport und Client erstellen
var transport = new MqttTransport("localhost", 1883, "my-agent-id");
var client = new MessagingClient(transport);

// 2. Callbacks registrieren
client.OnMessageType(I40MessageTypes.PROPOSAL, message =>
{
    Console.WriteLine($"Proposal empfangen von {message.Frame.Sender.Identification.Id}");
    
    // Proposal-Daten verarbeiten
    foreach (var element in message.InteractionElements)
    {
        if (element is Property prop)
        {
            Console.WriteLine($"  {prop.IdShort}: {prop.Value}");
        }
    }
});

// 3. Verbinden
await client.ConnectAsync();

// 4. Nachricht senden
var message = new I40MessageBuilder()
    .From("ProductHolon_P24")
    .To("ResourceHolon_RH2")
    .WithType(I40MessageTypes.CALL_FOR_PROPOSAL)
    .AddElement(new Property
    {
        IdShort = "RequiredCapability",
        Value = "Assemble",
        ValueType = "xs:string"
    })
    .Build();

await client.PublishAsync(message);

// 5. Warten und dann Trennen
await Task.Delay(5000);
await client.DisconnectAsync();
client.Dispose();
```

## 🔥 Erweiterte Beispiele

### Request-Response Pattern

```csharp
// Sender (Product Holon)
var conversationId = client.CreateConversation();

var request = new I40MessageBuilder()
    .From("P24")
    .To("RH2")
    .WithType(I40MessageTypes.CALL_FOR_PROPOSAL)
    .WithConversationId(conversationId)
    .ReplyBy(DateTime.UtcNow.AddSeconds(30))
    .Build();

await client.PublishAsync(request);

// Empfänger (Resource Holon)
client.OnMessageType(I40MessageTypes.CALL_FOR_PROPOSAL, async request =>
{
    var response = new I40MessageBuilder()
        .From("RH2")
        .To(request.Frame.Sender.Identification.Id)
        .WithType(I40MessageTypes.PROPOSAL)
        .WithConversationId(request.Frame.ConversationId)
        .ReplyingTo(request.Frame.MessageId!)
        .AddElement(new Property
        {
            IdShort = "EstimatedTime",
            Value = "120",
            ValueType = "xs:integer"
        })
        .Build();
    
    await client.PublishAsync(response);
});
```

### Bidding/Negotiation

```csharp
// Product Holon sendet CFP
var cfp = new I40MessageBuilder()
    .From("P24")
    .To("broadcast")
    .WithType(I40MessageTypes.CALL_FOR_PROPOSAL)
    .Build();

await client.PublishAsync(cfp, "factory/bidding");

// Resource Holons antworten mit Proposals
client.OnMessageType(I40MessageTypes.CALL_FOR_PROPOSAL, async msg =>
{
    var proposal = new I40MessageBuilder()
        .From("RH2")
        .To(msg.Frame.Sender.Identification.Id)
        .WithType(I40MessageTypes.PROPOSAL)
        .WithConversationId(msg.Frame.ConversationId)
        .AddElement(new SubmodelElementCollection
        {
            IdShort = "Offer",
            Value = new List<SubmodelElement>
            {
                new Property { IdShort = "Cost", Value = "42.5", ValueType = "xs:double" },
                new Property { IdShort = "StartTime", Value = DateTime.UtcNow.AddMinutes(10).ToString("o"), ValueType = "xs:dateTime" }
            }
        })
        .Build();
    
    await client.PublishAsync(proposal, "factory/bidding");
});

// Product Holon akzeptiert bestes Angebot
client.OnMessageType(I40MessageTypes.PROPOSAL, async proposal =>
{
    var acceptance = new I40MessageBuilder()
        .From("P24")
        .To(proposal.Frame.Sender.Identification.Id)
        .WithType(I40MessageTypes.ACCEPT_PROPOSAL)
        .WithConversationId(proposal.Frame.ConversationId)
        .Build();
    
    await client.PublishAsync(acceptance);
});
```

### Conversation Tracking

```csharp
var conversationId = client.CreateConversation(TimeSpan.FromMinutes(15));

// Mehrere Nachrichten in derselben Conversation senden
for (int i = 0; i < 5; i++)
{
    var msg = new I40MessageBuilder()
        .From("P24")
        .To("RH2")
        .WithType(I40MessageTypes.INFORM)
        .WithConversationId(conversationId)
        .Build();
    
    await client.PublishAsync(msg);
    await Task.Delay(1000);
}

// Historie abrufen
var history = client.GetConversationMessages(conversationId);
Console.WriteLine($"Conversation enthält {history.Count} Nachrichten");

foreach (var msg in history)
{
    Console.WriteLine($"  {msg.Frame.Type} @ {msg.CreatedAt}");
}

// Conversation abschließen
client.CompleteConversation(conversationId);
```

## 🎯 Integration mit BehaviorTree Nodes

```csharp
// Beispiel: ConnectToMessagingBroker Node
public class ConnectToMessagingBrokerNode : BTNode
{
    private MessagingClient _client;
    
    public override async Task<NodeStatus> Execute()
    {
        try
        {
            var transport = new MqttTransport("localhost", 1883, AgentId);
            _client = new MessagingClient(transport, $"factory/{AgentRole}/messages");
            
            await _client.ConnectAsync();
            
            Context.Set("MessagingClient", _client);
            return NodeStatus.Success;
        }
        catch (Exception ex)
        {
            Logger.Error($"Verbindung fehlgeschlagen: {ex.Message}");
            return NodeStatus.Failure;
        }
    }
}

// Beispiel: SendMessage Node
public class SendMessageNode : BTNode
{
    public string TargetAgent { get; set; }
    public string MessageType { get; set; }
    public List<SubmodelElement> Payload { get; set; }
    
    public override async Task<NodeStatus> Execute()
    {
        var client = Context.Get<MessagingClient>("MessagingClient");
        
        var message = new I40MessageBuilder()
            .From(Context.AgentId)
            .To(TargetAgent)
            .WithType(MessageType)
            .AddElements(Payload)
            .Build();
        
        await client.PublishAsync(message);
        return NodeStatus.Success;
    }
}

// Beispiel: WaitForMessage Node
public class WaitForMessageNode : BTNode
{
    public string ExpectedType { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    
    public override async Task<NodeStatus> Execute()
    {
        var client = Context.Get<MessagingClient>("MessagingClient");
        var messageReceived = new TaskCompletionSource<I40Message>();
        
        client.OnMessageType(ExpectedType, msg =>
        {
            messageReceived.TrySetResult(msg);
        });
        
        var completedTask = await Task.WhenAny(
            messageReceived.Task,
            Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds))
        );
        
        if (completedTask == messageReceived.Task)
        {
            Context.Set("ReceivedMessage", messageReceived.Task.Result);
            return NodeStatus.Success;
        }
        
        return NodeStatus.Failure;
    }
}
```

## 📊 Verfügbare Komponenten

### Core Classes
- `MessagingClient` - Haupt-Client für Messaging-Operationen
- `I40MessageBuilder` - Fluent API für Nachrichtenerstellung
- `MessageSerializer` - JSON Serialisierung/Deserialisierung
- `CallbackRegistry` - Callback-Verwaltung mit Filterung
- `ConversationManager` - Conversation-Tracking und -Verwaltung

### Transport Layer
- `IMessagingTransport` - Transport-Interface für Erweiterbarkeit
- `MqttTransport` - MQTT-Implementierung mit MQTTnet

### Models
- `I40Message` - Hauptnachrichtenmodell
- `MessageFrame` - Frame mit Sender/Receiver/Type
- `SubmodelElement` - Basis für alle AAS-Elemente
- `Property` - AAS Property
- `SubmodelElementCollection` - AAS Collection
- `SubmodelElementList` - AAS List

## 🔧 Troubleshooting

### MQTT Broker nicht erreichbar
```bash
# Prüfe ob Broker läuft
netstat -an | grep 1883

# Starte Broker
docker-compose up -d mosquitto

# Oder manuell
mosquitto -v
```

### Tests schlagen fehl
```bash
# Stelle sicher, dass alle Pakete installiert sind
dotnet restore

# Rebuild
dotnet clean
dotnet build

# Nur Unit Tests ausführen (ohne MQTT)
dotnet test --filter "FullyQualifiedName!~Integration"
```

### Serialisierungsprobleme
```csharp
// Aktiviere Logging
var serializer = new MessageSerializer();
var json = serializer.Serialize(message);
Console.WriteLine(json);

// Validiere Message
if (serializer.IsValidMessage(json))
{
    var deserializedMessage = serializer.Deserialize(json);
}
```
