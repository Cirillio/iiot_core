using System.Data;
using Dapper;
using IIoT.Shared.Models;
using Npgsql;

namespace IIoT.WebApi.Data.TypeHandlers;

public class DapperContext
{
    private readonly string _connectionString;

    public NpgsqlDataSource DataSource { get; }

    public DapperContext(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("AdamMonitoring")
            ?? throw new ArgumentNullException("Connection string 'AdamMonitoring' not found.");

        var builder = new NpgsqlDataSourceBuilder(_connectionString);

        builder.MapEnum<SensorDataType>("sensor_data_type");
        builder.MapEnum<ServiceStatus>("system_service_status");

        DataSource = builder.Build();

        SqlMapper.AddTypeHandler(typeof(SensorUiConfig), new JsonbTypeHandler());
    }

    public IDbConnection CreateConnection() => DataSource.CreateConnection();

    public async Task<bool> CheckConnectionAsync(CancellationToken ct)
    {
        try
        {
            using var conn = await DataSource.OpenConnectionAsync(ct);
            using var cmd = DataSource.CreateCommand("SELECT 1");
            await cmd.ExecuteScalarAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

// Хендлер для работы с JSONB (поле ui_config в sensor_settings)
public class JsonbTypeHandler : SqlMapper.ITypeHandler
{
    public void SetValue(IDbDataParameter parameter, object? value)
    {
        parameter.Value = value is null
            ? DBNull.Value
            : System.Text.Json.JsonSerializer.Serialize(value);
        ((NpgsqlParameter)parameter).NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb;
    }

    public object? Parse(Type destinationType, object value)
    {
        return value is string json
            ? System.Text.Json.JsonSerializer.Deserialize(json, destinationType)
            : null;
    }
}
