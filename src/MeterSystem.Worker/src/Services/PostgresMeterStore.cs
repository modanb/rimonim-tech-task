using MeterSystem.Shared.Models;
using Npgsql;

namespace MeterSystem.Worker.Services;

public sealed class PostgresMeterStore
{
    private readonly string _connectionString;

    public PostgresMeterStore(IConfiguration configuration)
    {
        var host = configuration.GetValue("POSTGRES_HOST", "localhost");
        var port = configuration.GetValue("POSTGRES_PORT", 5432);
        var database = configuration.GetValue("POSTGRES_DB", "meters");
        var user = configuration.GetValue("POSTGRES_USER", "postgres");
        var password = configuration.GetValue("POSTGRES_PASSWORD", "postgres");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = user,
            Password = password,
            Pooling = true
        };

        _connectionString = builder.ConnectionString;
    }

    public async Task SaveAsync(MeterReadingsMessage message, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var meterId = await GetOrCreateMeterIdAsync(connection, message.MeterNumber, cancellationToken);

        foreach (var reading in message.Readings)
        {
            await InsertReadingAsync(connection, meterId, reading, cancellationToken);
        }
    }

    private static async Task<long> GetOrCreateMeterIdAsync(NpgsqlConnection connection, long meterNumber, CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO meters (meter_number)
VALUES (@meterNumber)
ON CONFLICT (meter_number) DO NOTHING;

SELECT meter_id FROM meters WHERE meter_number = @meterNumber;
";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("meterNumber", meterNumber);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result == DBNull.Value)
        {
            throw new InvalidOperationException("Failed to resolve meter_id.");
        }

        return Convert.ToInt64(result);
    }

    private static async Task InsertReadingAsync(NpgsqlConnection connection, long meterId, MeterReadingDto reading, CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO meter_readings (meter_id, value_at, value)
VALUES (@meterId, @timestamp, @value)
ON CONFLICT DO NOTHING;
";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("meterId", meterId);
        command.Parameters.AddWithValue("timestamp", reading.Timestamp);
        command.Parameters.AddWithValue("value", reading.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
