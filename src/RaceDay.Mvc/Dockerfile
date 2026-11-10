# syntax=docker/dockerfile:1
# RaceDay MVC web app.
# Multi-stage build: the SDK image compiles and publishes; only the small ASP.NET runtime image ships.
# Build from this folder:  docker build -t raceday-web src/RaceDay.Mvc

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first, using only the project file, so the NuGet layer is cached until dependencies change.
COPY RaceDay.Mvc.csproj ./
RUN dotnet restore RaceDay.Mvc.csproj

# Then copy the source and publish a Release build.
COPY . .
RUN dotnet publish RaceDay.Mvc.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    # Where the RaceDay API lives. Override at run time with -e RaceDayApi__BaseUrl=...
    RaceDayApi__BaseUrl=http://host.docker.internal:5272

EXPOSE 8080

COPY --from=build /app/publish .

# Run as the image's built-in non-root user.
USER $APP_UID

ENTRYPOINT ["dotnet", "RaceDay.Mvc.dll"]
