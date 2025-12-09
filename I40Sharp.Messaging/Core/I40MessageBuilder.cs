using I40Sharp.Messaging.Models;
using BaSyx.Models.AdminShell;

namespace I40Sharp.Messaging.Core;

/// <summary>
/// Builder-Klasse für einfache Erstellung von I4.0 Messages
/// </summary>
public class I40MessageBuilder
{
    private readonly I40Message _message = new();
    
    public I40MessageBuilder()
    {
        _message.Frame.ConversationId = Guid.NewGuid().ToString();
    }
    
    /// <summary>
    /// Setzt den Sender der Nachricht
    /// </summary>
    public I40MessageBuilder From(string senderId, string? role = null)
    {
        _message.Frame.Sender = new Participant
        {
            Identification = new Identification { Id = senderId },
            Role = new Role { Name = role ?? string.Empty }
        };
        return this;
    }
    
    /// <summary>
    /// Setzt den Empfänger der Nachricht
    /// </summary>
    public I40MessageBuilder To(string receiverId, string? role = null)
    {
        _message.Frame.Receiver = new Participant
        {
            Identification = new Identification { Id = receiverId },
            Role = new Role { Name = role ?? string.Empty }
        };
        return this;
    }
    
    /// <summary>
    /// Setzt den Message Type
    /// </summary>
    public I40MessageBuilder WithType(string messageType)
    {
        _message.Frame.Type = messageType;
        return this;
    }
    
    /// <summary>
    /// Setzt eine spezifische ConversationId
    /// </summary>
    public I40MessageBuilder WithConversationId(string conversationId)
    {
        _message.Frame.ConversationId = conversationId;
        return this;
    }
    
    // Note: MessageId and ReplyTo are deprecated/removed from protocol; use ConversationId for correlation.
    
    /// <summary>
    /// Setzt eine Deadline für die Antwort
    /// </summary>
    public I40MessageBuilder ReplyBy(DateTime deadline)
    {
        _message.Frame.ReplyBy = deadline;
        return this;
    }
    
    /// <summary>
    /// Fügt ein SubmodelElement zu den InteractionElements hinzu
    /// </summary>
    public I40MessageBuilder AddElement(SubmodelElement element)
    {
        _message.InteractionElements.Add(element);
        return this;
    }
    
    /// <summary>
    /// Fügt mehrere SubmodelElements hinzu
    /// </summary>
    public I40MessageBuilder AddElements(IEnumerable<SubmodelElement> elements)
    {
        _message.InteractionElements.AddRange(elements);
        return this;
    }
    
    /// <summary>
    /// Erstellt die fertige I4.0 Message
    /// </summary>
    public I40Message Build()
    {
        ValidateMessage();
        return _message;
    }
    
    private void ValidateMessage()
    {
        if (string.IsNullOrEmpty(_message.Frame.Sender.Identification.Id))
            throw new InvalidOperationException("Sender ID ist erforderlich");
        
        if (string.IsNullOrEmpty(_message.Frame.Receiver.Identification.Id))
            throw new InvalidOperationException("Receiver ID ist erforderlich");
        
        if (string.IsNullOrEmpty(_message.Frame.Type))
            throw new InvalidOperationException("Message Type ist erforderlich");
        
        if (string.IsNullOrEmpty(_message.Frame.ConversationId))
            throw new InvalidOperationException("ConversationId ist erforderlich");
    }
}
