# 1. Сборка
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["modbusWebGateway.csproj", "./"]
RUN dotnet restore "modbusWebGateway.csproj"

COPY . .
RUN dotnet publish "modbusWebGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 80
ENTRYPOINT ["dotnet", "modbusWebGateway.dll"]
