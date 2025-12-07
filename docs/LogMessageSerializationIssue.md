# LogMessage Serialization Issue

## Aktueller Status
- Die erzeugte Datei `I40Sharp.Messaging.Tests/TestOutputs/MessageSamples/LogMessage.json` enthält im Abschnitt `interactionElements` weiterhin **nur flache Properties** (`LogLevel`, `Message`, `Timestamp`, `AgentRole`, `AgentState`).
- Es fehlt der erwartete `SubmodelElementCollection`-Wrapper (`"idShort": "Log"`) sowie der `value`-Array mit den fünf Properties inklusive `value`-Strings.
- Damit unterscheidet sich die Ausgabe vom Referenzformat aus dem AAS-Sharp-Client (`tests/AasSharpClient.Tests/TestOutputs/MessageExamples/LogMessage.json`).

## Erwartetes Verhalten
- `interactionElements` soll genau **eine** `SubmodelElementCollection` mit `idShort = "Log"` enthalten.
- Diese Collection muss ein `value`-Array besitzen, das für jede Log-Property (`LogLevel`, `Message`, `Timestamp`, `AgentRole`, `AgentState`) eine `Property` inkl. `value`-Eintrag enthält.

## Zuletzt durchgeführte Schritte
1. **Serializer-Code geprüft** (`MessageSerializer` + `FullSubmodelElementConverter` Einsatz) – Implementierung nutzt BaSyx-Konverter inklusive `SubmodelElementCollection`-Support.
2. **Unit-Tests ausgeführt**
   - `dotnet test I40Sharp.Messaging.Tests --filter MessageSerializerTests`
   - `dotnet test I40Sharp.Messaging.Tests --filter Serialize_LogMessage_WritesJsonFile`
   - Tests laufen durch, erzeugen jedoch weiterhin das oben beschriebene JSON ohne Collection + Werte.
3. **Ausgabedatei inspiziert** (`TestOutputs/MessageSamples/LogMessage.json`) – bestätigt, dass trotz Testlauf nur Properties ohne Werte geschrieben werden.

## Offene Fragen / Nächste Ideen
- Prüfen, ob im Testfall `Serialize_LogMessage_WritesJsonFile` wirklich der neue Serializer genutzt wird (z. B. Mehrfachregistrierung von Konvertern, falsche `JsonSerializerOptions`, oder alte Assembly im Output).
- Verifizieren, ob der BaSyx-Converter bei `SubmodelElementCollection` tatsächlich den `value`-Array füllt (ggf. Debug in `FullSubmodelElementConverter.Write`).
- Sicherstellen, dass `LogMessage`-Objekte vor dem Serialisieren korrekt aufgebaut sind (`SubmodelElementCollection` mit `Value.Value`-Einträgen).
- Nach Anpassungen erneut den JSON-Snapshot unter `TestOutputs/MessageSamples/LogMessage.json` erzeugen und vergleichen.
