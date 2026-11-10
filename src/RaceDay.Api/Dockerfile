# syntax=docker/dockerfile:1
# RaceDay API (Part 2), containerised so docker-compose can run the whole system.
# Build from this folder:  docker build -t raceday-api src/RaceDay.Api

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY RaceDay.Api.csproj ./
RUN dotnet restore RaceDay.Api.csproj

COPY . .
RUN dotnet publish RaceDay.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "RaceDay.Api.dll"]
