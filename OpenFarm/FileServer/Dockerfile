FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["FileServer/FileProcessor/FileProcessor.csproj", "FileServer/FileProcessor/"]
COPY ["RabbitMQHelper/RabbitMQHelper.csproj", "RabbitMQHelper/"]

# Restore packages
RUN dotnet restore "FileServer/FileProcessor/FileProcessor.csproj"

# Copy source code
COPY ["FileServer/FileProcessor/", "FileServer/FileProcessor/"]
COPY ["RabbitMQHelper/", "RabbitMQHelper/"]

COPY /FileServer/FileProcessor/${GOOGLE_SERVICE_ACCOUNT_CRED_FILE} .

# Build
WORKDIR /src/FileServer/FileProcessor
RUN dotnet build "FileProcessor.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FileProcessor.csproj" -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

WORKDIR /app
EXPOSE 80

# Install curl for health checks (required for docker-compose healthcheck)
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy application files
COPY --from=publish /app/publish .

# Change ownership of app directory
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

ENTRYPOINT ["dotnet", "FileProcessor.dll"]
