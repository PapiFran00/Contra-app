# syntax=docker/dockerfile:1

# Build and publish in the SDK image; only the published output reaches runtime.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore separately so dependency layers remain cached when application code changes.
COPY ["ContraApp.csproj", "./"]
COPY ["NuGet.Config", "./"]
RUN dotnet restore "ContraApp.csproj"

COPY . .
RUN dotnet publish "ContraApp.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish ./

# Program.cs persists Data Protection keys here. Make it writable by the
# non-root user provided by the .NET runtime image.
RUN mkdir -p /app/work/data-protection-keys \
    && chown -R "$APP_UID:$APP_UID" /app

USER $APP_UID
ENTRYPOINT ["dotnet", "ContraApp.dll"]
