# 1. Слой сборки
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем файлы проекта и восстанавливаем зависимости
COPY ["modbusClient.csproj", "./"]
RUN dotnet restore "modbusClient.csproj"

# Копируем всё остальное и собираем
COPY . .
RUN dotnet publish "modbusClient.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Финальный слой (Runtime)
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

# Запуск приложения
ENTRYPOINT ["dotnet", "modbusClient.dll"]