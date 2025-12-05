using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace I40Sharp.Messaging.Transport;

/// <summary>
/// MQTT Transport Implementierung basierend auf MQTTnet
/// </summary>
public class MqttTransport : IMessagingTransport
{
    private readonly IMqttClient _mqttClient;
    private readonly MqttClientOptions _options;
    private bool _disposed;
    
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    
    public bool IsConnected => _mqttClient?.IsConnected ?? false;
    
    public MqttTransport(string broker, int port = 1883, string? clientId = null, string? username = null, string? password = null)
    {
        // MQTTnet 4.x verwendet MqttFactory statt MqttClientFactory
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();
        
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(broker, port)
            .WithClientId(clientId ?? Guid.NewGuid().ToString())
            .WithCleanSession();
        
        if (!string.IsNullOrEmpty(username))
        {
            optionsBuilder.WithCredentials(username, password);
        }
        
        _options = optionsBuilder.Build();
        
        // Event-Handler registrieren
        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }
    
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            await _mqttClient.ConnectAsync(_options, cancellationToken);
        }
    }
    
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
        }
    }
    
    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("MQTT Client ist nicht verbunden");
        }
        
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();
        
        await _mqttClient.PublishAsync(message, cancellationToken);
    }
    
    public async Task SubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("MQTT Client ist nicht verbunden");
        }
        
        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(topic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build();
        
        await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);
    }
    
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("MQTT Client ist nicht verbunden");
        }
        
        var unsubscribeOptions = new MqttClientUnsubscribeOptionsBuilder()
            .WithTopicFilter(topic)
            .Build();
        
        await _mqttClient.UnsubscribeAsync(unsubscribeOptions, cancellationToken);
    }
    
    private Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        Connected?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
    
    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        Disconnected?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
    
    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = System.Text.Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
        
        MessageReceived?.Invoke(this, new MessageReceivedEventArgs
        {
            Topic = topic,
            Payload = payload
        });
        
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _mqttClient?.Dispose();
            _disposed = true;
        }
    }
}
