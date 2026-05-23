using System.Text;
using System.Text.Json;
using MeterSystem.Api.Models;
using MeterSystem.Shared.Models;
using RabbitMQ.Client;

namespace MeterSystem.Api.Services;

public sealed record RabbitMqOptions(
    string HostName,
    int Port,
    string UserName,
    string Password,
    string QueueName
);

public interface IRabbitMqPublisher
{
    Task PublishAsync(MeterReadingsMessage message, CancellationToken cancellationToken = default);
}

public sealed class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly RabbitMqOptions _options;

    public RabbitMqPublisher(RabbitMqOptions options)
    {
        _options = options;
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password
        };

        _connection = factory.CreateConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync(new CreateChannelOptions(false, false, null, null), CancellationToken.None).GetAwaiter().GetResult();
        _channel.QueueDeclareAsync(options.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task PublishAsync(MeterReadingsMessage message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        var body = Encoding.UTF8.GetBytes(payload);

        await _channel.BasicPublishAsync(string.Empty, _options.QueueName, body, cancellationToken);
    }

    public void Dispose()
    {
        _channel?.CloseAsync(CancellationToken.None).GetAwaiter().GetResult();
        _connection?.CloseAsync(CancellationToken.None).GetAwaiter().GetResult();
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
