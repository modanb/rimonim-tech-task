using MeterSystem.Api.Models;
using MeterSystem.Api.Services;
using Xunit;

namespace MeterSystem.Tests;

public class ReadingMessageFactoryTests
{
    private readonly ReadingMessageFactory _factory = new();

    [Fact]
    public void CreateFrom_ValidRequest_ReturnsMessage()
    {
        var request = new MeterReadingsRequest(
            MeterNumber: 12345,
            Readings: new Dictionary<string, double>
            {
                ["2026-03-18T10:15:00Z"] = 1234.56,
                ["2026-03-18T10:00:00Z"] = 1234.51
            });

        var result = _factory.CreateFrom(request);

        Assert.Equal(12345, result.MeterNumber);
        Assert.Collection(result.Readings,
            item =>
            {
                Assert.Equal(DateTime.Parse("2026-03-18T10:15:00Z").ToUniversalTime(), item.Timestamp);
                Assert.Equal(1234.56, item.Value);
            },
            item =>
            {
                Assert.Equal(DateTime.Parse("2026-03-18T10:00:00Z").ToUniversalTime(), item.Timestamp);
                Assert.Equal(1234.51, item.Value);
            });
    }

    [Fact]
    public void CreateFrom_InvalidTimestamp_ThrowsArgumentException()
    {
        var request = new MeterReadingsRequest(
            MeterNumber: 12345,
            Readings: new Dictionary<string, double>
            {
                ["not-a-timestamp"] = 1234.56
            });

        var exception = Assert.Throws<ArgumentException>(() => _factory.CreateFrom(request));
        Assert.Contains("invalid timestamp format", exception.Message);
    }

    [Fact]
    public void CreateFromRaw_ValidData_ReturnsMessage()
    {
        var request = new RawMeterReadingsRequest(
            MeterNumber: 12345,
            Data: "ChEKBgik9unNBhEK16NwPUqTQAoRCgYIoO/pzQYR16NwPQpKk0A=");

        var result = _factory.CreateFromRaw(request);

        Assert.Equal(12345, result.MeterNumber);
        Assert.Equal(2, result.Readings.Count);
        Assert.Contains(result.Readings, r => r.Timestamp == DateTime.Parse("2026-03-18T10:15:00Z").ToUniversalTime() && r.Value == 1234.56);
        Assert.Contains(result.Readings, r => r.Timestamp == DateTime.Parse("2026-03-18T10:00:00Z").ToUniversalTime() && r.Value == 1234.51);
    }

    [Fact]
    public void CreateFromRaw_InvalidBase64_ThrowsArgumentException()
    {
        var request = new RawMeterReadingsRequest(
            MeterNumber: 12345,
            Data: "not-base64");

        var exception = Assert.Throws<ArgumentException>(() => _factory.CreateFromRaw(request));
        Assert.Contains("data is not valid base64", exception.Message);
    }

    [Fact]
    public void CreateFromRaw_InvalidProtobuf_ThrowsArgumentException()
    {
        var request = new RawMeterReadingsRequest(
            MeterNumber: 12345,
            Data: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("invalid")));

        var exception = Assert.Throws<ArgumentException>(() => _factory.CreateFromRaw(request));
        Assert.Contains("data is not valid protobuf", exception.Message);
    }
}
