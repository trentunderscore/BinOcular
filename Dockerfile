# ==============================================================================
# Stage 1: Build the React 19 / Vite Frontend
# ==============================================================================
FROM node:22-alpine AS frontend-builder
WORKDIR /app/frontend

COPY frontend/binocular-ui/package*.json ./
RUN npm ci

COPY frontend/binocular-ui/ ./
RUN npm run build

# ==============================================================================
# Stage 2: Build and Publish the .NET 10 Web API
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-builder
WORKDIR /src

# Copy project file and restore dependencies
COPY backend/BinOcular.Api/BinOcular.Api.csproj backend/BinOcular.Api/
RUN dotnet restore backend/BinOcular.Api/BinOcular.Api.csproj

# Copy full backend source code
COPY backend/BinOcular.Api/ backend/BinOcular.Api/

# Embed compiled frontend into wwwroot for single-container hosting
COPY --from=frontend-builder /app/frontend/dist backend/BinOcular.Api/wwwroot

# Publish Release build
WORKDIR /src/backend/BinOcular.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ==============================================================================
# Stage 3: Minimal Production Runtime
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install security utilities and Detect It Easy (diec)
RUN apt-get update && apt-get install -y --no-install-recommends \
    file \
    binutils \
    wget \
    ca-certificates \
    && wget -q https://github.com/horsicq/DIE-engine/releases/download/3.21/die_3.21_Debian_12_amd64.deb -O /tmp/die.deb \
    && (apt-get install -y --no-install-recommends /tmp/die.deb || apt-get install -y -f) \
    && rm -f /tmp/die.deb \
    && apt-get purge -y --auto-remove wget \
    && rm -rf /var/lib/apt/lists/*

# Create persistent storage directories
RUN mkdir -p /app/data /app/App_Data/uploads /app/logs

COPY --from=backend-builder /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/binocular.db" \
    QT_QPA_PLATFORM=offscreen

EXPOSE 8080

VOLUME ["/app/data", "/app/App_Data/uploads"]

ENTRYPOINT ["dotnet", "BinOcular.Api.dll"]
