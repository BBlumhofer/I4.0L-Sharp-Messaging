using I40Sharp.Messaging.Models;

namespace I40Sharp.Messaging.Core;

/// <summary>
/// Verwaltet Conversations und deren Messages
/// </summary>
public class ConversationManager
{
    private readonly Dictionary<string, Conversation> _conversations = new();
    private readonly object _lock = new();
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(30);
    
    /// <summary>
    /// Erstellt eine neue Conversation
    /// </summary>
    public string CreateConversation(TimeSpan? timeout = null)
    {
        var conversationId = Guid.NewGuid().ToString();
        lock (_lock)
        {
            _conversations[conversationId] = new Conversation
            {
                ConversationId = conversationId,
                CreatedAt = DateTime.UtcNow,
                Timeout = timeout ?? _defaultTimeout
            };
        }
        return conversationId;
    }
    
    /// <summary>
    /// Fügt eine Nachricht zu einer Conversation hinzu
    /// </summary>
    public void AddMessage(I40Message message)
    {
        var conversationId = message.Frame.ConversationId;
        
        lock (_lock)
        {
            if (!_conversations.ContainsKey(conversationId))
            {
                _conversations[conversationId] = new Conversation
                {
                    ConversationId = conversationId,
                    CreatedAt = DateTime.UtcNow,
                    Timeout = _defaultTimeout
                };
            }
            
            _conversations[conversationId].Messages.Add(message);
            _conversations[conversationId].LastActivity = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Gibt alle Messages einer Conversation zurück
    /// </summary>
    public List<I40Message> GetMessages(string conversationId)
    {
        lock (_lock)
        {
            return _conversations.TryGetValue(conversationId, out var conversation)
                ? new List<I40Message>(conversation.Messages)
                : new List<I40Message>();
        }
    }
    
    /// <summary>
    /// Gibt die letzte Message einer Conversation zurück
    /// </summary>
    public I40Message? GetLastMessage(string conversationId)
    {
        lock (_lock)
        {
            return _conversations.TryGetValue(conversationId, out var conversation) && conversation.Messages.Count > 0
                ? conversation.Messages[^1]
                : null;
        }
    }
    
    /// <summary>
    /// Prüft ob eine Conversation existiert
    /// </summary>
    public bool ConversationExists(string conversationId)
    {
        lock (_lock)
        {
            return _conversations.ContainsKey(conversationId);
        }
    }
    
    /// <summary>
    /// Markiert eine Conversation als abgeschlossen
    /// </summary>
    public void CompleteConversation(string conversationId)
    {
        lock (_lock)
        {
            if (_conversations.TryGetValue(conversationId, out var conversation))
            {
                conversation.IsCompleted = true;
            }
        }
    }
    
    /// <summary>
    /// Entfernt abgelaufene Conversations
    /// </summary>
    public void CleanupExpiredConversations()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var expiredIds = _conversations
                .Where(kvp => kvp.Value.IsCompleted || 
                             (now - kvp.Value.LastActivity) > kvp.Value.Timeout)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var id in expiredIds)
            {
                _conversations.Remove(id);
            }
        }
    }
    
    /// <summary>
    /// Gibt die Anzahl der aktiven Conversations zurück
    /// </summary>
    public int GetActiveConversationCount()
    {
        lock (_lock)
        {
            return _conversations.Count(kvp => !kvp.Value.IsCompleted);
        }
    }
    
    private class Conversation
    {
        public string ConversationId { get; set; } = string.Empty;
        public List<I40Message> Messages { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public TimeSpan Timeout { get; set; }
        public bool IsCompleted { get; set; }
    }
}
