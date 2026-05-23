using MeterSystem.Api.Models;
using MeterSystem.Api.Protos;
using MeterSystem.Shared.Models;
using MeterDataProto = MeterSystem.Api.Protos.MeterData;

namespace MeterSystem.Api.Services;

public sealed class ReadingMessageFactory
{
    public MeterReadingsMessage CreateFrom(MeterReadingsRequest request)
    {
        if (request.Readings is null || request.Readings.Count == 0)
        {
            throw new ArgumentException("readings are required", nameof(request.Readings));
        }

        var readings = new List<MeterReadingDto>();
        foreach (var (timestamp, value) in request.Readings)
        {
            if (!DateTimeOffset.TryParse(timestamp, out var parsed))
            {
                throw new ArgumentException($"invalid timestamp format: {timestamp}", nameof(request.Readings));
            }

            readings.Add(new MeterReadingDto(parsed.UtcDateTime, value));
        }

        return new MeterReadingsMessage(request.MeterNumber, readings);
    }

    public MeterReadingsMessage CreateFromRaw(RawMeterReadingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Data))
        {
            throw new ArgumentException("data is required", nameof(request.Data));
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(request.Data);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("data is not valid base64", nameof(request.Data), ex);
        }

        MeterDataProto? decoded;
        try
        {
            decoded = MeterDataProto.Parser.ParseFrom(payload);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("data is not valid protobuf", nameof(request.Data), ex);
        }

        var readings = decoded.Readings
            .Select(r => new MeterReadingDto(r.Timestamp.ToDateTime().ToUniversalTime(), r.Value))
            .ToList();

        return new MeterReadingsMessage(request.MeterNumber, readings);
    }
}
