# MQTT Monitor Tool - Visualisierung aller I4.0 Messages

## 🎯 Verwendung

Der MQTT Monitor ist ein Live-Überwachungstool, das alle I4.0-Nachrichten auf dem MQTT Broker anzeigt.

### Schnellstart

```bash
# Terminal 1: Monitor starten
cd /home/benjamin/AgentDevelopment/I4.0-Sharp-Messaging
chmod +x monitor-mqtt.sh
./monitor-mqtt.sh

# Terminal 2: Tests ausführen
cd I40Sharp.Messaging.Tests
dotnet test --filter "FullyQualifiedName~Integration"
```

### Manuelle Ausführung

```bash
cd I40Sharp.Messaging.Tests
dotnet run --project Tools/MqttMonitor.cs

# Mit JSON-Ausgabe
dotnet run --project Tools/MqttMonitor.cs -- --json

# Anderer Broker
dotnet run --project Tools/MqttMonitor.cs -- mqtt.example.com 1883
```

## 📊 Ausgabebeispiel

```
═══════════════════════════════════════
📨 Nachricht #1
   Type:         callForProposal
   From:         P24
   To:           RH2
   Conversation: abc-123-def
   MessageId:    msg-001
   Elements:     1
     • Property: RequiredCapability
       = Assemble (xs:string)
   🎯 CALL FOR PROPOSAL erkannt

═══════════════════════════════════════
📨 Nachricht #2
   Type:         proposal
   From:         RH2
   To:           P24
   Conversation: abc-123-def
   MessageId:    msg-002
   ReplyTo:      msg-001
   Elements:     2
     • Property: EstimatedCost
       = 42.5 (xs:double)
     • Property: EstimatedTime
       = 120 (xs:integer)
   💡 PROPOSAL erkannt
```

## 🎨 Features

- ✅ **Live-Überwachung** aller MQTT Topics
- ✅ **Farbcodierte Ausgabe** nach Message Type
- ✅ **Detaillierte Element-Anzeige** (Properties, Collections, Lists)
- ✅ **Conversation-Tracking**
- ✅ **Statistiken** (Nachrichten/Sekunde, Gesamtzahl)
- ✅ **Optional JSON-Export**
- ✅ **Wildcard-Support** (#, +)

## 🔍 Überwachte Topics

Der Monitor abonniert automatisch:
- `i40/messages` (Standard-Topic für I4.0 Messages)
- `test/#` (Alle Test-Topics)
- `factory/#` (Production System Topics)
- `#` (Alle anderen Topics)

## ⚙️ Konfiguration

### Eigene Topics hinzufügen

```csharp
// In MqttMonitor.cs
await client.SubscribeAsync("my/custom/topic");
await client.SubscribeAsync("production/+/status");
```

### Filterung nach Message Type

```csharp
client.OnMessageType(I40MessageTypes.REQUIREMENT, msg =>
{
    Console.WriteLine("   📋 REQUIREMENT erkannt");
});
```

## 🐛 Debugging mit Monitor

### Szenario 1: Tests liefern keine Nachrichten

```bash
# Terminal 1: Monitor
./monitor-mqtt.sh

# Terminal 2: Tests
dotnet test --filter "PublishAsync_SendsMessageSuccessfully"
```

→ Wenn Monitor nichts anzeigt: Verbindungsproblem oder Topic-Mismatch

### Szenario 2: Message Format validieren

```bash
# Mit JSON-Ausgabe starten
dotnet run --project Tools/MqttMonitor.cs -- --json
```

→ Zeigt vollständiges JSON zur Validierung

### Szenario 3: Conversation-Tracking

```bash
# Monitor filtert automatisch nach ConversationId
# Nachrichten mit gleicher ConversationId werden gruppiert
```

## 🚀 Integration in CI/CD

```yaml
# .github/workflows/test.yml
- name: Start MQTT Monitor
  run: ./monitor-mqtt.sh &
  
- name: Run Integration Tests
  run: dotnet test --filter "Integration"
  
- name: Collect Monitor Logs
  run: pkill -INT MqttMonitor
```

## 💡 Tipps

### Performance-Test

```bash
# Zähle Nachrichten pro Sekunde
./monitor-mqtt.sh | grep "msg/s"
```

### Nachrichten speichern

```bash
# Alle Nachrichten in Datei loggen
./monitor-mqtt.sh --json > mqtt_log_$(date +%Y%m%d_%H%M%S).json
```

### Nur bestimmte Agents überwachen

```csharp
client.OnSender("P24", msg => {
    // Nur Nachrichten von P24
});

client.OnReceiver("RH2", msg => {
    // Nur Nachrichten an RH2
});
```

## 📝 Alternativen

Falls der Monitor nicht funktioniert, können Sie auch verwenden:

```bash
# Mosquitto CLI Tools
mosquitto_sub -h localhost -p 1883 -t "#" -v

# MQTT Explorer (GUI)
# https://mqtt-explorer.com/

# MQTTX CLI
mqttx sub -h localhost -p 1883 -t "#"
```
