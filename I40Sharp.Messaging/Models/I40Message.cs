using System.Text.Json.Serialization;
using BaSyx.Models.AdminShell;

namespace I40Sharp.Messaging.Models;

/// <summary>
/// Repräsentiert eine vollständige I4.0 Message
/// </summary>
public class I40Message
{
    [JsonPropertyName("frame")]
    public MessageFrame Frame { get; set; } = new();
    
    [JsonPropertyName("interactionElements")]
    public List<ISubmodelElement> InteractionElements { get; set; } = new();
    
    /// <summary>
    /// Zeitstempel wann die Nachricht erstellt wurde
    /// </summary>
    [JsonIgnore]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Zeitstempel wann die Nachricht empfangen wurde
    /// </summary>
    [JsonIgnore]
    public DateTime? ReceivedAt { get; set; }
}
