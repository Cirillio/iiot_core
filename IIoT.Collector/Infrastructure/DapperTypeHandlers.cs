using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;

namespace IIoT.Collector.Infrastructure;

/// <summary>
/// Транслятор имен для Npgsql: PascalCase (C#) to UPPER_SNAKE_CASE (Postgres enum).
/// </summary>
public class CollectorNameTranslator : INpgsqlNameTranslator
{
    public string TranslateMemberName(string clrName) =>
        Regex.Replace(clrName, "(?<!^)([A-Z])", "_$1").ToUpper();

    public string TranslateTypeName(string clrName) => clrName;
}

/// <summary>
/// Обработчик для работы с JSON/JSONB колонками PostgreSQL.
/// </summary>
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
