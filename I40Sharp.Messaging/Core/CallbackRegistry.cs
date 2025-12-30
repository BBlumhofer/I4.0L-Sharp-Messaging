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
    /// Registriert einen Callback für ein bestimmtes Topic
    /// </summary>
    public void RegisterTopicCallback(string topic, Action<I40Message, string> callback)
    {
        lock (_lock)
        {
            _registrations.Add(new CallbackRegistration
            {
                TopicCallback = callback,
                Type = CallbackType.Topic,
                Topic = topic
            });
        }
    }
    
    /// <summary>
    /// Ruft alle passenden Callbacks für eine Nachricht auf
    /// </summary>
    public void InvokeCallbacks(I40Message message, string topic)
    {
        List<CallbackRegistration> matchingCallbacks;

        lock (_lock)
        {
            // Show how many callbacks are registered (debug) and how many match (trace)
            _logger.LogDebug("[CallbackRegistry] RegisteredCallbacks={Count}", _registrations.Count);
            matchingCallbacks = _registrations.Where(r => r.Matches(message, topic)).ToList();
            _logger.LogTrace("[CallbackRegistry] MatchingCallbacks={Count} for type={Type} topic={Topic}", matchingCallbacks.Count, message.Frame?.Type, topic);
        }

        foreach (var registration in matchingCallbacks)
        {
            try
            {
                if (registration.Type == CallbackType.Topic && registration.TopicCallback != null)
                {
                    registration.TopicCallback(message, topic);
                }
                else
                {
                    registration.Callback(message);
                }
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

    /// <summary>
    /// Entfernt alle Callback-Registrierungen für eine bestimmte Conversation und Callback-Delegate.
    /// </summary>
    public void UnregisterConversationCallback(string conversationId, Action<I40Message> callback)
    {
        lock (_lock)
        {
            _registrations.RemoveAll(r => r.Type == CallbackType.Conversation && string.Equals(r.ConversationId, conversationId, StringComparison.Ordinal) && r.Callback == callback);
        }
    }

    /// <summary>
    /// Entfernt alle Callback-Registrierungen für ein bestimmtes Topic.
    /// </summary>
    public void UnregisterTopicCallback(string topic, Action<I40Message, string> callback)
    {
        lock (_lock)
        {
            _registrations.RemoveAll(r => r.Type == CallbackType.Topic && string.Equals(r.Topic, topic, StringComparison.OrdinalIgnoreCase) && r.TopicCallback == callback);
        }
    }
    
        private enum CallbackType
    {
        Global,
        MessageType,
        Sender,
        Receiver,
            Conversation,
            Topic
    }
    
    private class CallbackRegistration
    {
        public Action<I40Message> Callback { get; set; } = null!;
        public Action<I40Message, string>? TopicCallback { get; set; }
        public CallbackType Type { get; set; }
        public string? MessageType { get; set; }
        public string? SenderId { get; set; }
        public string? ReceiverId { get; set; }
        public string? ConversationId { get; set; }
        public string? Topic { get; set; }
        
        public bool Matches(I40Message message, string topic)
        {
            return Type switch
            {
                CallbackType.Global => true,
                CallbackType.MessageType => message.Frame?.Type == MessageType,
                CallbackType.Sender => message.Frame?.Sender?.Identification?.Id == SenderId,
                CallbackType.Receiver => message.Frame?.Receiver?.Identification?.Id == ReceiverId,
                CallbackType.Conversation => message.Frame?.ConversationId == ConversationId,
                CallbackType.Topic => string.Equals(topic, Topic, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }
}
