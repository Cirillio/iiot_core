# 1. Сборка
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["IIoT.WebApi/IIoT.WebApi.csproj", "IIoT.WebApi/"]
COPY ["IIoT.Shared/IIoT.Shared.csproj", "IIoT.Shared/"]
RUN dotnet restore "IIoT.WebApi/IIoT.WebApi.csproj"

COPY IIoT.WebApi/ IIoT.WebApi/
COPY IIoT.Shared/ IIoT.Shared/
WORKDIR /src/IIoT.WebApi
RUN dotnet publish "IIoT.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Установка системных зависимостей для Npgsql (Kerberos/GSSAPI)
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 80
ENTRYPOINT ["dotnet", "IIoT.WebApi.dll"]