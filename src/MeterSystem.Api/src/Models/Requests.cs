using System.Text.Json.Serialization;

namespace MeterSystem.Api.Models;

public sealed record MeterReadingsRequest(
    [property: JsonPropertyName("meter_number")] long MeterNumber,
    [property: JsonPropertyName("readings")] Dictionary<string, double>? Readings
);

public sealed record RawMeterReadingsRequest(
    [property: JsonPropertyName("meter_number")] long MeterNumber,
    [property: JsonPropertyName("data")] string? Data
);
