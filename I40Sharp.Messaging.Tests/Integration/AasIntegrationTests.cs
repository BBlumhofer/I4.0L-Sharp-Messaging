using I40Sharp.Messaging;
using I40Sharp.Messaging.Core;
using I40Sharp.Messaging.Models;
using I40Sharp.Messaging.Transport;
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
    
    [Fact]
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
        var actionsCollection = step.Value.FirstOrDefault(e => e.IdShort == "Actions");
        Assert.NotNull(actionsCollection);
        Assert.IsType<SubmodelElementCollection>(actionsCollection);
        
        // Cleanup
        await sender.DisconnectAsync();
        await receiver.DisconnectAsync();
    }
    
    [Fact]
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
        var startTime = scheduling.Value.FirstOrDefault(e => e.IdShort == "StartDateTime") as Property;
        Assert.NotNull(startTime);
        Assert.NotNull(startTime.Value);
        
        // Cleanup
        await resource.DisconnectAsync();
        await product.DisconnectAsync();
    }
    
    [Fact]
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
            .ReplyingTo(cfp.Frame.MessageId!)
            .AddElement(new Property
            {
                IdShort = "EstimatedCost",
                Value = "45.0",
                ValueType = "xs:double"
            })
            .AddElement(new Property
            {
                IdShort = "EstimatedDuration",
                Value = "120",
                ValueType = "xs:integer"
            })
            .Build();
        
        var proposal2 = new I40MessageBuilder()
            .From("ResourceHolon_RH3")
            .To("ProductHolon_P24")
            .WithType(I40MessageTypes.PROPOSAL)
            .WithConversationId(conversationId)
            .ReplyingTo(cfp.Frame.MessageId!)
            .AddElement(new Property
            {
                IdShort = "EstimatedCost",
                Value = "42.0",
                ValueType = "xs:double"
            })
            .AddElement(new Property
            {
                IdShort = "EstimatedDuration",
                Value = "140",
                ValueType = "xs:integer"
            })
            .Build();
        
        await resource1.PublishAsync(proposal1, "factory/negotiation");
        await resource2.PublishAsync(proposal2, "factory/negotiation");
        
        // Warte auf beide Proposals
        var receivedBoth = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;
        Assert.True(receivedBoth, "Nicht alle Proposals empfangen");
        Assert.Equal(2, proposalsReceived.Count);
        
        // Phase 3: Product wählt bestes Angebot (RH3 - niedrigerer Preis)
        var bestProposal = proposalsReceived
            .OrderBy(p => double.Parse(
                ((Property)p.InteractionElements.First(e => e.IdShort == "EstimatedCost")).Value ?? "999"))
            .First();
        
        var acceptance = new I40MessageBuilder()
            .From("ProductHolon_P24")
            .To(bestProposal.Frame.Sender.Identification.Id)
            .WithType(I40MessageTypes.ACCEPT_PROPOSAL)
            .WithConversationId(conversationId)
            .ReplyingTo(bestProposal.Frame.MessageId!)
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
        var action = new SubmodelElementCollection
        {
            IdShort = "Action001",
            ModelType = "SubmodelElementCollection",
            SemanticId = new SemanticId
            {
                Keys = new List<Key>
                {
                    new Key { Type = "GlobalReference", Value = "https://admin-shell.io/idta/HierarchicalStructures/Action/1/0" }
                }
            },
            Value = new List<SubmodelElement>
            {
                new Property
                {
                    IdShort = "ActionTitle",
                    Value = "Assemble",
                    ValueType = "xs:string"
                },
                new Property
                {
                    IdShort = "Status",
                    Value = "planned",
                    ValueType = "xs:string"
                },
                new Property
                {
                    IdShort = "MachineName",
                    Value = "AssemblyStation_01",
                    ValueType = "xs:string"
                },
                new SubmodelElementCollection
                {
                    IdShort = "InputParameters",
                    Value = new List<SubmodelElement>
                    {
                        new Property { IdShort = "TorqueValue", Value = "50", ValueType = "xs:integer" },
                        new Property { IdShort = "Speed", Value = "1500", ValueType = "xs:integer" }
                    }
                }
            }
        };
        
        // Erstelle Step mit Action
        var step = new SubmodelElementCollection
        {
            IdShort = "Step0001",
            ModelType = "SubmodelElementCollection",
            SemanticId = new SemanticId
            {
                Keys = new List<Key>
                {
                    new Key { Type = "GlobalReference", Value = "https://admin-shell.io/idta/HierarchicalStructures/Step/1/0" }
                }
            },
            Value = new List<SubmodelElement>
            {
                new Property
                {
                    IdShort = "StepTitle",
                    Value = "Assembly Process",
                    ValueType = "xs:string"
                },
                new Property
                {
                    IdShort = "Status",
                    Value = "planned",
                    ValueType = "xs:string"
                },
                new Property
                {
                    IdShort = "Station",
                    Value = "Station_A",
                    ValueType = "xs:string"
                },
                new SubmodelElementCollection
                {
                    IdShort = "Actions",
                    Value = new List<SubmodelElement> { action }
                },
                new SubmodelElementCollection
                {
                    IdShort = "Scheduling",
                    Value = new List<SubmodelElement>
                    {
                        new Property { IdShort = "EarliestStartTime", Value = DateTime.UtcNow.AddHours(1).ToString("o"), ValueType = "xs:dateTime" },
                        new Property { IdShort = "Deadline", Value = DateTime.UtcNow.AddHours(4).ToString("o"), ValueType = "xs:dateTime" },
                        new Property { IdShort = "EstimatedDuration", Value = "180", ValueType = "xs:integer" }
                    }
                }
            }
        };
        
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
        var scheduling = new SubmodelElementCollection
        {
            IdShort = "Scheduling",
            SemanticId = new SemanticId
            {
                Keys = new List<Key>
                {
                    new Key { Type = "GlobalReference", Value = "https://smartfactory.de/semantics/Scheduling" }
                }
            },
            Value = new List<SubmodelElement>
            {
                new Property
                {
                    IdShort = "StartDateTime",
                    Value = DateTime.UtcNow.AddMinutes(30).ToString("o"),
                    ValueType = "xs:dateTime"
                },
                new Property
                {
                    IdShort = "EndDateTime",
                    Value = DateTime.UtcNow.AddMinutes(210).ToString("o"),
                    ValueType = "xs:dateTime"
                },
                new Property
                {
                    IdShort = "SetupTime",
                    Value = "15",
                    ValueType = "xs:integer"
                },
                new Property
                {
                    IdShort = "CycleTime",
                    Value = "165",
                    ValueType = "xs:integer"
                }
            }
        };
        
        return new I40MessageBuilder()
            .From("ResourceHolon_RH2")
            .To("ProductHolon_P24")
            .WithType(I40MessageTypes.PROPOSAL)
            .WithConversationId(conversationId)
            .AddElement(scheduling)
            .AddElement(new Property
            {
                IdShort = "ResourceAvailable",
                Value = "true",
                ValueType = "xs:boolean"
            })
            .AddElement(new Property
            {
                IdShort = "EstimatedCost",
                Value = "45.50",
                ValueType = "xs:double"
            })
            .Build();
    }
    
    private SubmodelElementCollection CreateStepRequirement(string capability)
    {
        return new SubmodelElementCollection
        {
            IdShort = "RequiredCapability",
            Value = new List<SubmodelElement>
            {
                new Property
                {
                    IdShort = "CapabilityName",
                    Value = capability,
                    ValueType = "xs:string"
                },
                new Property
                {
                    IdShort = "Priority",
                    Value = "high",
                    ValueType = "xs:string"
                },
                new Property
                {
                    IdShort = "Quantity",
                    Value = "1",
                    ValueType = "xs:integer"
                }
            }
        };
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
