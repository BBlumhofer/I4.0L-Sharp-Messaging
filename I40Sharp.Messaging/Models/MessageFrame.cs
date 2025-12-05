using System.Text.Json.Serialization;

namespace I40Sharp.Messaging.Models;

/// <summary>
/// Repräsentiert eine Rolle eines Agenten
/// </summary>
public class Role
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Repräsentiert die Identifikation eines Agenten
/// </summary>
public class Identification
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("idType")]
    public string? IdType { get; set; }
}

/// <summary>
/// Repräsentiert Sender oder Receiver eines Message Frames
/// </summary>
public class Participant
{
    [JsonPropertyName("identification")]
    public Identification Identification { get; set; } = new();
    
    [JsonPropertyName("role")]
    public Role Role { get; set; } = new();
}

/// <summary>
/// Repräsentiert den Frame einer I4.0 Message
/// </summary>
public class MessageFrame
{
    [JsonPropertyName("sender")]
    public Participant Sender { get; set; } = new();
    
    [JsonPropertyName("receiver")]
    public Participant Receiver { get; set; } = new();
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("conversationId")]
    public string ConversationId { get; set; } = string.Empty;
    
    [JsonPropertyName("messageId")]
    public string? MessageId { get; set; }
    
    [JsonPropertyName("replyTo")]
    public string? ReplyTo { get; set; }
    
    [JsonPropertyName("replyBy")]
    public DateTime? ReplyBy { get; set; }
}
