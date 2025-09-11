# Use the official .NET 9.0 runtime as the base image for running the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the .NET 9.0 SDK for building the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore dependencies
COPY ["Maliev.CurrencyService.Api/Maliev.CurrencyService.Api.csproj", "Maliev.CurrencyService.Api/"]
COPY ["Maliev.CurrencyService.Data/Maliev.CurrencyService.Data.csproj", "Maliev.CurrencyService.Data/"]
RUN dotnet restore "./Maliev.CurrencyService.Api/Maliev.CurrencyService.Api.csproj"

# Copy the rest of the application code
COPY . .

# Build the application
WORKDIR "/src/Maliev.CurrencyService.Api"
RUN dotnet build "./Maliev.CurrencyService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Maliev.CurrencyService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage: copy the published application to the runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create a non-root user for security
RUN addgroup --group dotnet --gid 2000 \
    && adduser --uid 1000 --gid 2000 --shell /bin/false --no-create-home --disabled-password dotnet

# Set ownership and switch to non-root user
RUN chown -R dotnet:dotnet /app
USER dotnet

# Set the entry point
ENTRYPOINT ["dotnet", "Maliev.CurrencyService.Api.dll"]