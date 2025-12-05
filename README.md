# I4.0 Sharp Messaging

## ⭐ Overview

**I4.0 Sharp Messaging** ist eine modulare, erweiterbare Messaging-Bibliothek für Industrie-4.0-Systeme, die den Austausch von **I4.0-konformen Nachrichten** zwischen autonomen Produktionsagenten (Holons) ermöglicht.

Die Bibliothek konzentriert sich zunächst auf **MQTT** als Transportprotokoll, bietet jedoch bereits eine transport-agnostische Architektur, sodass später weitere Transportschichten wie Kafka, AMQP, OPC UA PubSub, WebSockets oder In-Memory Messaging ergänzt werden können.

Der Messaging Client ermöglicht:

- Aufbau und Verwaltung von MQTT-Verbindungen  
- Senden und Empfangen von I4.0 Messages  
- Registrierung von Callbacks (MessageType, Sender, Receiver, ConversationId)  
- Serialisierung & Deserialisierung von AAS-Elementen  
- Conversation Tracking  
- Semantische Validierung (optional)

---

## ⭐ Motivation

Holonische Produktionssysteme benötigen eine standardisierte Form der Kommunikation zwischen autonomen Agenten (Ressourcen, Produkten, Transportern, Modulen).  
Die I4.0 Message ist dabei der semantische Container, der AAS-Daten strukturiert überträgt.

Ziele der Bibliothek:

- Hohe Modularität  
- Abstraktion vom Transportkanal  
- Semantische Integrität  
- AAS Roundtrip-Fähigkeit  
- Einfache Integration in Behavior Trees  

---

# ⚙️ I4.0 Message Format

Eine Nachricht besteht aus zwei Hauptkomponenten:

### **Frame**
- `sender`
- `receiver`
- `type` (MessageType)
- `conversationId`

### **InteractionElements**
AAS-SubmodelElement-Strukturen, serialisiert als JSON.

Beispiel:

```json
{
  "frame": {
    "sender": { "identification": { "id": "P24" }, "role": {} },
    "receiver": { "identification": { "id": "RH2" }, "role": {} },
    "type": "consent",
    "conversationId": "https://smartfactory.de/shells/afyiKUW9eU"
  },
  "interactionElements": [
    {
      "modelType": "SubmodelElementCollection",
      "semanticId": {
        "keys": [
          { "type": "GlobalReference", "value": "https://smartfactory.de/semantics/submodel-element/Step" }
        ],
        "type": "ExternalReference"
      },
      "idShort": "Step0001",
      "value": [
        {
          "modelType": "Property",
          "semanticId": {
            "keys": [
              { "type": "GlobalReference", "value": "https://smartfactory.de/semantics/submodel-element/Step/StepTitle" }
            ],
            "type": "ExternalReference"
          },
          "value": "Load",
          "valueType": "xs:string",
          "idShort": "StepTitle"
        }
      ]
    }
  ]
}
```

---

# 📨 Unterstützte MessageTypes

## 🟦 Negotiation Types
| Type | Beschreibung |
|------|--------------|
| `CallForProposal` | Angebot anfordern |
| `Proposal` | Proposal eines Bieters |
| `acceptProposal` | Proposal akzeptieren |
| `denyProposal` | Proposal ablehnen |

## 🟨 Informational
| Type | Beschreibung |
|------|--------------|
| `inform` | Information oder Status |
| `informConfirm` | Bestätigung |
| `failure` | Fehlermeldung |

## 🟧 Requirement-Oriented
| Type | Beschreibung |
|------|--------------|
| `requirement` | Schritt anfordern |
| `requirementInform` | Fortschrittsinfo |
| `requirementRepeat` | Wiederholen |
| `requirementPreviously` | Aus vorheriger Conversation |
| `requirementTerminate` | Terminieren |

## 🟥 Lifecycle Nachrichten
| Type | Beschreibung |
|------|--------------|
| `Lifecycle_killAgent` | Agent beenden |
| `Lifecycle_restartAgent` | Agent neu starten |
| `Lifecycle_spawnAgent` | Kind-Holon erzeugen |
| `Lifecycle_updateAgent` | Topologie aktualisieren |

## 🟩 Order / Production Plan
| Type | Beschreibung |
|------|--------------|
| `recipe` | Produktionsrezept |
| `Order_deleteAction` | Aktion löschen |
| `Order_terminateAction` | Aktion abbrechen |
| `Order_doneAction` | Aktion fertig |
| `Order_executeAction` | Aktion starten |
| `Order_productCreation` | Produkt / BOM erzeugen |

---

# 🧱 Architektur

```
I40Sharp.Messaging
 ├── MessagingClient
 ├── IMessagingTransport
 │      └── MqttTransport (erste Implementierung)
 ├── MessageSerializer
 ├── MessageRouter
 ├── CallbackRegistry
 └── ConversationManager
```

---

# 🔌 MQTT Transport (initial implementation)

- Nutzt **MQTTnet**
- Unterstützt QoS 0 und 1
- Automatische Wiederverbindung (Reconnect)
- Topic-Schema frei konfigurierbar

---

# 🔄 Conversation Management

Jede I4.0-Message besitzt eine `conversationId`.

Der MessagingClient stellt bereit:

- automatische Erzeugung von conversationIds  
- Sammeln aller eingehenden Nachrichten einer Conversation  
- Conversation-Timeouts  
- Wiederaufnahme (`requirementPreviously`)  

---

# 🧩 Callback-System

Callbacks können auf verschiedenen Ebenen registriert werden:

```csharp
client.OnMessage(msg => {...});
client.OnMessageType("Proposal", msg => {...});
client.OnSender("P24", msg => {...});
client.OnReceiver("RH2", msg => {...});
client.OnConversation("https://id/123", msg => {...});
```

Mehrere Filter können kombiniert werden.

---

# 📦 Serialisierung / Deserialisierung

Der Serializer stellt sicher:

- Validierung der AAS-Struktur  
- Normalisierung der JSON-Form  
- Roundtrip-Sicherheit (AAS → Message → AAS)  
- Konsistente Struktur für Message-Routing  

---

# 🛠 Beispiel: Senden einer Nachricht

```csharp
var msg = new I40MessageBuilder()
    .From("P24")
    .To("RH2")
    .WithType(I40_MessageTypes.CONSENT)
    .WithConversationId(Guid.NewGuid().ToString())
    .AddElement(stepElement)
    .Build();

client.Publish(msg);
```

---

# 🧪 Beispiel: Callback registrieren

```csharp
client.OnMessageType(I40_MessageTypes.CALL_FOR_PROPOSAL, msg =>
{
    Console.WriteLine("Received CFP from: " + msg.Frame.Sender.Id);
});
```

---
# I4.0L-Sharp-Messaging
