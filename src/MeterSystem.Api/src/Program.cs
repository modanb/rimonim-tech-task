using System.Text.Json;
using MeterSystem.Api.Models;
using MeterSystem.Api.Services;
using MeterSystem.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var rabbitMqOptions = new RabbitMqOptions(
    builder.Configuration.GetValue("RABBITMQ_HOST", "localhost"),
    builder.Configuration.GetValue("RABBITMQ_PORT", 5672),
    builder.Configuration.GetValue("RABBITMQ_USERNAME", "guest"),
    builder.Configuration.GetValue("RABBITMQ_PASSWORD", "guest"),
    builder.Configuration.GetValue("RABBITMQ_QUEUE", "meter_readings")
);

builder.Services.AddSingleton<IRabbitMqPublisher>(_ => new RabbitMqPublisher(rabbitMqOptions));
builder.Services.AddSingleton<ReadingMessageFactory>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/readings", async (MeterReadingsRequest request, IRabbitMqPublisher publisher, ReadingMessageFactory factory) =>
{
    try
    {
        var message = factory.CreateFrom(request);
        await publisher.PublishAsync(message);
        return Results.Accepted();
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/readings/raw", async (RawMeterReadingsRequest request, IRabbitMqPublisher publisher, ReadingMessageFactory factory) =>
{
    try
    {
        var message = factory.CreateFromRaw(request);
        await publisher.PublishAsync(message);
        return Results.Accepted();
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
