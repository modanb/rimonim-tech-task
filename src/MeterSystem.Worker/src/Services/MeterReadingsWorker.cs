using System.Text;
using System.Text.Json;
using MeterSystem.Shared.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MeterSystem.Worker.Services;

public sealed class MeterReadingsWorker : BackgroundService
{
    private readonly ILogger<MeterReadingsWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly PostgresMeterStore _store;

    public MeterReadingsWorker(ILogger<MeterReadingsWorker> logger, IConfiguration configuration, PostgresMeterStore store)
    {
        _logger = logger;
        _configuration = configuration;
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var host = _configuration.GetValue("RABBITMQ_HOST", "localhost");
        var port = _configuration.GetValue("RABBITMQ_PORT", 5672);
        var user = _configuration.GetValue("RABBITMQ_USERNAME", "guest");
        var password = _configuration.GetValue("RABBITMQ_PASSWORD", "guest");
        var queue = _configuration.GetValue("RABBITMQ_QUEUE", "meter_readings");

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = user,
            Password = password
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync(CancellationToken.None);
                await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(false, false, null, null), CancellationToken.None);
                await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: CancellationToken.None);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        var body = delivery.Body.ToArray();
                        var text = Encoding.UTF8.GetString(body);
                        var message = JsonSerializer.Deserialize<MeterReadingsMessage>(text, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (message is null)
                        {
                            _logger.LogWarning("Received empty or malformed queue message; rejecting.");
                            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, CancellationToken.None);
                            return;
                        }

                        await _store.SaveAsync(message, stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, CancellationToken.None);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Invalid JSON in queue message; rejecting.");
                        await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, CancellationToken.None);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Cancellation requested while processing queue message.");
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, true, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process queue message; requeueing.");
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, true, CancellationToken.None);
                    }
                };

                await channel.BasicConsumeAsync(queue, false, consumer, CancellationToken.None);
                _logger.LogInformation("Meter readings worker is consuming queue '{queueName}'...", queue);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker shutdown requested.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker failed to connect or consume queue; retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
