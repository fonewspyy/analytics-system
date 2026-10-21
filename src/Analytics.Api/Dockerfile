# Build context is the repository root:  docker build -f src/Analytics.Api/Dockerfile .

# ---- build ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

# Restore first, from project files only, so the layer is cached until a
# dependency changes rather than on every source edit.
COPY src/Analytics.Domain/Analytics.Domain.csproj                 src/Analytics.Domain/
COPY src/Analytics.Application/Analytics.Application.csproj       src/Analytics.Application/
COPY src/Analytics.Infrastructure/Analytics.Infrastructure.csproj src/Analytics.Infrastructure/
COPY src/Analytics.Api/Analytics.Api.csproj                       src/Analytics.Api/
RUN dotnet restore src/Analytics.Api/Analytics.Api.csproj

COPY src/ src/
RUN dotnet publish src/Analytics.Api/Analytics.Api.csproj \
        --configuration Release --no-restore --output /app

# ---- runtime --------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Built-in non-root user of the .NET images.
USER $APP_UID

# Alpine ships busybox wget, so no extra package is needed for the probe.
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "Analytics.Api.dll"]
