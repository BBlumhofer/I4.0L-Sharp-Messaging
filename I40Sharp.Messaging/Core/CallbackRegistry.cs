using I40Sharp.Messaging.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace I40Sharp.Messaging.Core;

/// <summary>
/// Verwaltet Message Callbacks und Routing
/// </summary>
public class CallbackRegistry
{
    private readonly List<CallbackRegistration> _registrations = new();
    private readonly object _lock = new();
    private readonly ILogger<CallbackRegistry> _logger;

    public CallbackRegistry(ILogger<CallbackRegistry>? logger = null)
    {
        _logger = logger ?? NullLogger<CallbackRegistry>.Instance;
    }
    
    /// <summary>
    /// Registriert einen globalen Callback für alle Nachrichten
    /// </summary>
    public void RegisterGlobalCallback(Action<I40Message> callback)
    {
        lock (_lock)
        {
            _registrations.Add(new CallbackRegistration
            {
                Callback = callback,
                Type = CallbackType.Global
            });
        }
    }
    
    /// <summary>
    /// Registriert einen Callback für einen bestimmten Message Type
    /// </summary>
    public void RegisterMessageTypeCallback(string messageType, Action<I40Message> callback)
    {
        lock (_lock)
        {
            _registrations.Add(new CallbackRegistration
            {
                Callback = callback,
                Type = CallbackType.MessageType,
                MessageType = messageType
            });
        }
    }
    
    /// <summary>
    /// Registriert einen Callback für einen bestimmten Sender
    /// </summary>
    public void RegisterSenderCallback(string senderId, Action<I40Message> callback)
    {
        lock (_lock)
        {
            _registrations.Add(new CallbackRegistration
            {
                Callback = callback,
                Type = CallbackType.Sender,
                SenderId = senderId
            });
        }
    }
    
    /// <summary>
    /// Registriert einen Callback für einen bestimmten Receiver
    /// </summary>
    public void RegisterReceiverCallback(string receiverId, Action<I40Message> callback)
    {
        lock (_lock)
        {
            _registrations.Add(new CallbackRegistration
            {
                Callback = callback,
                Type = CallbackType.Receiver,
                ReceiverId = receiverId
            });
        }
    }
    
    /// <summary>
    /// Registriert einen Callback für eine bestimmte Conversation
    /// </summary>
    public void RegisterConversationCallback(string conversationId, Action<I40Message> callback)
    {
        lock (_lock)
        {
            _registrations.Add(new CallbackRegistration
            {
                Callback = callback,
                Type = CallbackType.Conversation,
                ConversationId = conversationId
            });
        }
    }
    
    /// <summary>
    /// Ruft alle passenden Callbacks für eine Nachricht auf
    /// </summary>
    public void InvokeCallbacks(I40Message message)
    {
        List<CallbackRegistration> matchingCallbacks;

        lock (_lock)
        {
            // Show how many callbacks are registered (debug) and how many match (trace)
            _logger.LogDebug("[CallbackRegistry] RegisteredCallbacks={Count}", _registrations.Count);

            matchingCallbacks = _registrations.Where(r => r.Matches(message)).ToList();
            _logger.LogTrace("[CallbackRegistry] MatchingCallbacks={Count} for type={Type}", matchingCallbacks.Count, message.Frame?.Type);
        }

        foreach (var registration in matchingCallbacks)
        {
            try
            {
                registration.Callback(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Ausführen des Callbacks");
            }
        }
    }
    
    /// <summary>
    /// Entfernt alle Callbacks
    /// </summary>
    public void ClearCallbacks()
    {
        lock (_lock)
        {
            _registrations.Clear();
        }
    }
    
    private enum CallbackType
    {
        Global,
        MessageType,
        Sender,
        Receiver,
        Conversation
    }
    
    private class CallbackRegistration
    {
        public Action<I40Message> Callback { get; set; } = null!;
        public CallbackType Type { get; set; }
        public string? MessageType { get; set; }
        public string? SenderId { get; set; }
        public string? ReceiverId { get; set; }
        public string? ConversationId { get; set; }
        
        public bool Matches(I40Message message)
        {
            return Type switch
            {
                CallbackType.Global => true,
                CallbackType.MessageType => message.Frame.Type == MessageType,
                CallbackType.Sender => message.Frame.Sender.Identification.Id == SenderId,
                CallbackType.Receiver => message.Frame.Receiver.Identification.Id == ReceiverId,
                CallbackType.Conversation => message.Frame.ConversationId == ConversationId,
                _ => false
            };
        }
    }
}
