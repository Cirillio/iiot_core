using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            
            // Настройка нативного маппинга энумов Postgres <-> C#
            // Это решает проблему Error parsing column (String -> Enum)
            var translator = new UpperSnakeCaseNameTranslator();
            builder.MapEnum<SensorDataType>("sensor_data_type", translator);
            builder.MapEnum<ModbusRegisterType>("modbus_register_type", translator);
            builder.MapEnum<ServiceStatus>("system_service_status", translator);

            DataSource = builder.Build();

            // Регистрация хендлеров Dapper для сложных типов (JSON)
            SqlMapper.AddTypeHandler(typeof(SensorUiConfig), new JsonbTypeHandler());
            
            // Хендлеры для энумов, чтобы Dapper не отправлял их как int
            SqlMapper.AddTypeHandler(typeof(SensorDataType), new EnumTypeHandler<SensorDataType>());
            SqlMapper.AddTypeHandler(typeof(ModbusRegisterType), new EnumTypeHandler<ModbusRegisterType>());
            SqlMapper.AddTypeHandler(typeof(ServiceStatus), new EnumTypeHandler<ServiceStatus>());

            _logger.Information(
                "DapperContext initialized with native Enum mapping for {Host}",
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
/// Транслятор имен для Npgsql: PascalCase (C#) to and from UPPER_SNAKE_CASE (Postgres).
/// </summary>
public class UpperSnakeCaseNameTranslator : INpgsqlNameTranslator
{
    public string TranslateMemberName(string clrName) =>
        Regex.Replace(clrName, "(?<!^)([A-Z])", "_$1").ToUpper();

    public string TranslateTypeName(string clrName) => clrName;
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

/// <summary>
/// Хендлер для маппинга перечислений (Enum) в строки (SNAKE_CASE) для PostgreSQL.
/// </summary>
public class EnumTypeHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum
{
    public override void SetValue(IDbDataParameter parameter, T value)
    {
        // Преобразуем PascalCase -> SNAKE_CASE_UPPER для соответствия Postgres ENUM
        var snakeCase = Regex.Replace(value.ToString(), "(?<!^)([A-Z])", "_$1").ToUpper();
        parameter.Value = snakeCase;
        parameter.DbType = DbType.String;
    }

    public override T Parse(object value)
    {
        if (value == null || value is DBNull) return default;
        return Enum.Parse<T>(value.ToString()!.Replace("_", ""), true);
    }
}
