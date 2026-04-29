# ---- Stage 1: Build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS builder
WORKDIR /src

# Restore dependencies first (layer-cache friendly)
COPY OcrApi.csproj ./
RUN dotnet restore OcrApi.csproj

# Copy remaining source and publish
COPY . .
RUN dotnet publish OcrApi.csproj \
    --configuration Release \
    --runtime linux-musl-x64 \
    --self-contained false \
    --output /app/publish

# ---- Stage 2: Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup --system --gid 1001 appgroup \
 && adduser  --system --uid 1001 --ingroup appgroup appuser

COPY --from=builder --chown=appuser:appgroup /app/publish ./

USER appuser

EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "OcrApi.dll"]
