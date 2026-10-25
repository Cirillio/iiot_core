namespace IIoT.Shared.Configuration;

/// <summary>
/// Локальный запуск вне Docker: подтягивает переменные из .env и собирает строку подключения к БД.
/// В Docker строка приходит из ConnectionStrings__* (см. docker-compose.yml), и этот код не задействуется.
/// </summary>
public static class LocalEnv
{
    /// <summary>
    /// Ищет .env от текущей папки вверх по дереву и загружает пары KEY=VALUE в окружение процесса.
    /// Уже заданные переменные не перезаписываются: реальное окружение приоритетнее файла.
    /// </summary>
    public static void Load()
    {
        var path = FindEnvFile(Directory.GetCurrentDirectory()) ?? FindEnvFile(AppContext.BaseDirectory);
        if (path is null)
            return;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"', '\'');

            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>
    /// Собирает строку подключения к PostgreSQL из POSTGRES_USER / POSTGRES_PASSWORD / POSTGRES_DB.
    /// Обязательные значения без дефолтов: нет переменной, значит падаем при старте.
    /// </summary>
    public static string BuildPostgresConnectionString(string extraOptions = "")
    {
        var user = Require("POSTGRES_USER");
        var password = Require("POSTGRES_PASSWORD");
        var database = Require("POSTGRES_DB");
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "127.0.0.1";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";

        return $"Host={host};Port={port};Database={database};Username={user};Password={password};{extraOptions}";
    }

    private static string Require(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Переменная окружения {name} не задана (см. .env.example).");

    private static string? FindEnvFile(string startDir)
    {
        for (var dir = new DirectoryInfo(startDir); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
