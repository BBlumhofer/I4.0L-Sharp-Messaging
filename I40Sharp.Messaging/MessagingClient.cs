using I40Sharp.Messaging.Core;
using I40Sharp.Messaging.Models;
using I40Sharp.Messaging.Transport;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace I40Sharp.Messaging;

/// <summary>
/// Hauptklasse für I4.0 Messaging Client
/// </summary>
public class MessagingClient : IDisposable
{
    private readonly IMessagingTransport _transport;
    private readonly MessageSerializer _serializer;
    private readonly CallbackRegistry _callbackRegistry;
    private readonly ConversationManager _conversationManager;
    private readonly string _defaultTopic;
    private bool _disposed;
    private readonly ILogger _logger;
    // subscription tracking removed — restore direct transport subscriptions

    private readonly object _inboxLock = new();
    private readonly Queue<QueuedMessage> _inbox = new();

    private sealed record QueuedMessage(I40Message Message, string Topic, DateTimeOffset ReceivedAt);
    
    /// <summary>
    /// Event wird ausgelöst wenn die Verbindung hergestellt wurde
    /// </summary>
    public event EventHandler? Connected;
    
    /// <summary>
    /// Event wird ausgelöst wenn die Verbindung getrennt wurde
    /// </summary>
    public event EventHandler? Disconnected;
    
    /// <summary>
    /// Gibt an ob der Client verbunden ist
    /// </summary>
    public bool IsConnected => _transport.IsConnected;

    /// <summary>
    /// Anzahl aktuell gepufferter, noch nicht konsumierter Nachrichten.
    /// </summary>
    public int InboxCount
    {
        get
        {
            lock (_inboxLock)
            {
                return _inbox.Count;
            }
        }
    }
    
    /// <summary>
    /// Erstellt einen neuen MessagingClient
    /// </summary>
    public MessagingClient(IMessagingTransport transport, string defaultTopic = "i40/messages", ILogger? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _serializer = new MessageSerializer();
        _callbackRegistry = new CallbackRegistry();
        _conversationManager = new ConversationManager();
        _defaultTopic = defaultTopic;
        _logger = logger ?? NullLogger<MessagingClient>.Instance;
        
        _transport.MessageReceived += OnTransportMessageReceived;
        _transport.Connected += (s, e) => Connected?.Invoke(this, EventArgs.Empty);
        _transport.Disconnected += (s, e) => Disconnected?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Stellt die Verbindung zum Broker her
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _transport.ConnectAsync(cancellationToken);
        await _transport.SubscribeAsync(_defaultTopic, cancellationToken);
    }
    
    /// <summary>
    /// Trennt die Verbindung zum Broker
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _transport.DisconnectAsync(cancellationToken);
    }
    
    /// <summary>
    /// Sendet eine I4.0 Message
    /// </summary>
    public async Task PublishAsync(I40Message message, string? topic = null, CancellationToken cancellationToken = default)
    {
        var json = _serializer.Serialize(message);
        var targetTopic = topic ?? _defaultTopic;
        
        await _transport.PublishAsync(targetTopic, json, cancellationToken);
        
        // Message zur Conversation hinzufügen
        _conversationManager.AddMessage(message);
    }
    
    /// <summary>
    /// Abonniert ein zusätzliches Topic
    /// </summary>
    public async Task SubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        await _transport.SubscribeAsync(topic, cancellationToken);
    }
    
    /// <summary>
    /// Beendet das Abonnement eines Topics
    /// </summary>
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        await _transport.UnsubscribeAsync(topic, cancellationToken);
    }
    
    /// <summary>
    /// Registriert einen Callback für alle Nachrichten
    /// </summary>
    public void OnMessage(Action<I40Message> callback)
    {
        _callbackRegistry.RegisterGlobalCallback(callback);
    }
    
    /// <summary>
    /// Registriert einen Callback für einen bestimmten Message Type
    /// </summary>
    public void OnMessageType(string messageType, Action<I40Message> callback)
    {
        _callbackRegistry.RegisterMessageTypeCallback(messageType, callback);
    }
    
    /// <summary>
    /// Registriert einen Callback für einen bestimmten Sender
    /// </summary>
    public void OnSender(string senderId, Action<I40Message> callback)
    {
        _callbackRegistry.RegisterSenderCallback(senderId, callback);
    }
    
    /// <summary>
    /// Registriert einen Callback für einen bestimmten Receiver
    /// </summary>
    public void OnReceiver(string receiverId, Action<I40Message> callback)
    {
        _callbackRegistry.RegisterReceiverCallback(receiverId, callback);
    }
    
    /// <summary>
    /// Registriert einen Callback für eine bestimmte Conversation
    /// </summary>
    public void OnConversation(string conversationId, Action<I40Message> callback)
    {
        _callbackRegistry.RegisterConversationCallback(conversationId, callback);
    }

    /// <summary>
    /// Entfernt einen zuvor registrierten Callback für eine Conversation
    /// </summary>
    public void OffConversation(string conversationId, Action<I40Message> callback)
    {
        _callbackRegistry.UnregisterConversationCallback(conversationId, callback);
    }

    /// <summary>
    /// Registriert einen Callback für ein bestimmtes Topic
    /// </summary>
    public void OnTopic(string topic, Action<I40Message, string> callback)
    {
        _callbackRegistry.RegisterTopicCallback(topic, callback);
    }

    /// <summary>
    /// Entfernt einen zuvor registrierten Topic-Callback
    /// </summary>
    public void OffTopic(string topic, Action<I40Message, string> callback)
    {
        _callbackRegistry.UnregisterTopicCallback(topic, callback);
    }

    /// <summary>
    /// Versucht eine Nachricht aus der globalen Inbox zu entnehmen, die auf das Prädikat passt.
    /// Damit gehen Nachrichten nicht verloren, wenn gerade kein Callback registriert ist.
    /// </summary>
    public bool TryDequeueMatching(Func<I40Message, string, bool> predicate, out I40Message message, out string topic)
    {
        return TryDequeueMatching(predicate, out message, out topic, out _);
    }

    /// <summary>
    /// Dequeues all messages that match the given predicate and returns them as a list.
    /// This is useful for consumers that want to bulk-consume buffered messages.
    /// </summary>
    public List<(I40Message Message, string Topic)> DequeueMatchingAll(Func<I40Message, string, bool> predicate)
    {
        var result = new List<(I40Message, string)>();
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        lock (_inboxLock)
        {
            var count = _inbox.Count;
            for (var i = 0; i < count; i++)
            {
                var item = _inbox.Dequeue();
                if (predicate(item.Message, item.Topic))
                {
                    result.Add((item.Message, item.Topic));
                }
                else
                {
                    _inbox.Enqueue(item);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns a snapshot of the current inbox (message + topic) without dequeuing.
    /// Useful for diagnostics.
    /// </summary>
    public List<(I40Message Message, string Topic)> PeekInboxSnapshot()
    {
        var result = new List<(I40Message, string)>();
        lock (_inboxLock)
        {
            foreach (var item in _inbox)
            {
                result.Add((item.Message, item.Topic));
            }
        }

        return result;
    }

    /// <summary>
    /// Versucht eine Nachricht zu entnehmen und liefert zusätzlich den Zeitpunkt, wann sie empfangen wurde.
    /// </summary>
    public bool TryDequeueMatching(
        Func<I40Message, string, bool> predicate,
        out I40Message message,
        out string topic,
        out DateTimeOffset receivedAt)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        lock (_inboxLock)
        {
            var count = _inbox.Count;
            for (var i = 0; i < count; i++)
            {
                var item = _inbox.Dequeue();
                if (predicate(item.Message, item.Topic))
                {
                    message = item.Message;
                    topic = item.Topic;
                    receivedAt = item.ReceivedAt;
                    return true;
                }

                _inbox.Enqueue(item);
            }
        }

        message = null!;
        topic = string.Empty;
        receivedAt = default;
        return false;
    }
    
    /// <summary>
    /// Gibt alle Messages einer Conversation zurück
    /// </summary>
    public List<I40Message> GetConversationMessages(string conversationId)
    {
        return _conversationManager.GetMessages(conversationId);
    }
    
    /// <summary>
    /// Erstellt eine neue Conversation und gibt die ID zurück
    /// </summary>
    public string CreateConversation(TimeSpan? timeout = null)
    {
        return _conversationManager.CreateConversation(timeout);
    }
    
    /// <summary>
    /// Markiert eine Conversation als abgeschlossen
    /// </summary>
    public void CompleteConversation(string conversationId)
    {
        _conversationManager.CompleteConversation(conversationId);
    }
    
    /// <summary>
    /// Entfernt abgelaufene Conversations
    /// </summary>
    public void CleanupExpiredConversations()
    {
        _conversationManager.CleanupExpiredConversations();
    }
    
    private void OnTransportMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        try
        {
            var message = _serializer.Deserialize(e.Payload);
            if (message != null)
            {
                // Debug: ensure we see raw incoming messages and topics during integration tests
                _logger.LogDebug("[MessagingClient] Received on topic='{Topic}' type='{Type}' sender='{Sender}'", e.Topic, message.Frame?.Type, message.Frame?.Sender?.Identification?.Id);
                // Global Queueing: immer puffern, auch wenn gerade kein BT-Knoten wartet.
                lock (_inboxLock)
                {
                    _inbox.Enqueue(new QueuedMessage(message, e.Topic ?? string.Empty, DateTimeOffset.UtcNow));
                    // Basic bound to avoid unbounded growth if consumer is stalled.
                    // Keep the newest messages by dropping oldest.
                    while (_inbox.Count > 10_000)
                    {
                        _inbox.Dequeue();
                    }
                }

                // Message zur Conversation hinzufügen
                _conversationManager.AddMessage(message);
                
                // Callbacks ausführen (inkl. Topic)
                _callbackRegistry.InvokeCallbacks(message, e.Topic ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Verarbeiten der Nachricht");
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _transport?.Dispose();
            _disposed = true;
        }
    }
}
