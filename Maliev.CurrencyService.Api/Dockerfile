# syntax=docker/dockerfile:1.4
# Multi-stage Docker build for Maliev.CurrencyService.Api
# Based on MALIEV Co. Ltd. standard .NET 9 microservice template

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and nuget.config (excluding tests)
COPY nuget.config ./
COPY Maliev.CurrencyService.Api/Maliev.CurrencyService.Api.csproj Maliev.CurrencyService.Api/
COPY Maliev.CurrencyService.Data/Maliev.CurrencyService.Data.csproj Maliev.CurrencyService.Data/

# Restore dependencies
RUN --mount=type=secret,id=nuget_username \
  --mount=type=secret,id=nuget_password \
  NUGET_USERNAME=$(cat /run/secrets/nuget_username) \
  NUGET_PASSWORD=$(cat /run/secrets/nuget_password) \
  dotnet restore Maliev.CurrencyService.Api/Maliev.CurrencyService.Api.csproj

# Copy all source code
COPY . .

# Build and publish
WORKDIR /src/Maliev.CurrencyService.Api
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create directory for Google Secret Manager secrets and ensure 'app' owns the workdir (app user already exists in ASP.NET runtime image)
RUN mkdir -p /mnt/secrets && chown -R app:app /app /mnt/secrets

# Switch to non-root user
USER app

# Copy published application (now owned by app)
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set ASP.NET Core environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/currencies/liveness || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Maliev.CurrencyService.Api.dll"]
