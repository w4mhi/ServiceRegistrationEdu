# Multi-stage Dockerfile for Service Registry
# Optimizes image size by separating build and runtime stages

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files for dependency restore
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["src/Omni.ServiceRegistry.Models/Omni.ServiceRegistry.Models.csproj", "src/Omni.ServiceRegistry.Models/"]
COPY ["src/Omni.ServiceRegistry.Interfaces/Omni.ServiceRegistry.Interfaces.csproj", "src/Omni.ServiceRegistry.Interfaces/"]
COPY ["src/Omni.ServiceRegistry.Services/Omni.ServiceRegistry.Services.csproj", "src/Omni.ServiceRegistry.Services/"]
COPY ["src/Omni.ServiceRegistry.Data/Omni.ServiceRegistry.Data.csproj", "src/Omni.ServiceRegistry.Data/"]
COPY ["src/Omni.ServiceRegistry.Api/Omni.ServiceRegistry.Api.csproj", "src/Omni.ServiceRegistry.Api/"]
COPY ["src/Omni.ServiceRegistry.Dashboard/Omni.ServiceRegistry.Dashboard.csproj", "src/Omni.ServiceRegistry.Dashboard/"]

# Restore dependencies
RUN dotnet restore "src/Omni.ServiceRegistry.Api/Omni.ServiceRegistry.Api.csproj"
RUN dotnet restore "src/Omni.ServiceRegistry.Dashboard/Omni.ServiceRegistry.Dashboard.csproj"

# Copy all source code
COPY . .

# Build API
WORKDIR "/src/src/Omni.ServiceRegistry.Api"
RUN dotnet build "Omni.ServiceRegistry.Api.csproj" -c Release -o /app/build/api

# Build Dashboard
WORKDIR "/src/src/Omni.ServiceRegistry.Dashboard"
RUN dotnet build "Omni.ServiceRegistry.Dashboard.csproj" -c Release -o /app/build/dashboard

# Publish API
WORKDIR "/src/src/Omni.ServiceRegistry.Api"
RUN dotnet publish "Omni.ServiceRegistry.Api.csproj" -c Release -o /app/publish/api /p:UseAppHost=false

# Publish Dashboard
WORKDIR "/src/src/Omni.ServiceRegistry.Dashboard"
RUN dotnet publish "Omni.ServiceRegistry.Dashboard.csproj" -c Release -o /app/publish/dashboard /p:UseAppHost=false

# Stage 2: Runtime for API
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api-runtime
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser && chown -R appuser:appuser /app
USER appuser

# Copy published API
COPY --from=build /app/publish/api .

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Omni.ServiceRegistry.Api.dll"]

# Stage 3: Runtime for Dashboard
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS dashboard-runtime
WORKDIR /app
EXPOSE 8082
EXPOSE 8083

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser && chown -R appuser:appuser /app
USER appuser

# Copy published Dashboard
COPY --from=build /app/publish/dashboard .

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8082/health || exit 1

ENTRYPOINT ["dotnet", "Omni.ServiceRegistry.Dashboard.dll"]

# Stage 4: Combined runtime (default)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser && chown -R appuser:appuser /app
USER appuser

# Copy both API and Dashboard
COPY --from=build /app/publish/api ./api
COPY --from=build /app/publish/dashboard ./dashboard

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Default to API (can be overridden in docker-compose or k8s)
ENTRYPOINT ["dotnet", "api/Omni.ServiceRegistry.Api.dll"]
