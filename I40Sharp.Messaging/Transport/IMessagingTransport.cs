using I40Sharp.Messaging.Models;

namespace I40Sharp.Messaging.Transport;

/// <summary>
/// Interface für Transport-Layer Abstraction
/// </summary>
public interface IMessagingTransport : IDisposable
{
    /// <summary>
    /// Event wird ausgelöst wenn eine Nachricht empfangen wurde
    /// </summary>
    event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    
    /// <summary>
    /// Event wird ausgelöst wenn die Verbindung hergestellt wurde
    /// </summary>
    event EventHandler? Connected;
    
    /// <summary>
    /// Event wird ausgelöst wenn die Verbindung getrennt wurde
    /// </summary>
    event EventHandler? Disconnected;
    
    /// <summary>
    /// Gibt an ob die Verbindung aktiv ist
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Stellt die Verbindung her
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Trennt die Verbindung
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sendet eine Nachricht
    /// </summary>
    Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Abonniert ein Topic
    /// </summary>
    Task SubscribeAsync(string topic, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Beendet das Abonnement eines Topics
    /// </summary>
    Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default);
}

/// <summary>
/// EventArgs für empfangene Nachrichten
/// </summary>
public class MessageReceivedEventArgs : EventArgs
{
    public string Topic { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}
