# Build de contexto na raiz do repositório (necessário para copiar a pasta channels/):
#   docker build -f src/ClaroFlowEngine.Api/Dockerfile -t claro-flow-engine-api .
# Em uso normal, isso é feito pelo docker-compose.full.yml (build.context: . / build.dockerfile: ...).

# ---- Estágio 1: build (SDK completo) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia só o .csproj primeiro para aproveitar cache de camada do Docker no restore.
COPY src/ClaroFlowEngine.Api/ClaroFlowEngine.Api.csproj src/ClaroFlowEngine.Api/
RUN dotnet restore src/ClaroFlowEngine.Api/ClaroFlowEngine.Api.csproj

COPY src/ClaroFlowEngine.Api/ src/ClaroFlowEngine.Api/
RUN dotnet publish src/ClaroFlowEngine.Api/ClaroFlowEngine.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- Estágio 2: runtime (só ASP.NET Core Runtime, imagem final bem menor que a do SDK) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Cache-buster: força rebuild da camada de canais quando o .dockerignore muda
ARG CACHE_BUST=1
# Canais simulados (HTML/CSS/JS) — copiados para dentro da imagem para que a API
# possa servi-los em /channels/* no modo "full" (ver Program.cs e docker-compose.full.yml).
COPY channels/ ./channels/

ENV ASPNETCORE_URLS=http://+:8080
# Environment padrão da imagem é Production (nunca aplica migration/seed automaticamente —
# ver padroes-e-boas-praticas.md §7 "Migrations sempre versionadas"). O docker-compose.full.yml
# sobrescreve para Staging, que é o gatilho já existente no Program.cs para migrate+seed automáticos.
ENV ASPNETCORE_ENVIRONMENT=Production
# Caminho relativo ao WORKDIR (/app) — casa com o COPY channels/ acima.
ENV StaticFiles__ChannelsPath=channels

EXPOSE 8080

ENTRYPOINT ["dotnet", "ClaroFlowEngine.Api.dll"]
