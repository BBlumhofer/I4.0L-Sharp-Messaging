using System.Linq;
using I40Sharp.Messaging;
using I40Sharp.Messaging.Core;
using I40Sharp.Messaging.Models;
using I40Sharp.Messaging.Transport;
using BaSyx.Models.AdminShell;
using Xunit;

namespace I40Sharp.Messaging.Tests.Integration;

/// <summary>
/// Erweiterte Tests mit AAS-konformen Action/Step Strukturen
/// </summary>
public class AasIntegrationTests : IDisposable
{
    private readonly string _broker = "localhost";
    private readonly int _port = 1883;
    private readonly List<MessagingClient> _clients = new();
    
    [Fact(Skip = "Requires running MQTT broker on localhost:1883")]
    public async Task SendActionRequest_WithCompleteAasStructure_ReceivesMessage()
    {
        // Arrange - Erstelle Sender (Product Holon) und Empfänger (Resource Holon)
        var senderTransport = new MqttTransport(_broker, _port, "product-holon-p24");
        var receiverTransport = new MqttTransport(_broker, _port, "resource-holon-rh2");
        
        var sender = new MessagingClient(senderTransport, "factory/actions");
        var receiver = new MessagingClient(receiverTransport, "factory/actions");
        
        _clients.Add(sender);
        _clients.Add(receiver);
        
        I40Message? receivedMessage = null;
        var messageReceived = new TaskCompletionSource<bool>();
        
        // Empfänger wartet auf Action Request
        receiver.OnMessageType(I40MessageTypes.REQUIREMENT, msg =>
        {
            receivedMessage = msg;
            messageReceived.TrySetResult(true);
        });
        
        await sender.ConnectAsync();
        await receiver.ConnectAsync();
        await Task.Delay(1000);
        
        // Act - Erstelle eine vollständige Action-Struktur
        var actionMessage = CreateActionRequestMessage();
        
        await sender.PublishAsync(actionMessage, "factory/actions");
        
        // Warte auf Empfang
        var completed = await Task.WhenAny(
            messageReceived.Task,
            Task.Delay(5000)
        ) == messageReceived.Task;
        
        // Assert
        Assert.True(completed, "Action Request wurde nicht empfangen");
        Assert.NotNull(receivedMessage);
        Assert.Equal(I40MessageTypes.REQUIREMENT, receivedMessage.Frame.Type);
        Assert.Equal("ProductHolon_P24", receivedMessage.Frame.Sender.Identification.Id);
        Assert.Equal("ResourceHolon_RH2", receivedMessage.Frame.Receiver.Identification.Id);
        
        // Prüfe Action-Struktur
        Assert.NotEmpty(receivedMessage.InteractionElements);
        var stepElement = receivedMessage.InteractionElements.FirstOrDefault();
        Assert.NotNull(stepElement);
        Assert.IsType<SubmodelElementCollection>(stepElement);
        
        var step = (SubmodelElementCollection)stepElement;
        Assert.Equal("Step0001", step.IdShort);
        
        // Prüfe Actions Collection
        var actionsCollection = step.Value.Value.FirstOrDefault(e => e.IdShort == "Actions");
        Assert.NotNull(actionsCollection);
        Assert.IsType<SubmodelElementCollection>(actionsCollection);
        
        // Cleanup
        await sender.DisconnectAsync();
        await receiver.DisconnectAsync();
    }
    
    [Fact(Skip = "Requires running MQTT broker on localhost:1883")]
    public async Task SendProposalWithScheduling_WithTimeWindows_ReceivesMessage()
    {
        // Arrange
        var resourceTransport = new MqttTransport(_broker, _port, "resource-holon-rh2-proposal");
        var productTransport = new MqttTransport(_broker, _port, "product-holon-p24-proposal");
        
        var resource = new MessagingClient(resourceTransport, "factory/proposals");
        var product = new MessagingClient(productTransport, "factory/proposals");
        
        _clients.Add(resource);
        _clients.Add(product);
        
        I40Message? receivedProposal = null;
        var proposalReceived = new TaskCompletionSource<bool>();
        
        product.OnMessageType(I40MessageTypes.PROPOSAL, msg =>
        {
            receivedProposal = msg;
            proposalReceived.TrySetResult(true);
        });
        
        await resource.ConnectAsync();
        await product.ConnectAsync();
        await Task.Delay(1000);
        
        // Act - Resource sendet Proposal mit Scheduling-Informationen
        var conversationId = Guid.NewGuid().ToString();
        var proposal = CreateProposalWithScheduling(conversationId);
        
        await resource.PublishAsync(proposal, "factory/proposals");
        
        var completed = await Task.WhenAny(
            proposalReceived.Task,
            Task.Delay(5000)
        ) == proposalReceived.Task;
        
        // Assert
        Assert.True(completed, "Proposal wurde nicht empfangen");
        Assert.NotNull(receivedProposal);
        Assert.Equal(I40MessageTypes.PROPOSAL, receivedProposal.Frame.Type);
        
        // Prüfe Scheduling-Daten
        var schedulingElement = receivedProposal.InteractionElements
            .FirstOrDefault(e => e.IdShort == "Scheduling");
        Assert.NotNull(schedulingElement);
        Assert.IsType<SubmodelElementCollection>(schedulingElement);
        
        var scheduling = (SubmodelElementCollection)schedulingElement;
        var startTime = scheduling.Value.Value.FirstOrDefault(e => e.IdShort == "StartDateTime") as Property;
        Assert.NotNull(startTime);
        Assert.NotNull(startTime.Value);
        
        // Cleanup
        await resource.DisconnectAsync();
        await product.DisconnectAsync();
    }
    
    [Fact(Skip = "Requires running MQTT broker on localhost:1883")]
    public async Task CompleteNegotiationCycle_CallForProposalToAcceptance_Success()
    {
        // Arrange - Multi-Agent Negotiation Scenario
        var productTransport = new MqttTransport(_broker, _port, "product-p24-negotiation");
        var resource1Transport = new MqttTransport(_broker, _port, "resource-rh2-negotiation");
        var resource2Transport = new MqttTransport(_broker, _port, "resource-rh3-negotiation");
        
        var product = new MessagingClient(productTransport, "factory/negotiation");
        var resource1 = new MessagingClient(resource1Transport, "factory/negotiation");
        var resource2 = new MessagingClient(resource2Transport, "factory/negotiation");
        
        _clients.Add(product);
        _clients.Add(resource1);
        _clients.Add(resource2);
        
        var proposalsReceived = new List<I40Message>();
        var proposalCount = 0;
        var tcs = new TaskCompletionSource<bool>();
        
        // Product wartet auf Proposals
        product.OnMessageType(I40MessageTypes.PROPOSAL, msg =>
        {
            proposalsReceived.Add(msg);
            proposalCount++;
            if (proposalCount >= 2)
            {
                tcs.TrySetResult(true);
            }
        });
        
        var acceptanceReceived = new TaskCompletionSource<I40Message>();
        
        // Resources warten auf Acceptance
        resource1.OnMessageType(I40MessageTypes.ACCEPT_PROPOSAL, msg => acceptanceReceived.TrySetResult(msg));
        resource2.OnMessageType(I40MessageTypes.ACCEPT_PROPOSAL, msg => acceptanceReceived.TrySetResult(msg));
        
        await product.ConnectAsync();
        await resource1.ConnectAsync();
        await resource2.ConnectAsync();
        await Task.Delay(1500);
        
        // Act - Phase 1: Product sendet Call for Proposal
        var conversationId = product.CreateConversation();
        var cfp = new I40MessageBuilder()
            .From("ProductHolon_P24")
            .To("broadcast")
            .WithType(I40MessageTypes.CALL_FOR_PROPOSAL)
            .WithConversationId(conversationId)
            .AddElement(CreateStepRequirement("Assemble"))
            .Build();
        
        await product.PublishAsync(cfp, "factory/negotiation");
        await Task.Delay(500);
        
        // Phase 2: Resources antworten mit Proposals
        var proposal1 = new I40MessageBuilder()
            .From("ResourceHolon_RH2")
            .To("ProductHolon_P24")
            .WithType(I40MessageTypes.PROPOSAL)
            .WithConversationId(conversationId)
            .AddElement(new Property<double>("EstimatedCost", 45.0))
            .AddElement(new Property<int>("EstimatedDuration", 120))
            .Build();
        
        var proposal2 = new I40MessageBuilder()
            .From("ResourceHolon_RH3")
            .To("ProductHolon_P24")
            .WithType(I40MessageTypes.PROPOSAL)
            .WithConversationId(conversationId)
            .AddElement(new Property<double>("EstimatedCost", 42.0))
            .AddElement(new Property<int>("EstimatedDuration", 140))
            .Build();
        
        await resource1.PublishAsync(proposal1, "factory/negotiation");
        await resource2.PublishAsync(proposal2, "factory/negotiation");
        
        // Warte auf beide Proposals
        var receivedBoth = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;
        Assert.True(receivedBoth, "Nicht alle Proposals empfangen");
        Assert.Equal(2, proposalsReceived.Count);
        
        // Phase 3: Product wählt bestes Angebot (RH3 - niedrigerer Preis)
        var bestProposal = proposalsReceived
            .OrderBy(p => ((PropertyValue)((Property)p.InteractionElements.First(e => e.IdShort == "EstimatedCost")).Value).Value.ToObject<double>())
            .First();
        
        var acceptance = new I40MessageBuilder()
            .From("ProductHolon_P24")
            .To(bestProposal.Frame.Sender.Identification.Id)
            .WithType(I40MessageTypes.ACCEPT_PROPOSAL)
            .WithConversationId(conversationId)
            .Build();
        
        await product.PublishAsync(acceptance, "factory/negotiation");
        
        // Warte auf Acceptance
        var acceptanceCompleted = await Task.WhenAny(
            acceptanceReceived.Task,
            Task.Delay(5000)
        ) == acceptanceReceived.Task;
        
        // Assert
        Assert.True(acceptanceCompleted, "Acceptance wurde nicht empfangen");
        var acceptedMsg = acceptanceReceived.Task.Result;
        Assert.Equal("ResourceHolon_RH3", acceptedMsg.Frame.Receiver.Identification.Id);
        Assert.Equal(conversationId, acceptedMsg.Frame.ConversationId);
        
        // Prüfe Conversation History
        var history = product.GetConversationMessages(conversationId);
        Assert.True(history.Count >= 4); // CFP + 2 Proposals + Acceptance (+ ihre Echos)
        
        // Cleanup
        product.CompleteConversation(conversationId);
        await product.DisconnectAsync();
        await resource1.DisconnectAsync();
        await resource2.DisconnectAsync();
    }
    
    private I40Message CreateActionRequestMessage()
    {
        var conversationId = Guid.NewGuid().ToString();
        
        // Erstelle Action
        var action = new SubmodelElementCollection("Action001")
        {
            Value = new SubmodelElementCollectionValue(),
            SemanticId = new Reference(
                new Key(KeyType.GlobalReference, "https://admin-shell.io/idta/HierarchicalStructures/Action/1/0"))
        };

        action.Value.Value.Add(I40MessageBuilder.CreateStringProperty("ActionTitle", "Assemble"));
        action.Value.Value.Add(I40MessageBuilder.CreateStringProperty("Status", "planned"));
        action.Value.Value.Add(I40MessageBuilder.CreateStringProperty("MachineName", "AssemblyStation_01"));

        var inputParameters = new SubmodelElementCollection("InputParameters") { Value = new SubmodelElementCollectionValue() };
        inputParameters.Value.Value.Add(new Property<int>("TorqueValue", 50));
        inputParameters.Value.Value.Add(new Property<int>("Speed", 1500));
        action.Value.Value.Add(inputParameters);
        
        // Erstelle Step mit Action
        var step = new SubmodelElementCollection("Step0001")
        {
            Value = new SubmodelElementCollectionValue(),
            SemanticId = new Reference(
                new Key(KeyType.GlobalReference, "https://admin-shell.io/idta/HierarchicalStructures/Step/1/0"))
        };

        step.Value.Value.Add(I40MessageBuilder.CreateStringProperty("StepTitle", "Assembly Process"));
        step.Value.Value.Add(I40MessageBuilder.CreateStringProperty("Status", "planned"));
        step.Value.Value.Add(I40MessageBuilder.CreateStringProperty("Station", "Station_A"));

        var actions = new SubmodelElementCollection("Actions") { Value = new SubmodelElementCollectionValue() };
        actions.Value.Value.Add(action);
        step.Value.Value.Add(actions);

        var scheduling = new SubmodelElementCollection("Scheduling") { Value = new SubmodelElementCollectionValue() };
        scheduling.Value.Value.Add(new Property<string>("EarliestStartTime", DateTime.UtcNow.AddHours(1).ToString("o")));
        scheduling.Value.Value.Add(new Property<string>("Deadline", DateTime.UtcNow.AddHours(4).ToString("o")));
        scheduling.Value.Value.Add(new Property<int>("EstimatedDuration", 180));
        step.Value.Value.Add(scheduling);
        
        return new I40MessageBuilder()
            .From("ProductHolon_P24")
            .To("ResourceHolon_RH2")
            .WithType(I40MessageTypes.REQUIREMENT)
            .WithConversationId(conversationId)
            .AddElement(step)
            .Build();
    }
    
    private I40Message CreateProposalWithScheduling(string conversationId)
    {
        var scheduling = new SubmodelElementCollection("Scheduling")
        {
            Value = new SubmodelElementCollectionValue(),
            SemanticId = new Reference(
                new Key(KeyType.GlobalReference, "https://smartfactory.de/semantics/Scheduling"))
        };

        scheduling.Value.Value.Add(new Property<string>("StartDateTime", DateTime.UtcNow.AddMinutes(30).ToString("o")));
        scheduling.Value.Value.Add(new Property<string>("EndDateTime", DateTime.UtcNow.AddMinutes(210).ToString("o")));
        scheduling.Value.Value.Add(new Property<int>("SetupTime", 15));
        scheduling.Value.Value.Add(new Property<int>("CycleTime", 165));
        
        return new I40MessageBuilder()
            .From("ResourceHolon_RH2")
            .To("ProductHolon_P24")
            .WithType(I40MessageTypes.PROPOSAL)
            .WithConversationId(conversationId)
            .AddElement(scheduling)
            .AddElement(new Property<bool>("ResourceAvailable", true))
            .AddElement(new Property<double>("EstimatedCost", 45.50))
            .Build();
    }
    
    private SubmodelElementCollection CreateStepRequirement(string capability)
    {
        var collection = new SubmodelElementCollection("RequiredCapability")
        {
            Value = new SubmodelElementCollectionValue()
        };
        collection.Value.Value.Add(I40MessageBuilder.CreateStringProperty("CapabilityName", capability));
        collection.Value.Value.Add(I40MessageBuilder.CreateStringProperty("Priority", "high"));
        collection.Value.Value.Add(new Property<int>("Quantity", 1));
        return collection;
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
