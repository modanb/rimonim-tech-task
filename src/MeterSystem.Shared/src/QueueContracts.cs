namespace MeterSystem.Shared.Models;

public sealed record MeterReadingDto(DateTime Timestamp, double Value);
public sealed record MeterReadingsMessage(long MeterNumber, IReadOnlyList<MeterReadingDto> Readings);
