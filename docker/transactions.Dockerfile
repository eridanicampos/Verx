# syntax=docker/dockerfile:1.7
# Build multi-stage: SDK só no estágio de build; imagem final apenas com o runtime ASP.NET,
# executando como usuário não-root. Contexto de build = raiz do repositório.
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Domain/CashFlow.Transactions.Domain/CashFlow.Transactions.Domain.csproj src/Domain/CashFlow.Transactions.Domain/packages.lock.json src/Domain/CashFlow.Transactions.Domain/
COPY src/Application/CashFlow.Transactions.Application/CashFlow.Transactions.Application.csproj src/Application/CashFlow.Transactions.Application/packages.lock.json src/Application/CashFlow.Transactions.Application/
COPY src/Infrastructure/CashFlow.Transactions.Infrastructure/CashFlow.Transactions.Infrastructure.csproj src/Infrastructure/CashFlow.Transactions.Infrastructure/packages.lock.json src/Infrastructure/CashFlow.Transactions.Infrastructure/
COPY src/API/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj src/API/CashFlow.Transactions.Api/packages.lock.json src/API/CashFlow.Transactions.Api/
# Restore em camada própria (cache) e em modo travado: falha se o lock divergir.
RUN dotnet restore src/API/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj --locked-mode
COPY src/Domain/CashFlow.Transactions.Domain/ src/Domain/CashFlow.Transactions.Domain/
COPY src/Application/CashFlow.Transactions.Application/ src/Application/CashFlow.Transactions.Application/
COPY src/Infrastructure/CashFlow.Transactions.Infrastructure/ src/Infrastructure/CashFlow.Transactions.Infrastructure/
COPY src/API/CashFlow.Transactions.Api/ src/API/CashFlow.Transactions.Api/
RUN dotnet publish src/API/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj -c Release -o /app --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1 \
    SecretsPath=/run/secrets
COPY --from=build --chown=root:root --chmod=0555 /app ./
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
  CMD wget -qO- http://127.0.0.1:8080/health/live >/dev/null || exit 1
ENTRYPOINT ["dotnet", "CashFlow.Transactions.Api.dll"]
