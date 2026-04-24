using System.Data;
using System.Text.Json;
using Dapper;
using IIoT.Shared.Models;
using Npgsql;
using Serilog;

namespace IIoT.WebApi.Data.TypeHandlers;

/// <summary>
/// Контекст для работы с Dapper.
/// Инициализирует соединение с PostgreSQL и регистрирует кастомные преобразователи типов.
/// </summary>
public class DapperContext
{
    private readonly string _connectionString;
    private readonly Serilog.ILogger _logger = Log.ForContext<DapperContext>();

    /// <summary>
    /// Источник данных Npgsql для создания соединений.
    /// </summary>
    public NpgsqlDataSource DataSource { get; }

    public DapperContext(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("AdamMonitoring")
            ?? throw new ArgumentNullException("Connection string 'AdamMonitoring' not found.");

        // КРИТИЧЕСКИ ВАЖНО: Включаем маппинг snake_case (БД) -> PascalCase (C#)
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        try
        {
            var builder = new NpgsqlDataSourceBuilder(_connectionString);
            DataSource = builder.Build();

            // Регистрация глобальных хендлеров Dapper
            SqlMapper.AddTypeHandler(typeof(SensorUiConfig), new JsonbTypeHandler());
            SqlMapper.AddTypeHandler(typeof(SensorDataType), new EnumTypeHandler<SensorDataType>());
            SqlMapper.AddTypeHandler(typeof(ServiceStatus), new EnumTypeHandler<ServiceStatus>());

            _logger.Information(
                "DapperContext initialized for {Host}",
                new NpgsqlConnectionStringBuilder(_connectionString).Host
            );
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize NpgsqlDataSource");
            throw;
        }
    }

    public IDbConnection CreateConnection() => DataSource.CreateConnection();
}

/// <summary>
/// Преобразователь для работы с перечислениями (Enum).
/// </summary>
public class EnumTypeHandler<T> : SqlMapper.ITypeHandler
    where T : struct, Enum
{
    public void SetValue(IDbDataParameter parameter, object? value)
    {
        parameter.Value = value?.ToString();
    }

    public object Parse(Type destinationType, object value)
    {
        if (value is null || value is DBNull)
            return default(T);
        var valueString = value.ToString();
        return Enum.TryParse<T>(valueString, true, out var result) ? result : default(T);
    }
}

/// <summary>
/// Преобразователь для работы с типом JSONB в PostgreSQL.
/// </summary>
public class JsonbTypeHandler : SqlMapper.ITypeHandler
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public void SetValue(IDbDataParameter parameter, object? value)
    {
        parameter.Value = value is null ? DBNull.Value : JsonSerializer.Serialize(value, _options);

        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb;
        }
    }

    public object? Parse(Type destinationType, object value)
    {
        if (value is null || value is DBNull)
            return null;

        string? json = value switch
        {
            string s => s,
            JsonElement element => element.GetRawText(),
            _ => value.ToString(),
        };

        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize(json, destinationType, _options);
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "Failed to deserialize JSONB to {Type}. Raw value: {Raw}",
                destinationType.Name,
                json
            );
            return null;
        }
    }
}
