using System.Data;
using System.Text.Json;
using Dapper;

namespace IIoT.Collector.Infrastructure;

/// <summary>
/// Обработчик типов для Dapper, который маппит строки из БД в Enums.
/// </summary>
public class EnumStringHandler<T> : SqlMapper.TypeHandler<T>
    where T : struct, Enum
{
    public override void SetValue(IDbDataParameter parameter, T value)
    {
        parameter.Value = value.ToString();
    }

    public override T Parse(object value)
    {
        if (value is string s && Enum.TryParse<T>(s, true, out var result))
        {
            return result;
        }

        if (value == null || value is DBNull)
            throw new DataException($"Cannot parse null to enum {typeof(T).Name}");

        throw new DataException(
            $"Cannot parse '{value}' ({value.GetType()}) to enum {typeof(T).Name}"
        );
    }
}

/// <summary>
/// Обработчик для работы с JSON/JSONB колонками PostgreSQL.
/// Автоматически сериализует/десериализует объекты в JSON строки.
/// </summary>
/// <typeparam name="T">Тип C# объекта для маппинга</typeparam>
public class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.Value = value == null ? DBNull.Value : JsonSerializer.Serialize(value);
    }

    public override T? Parse(object value)
    {
        if (value == null || value is DBNull)
            return default;

        var json = value.ToString();
        return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json);
    }
}
