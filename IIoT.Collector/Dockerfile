# 1. Слой сборки
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем файлы проекта и восстанавливаем зависимости
COPY ["IIoT.Collector/IIoT.Collector.csproj", "IIoT.Collector/"]
COPY ["IIoT.Shared/IIoT.Shared.csproj", "IIoT.Shared/"]
RUN dotnet restore "IIoT.Collector/IIoT.Collector.csproj"

# Копируем всё остальное и собираем
COPY IIoT.Collector/ IIoT.Collector/
COPY IIoT.Shared/ IIoT.Shared/
WORKDIR /src/IIoT.Collector
RUN dotnet publish "IIoT.Collector.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Финальный слой (Runtime)
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

# Запуск приложения
ENTRYPOINT ["dotnet", "IIoT.Collector.dll"]
