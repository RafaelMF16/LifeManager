# Build context: the repository root (docker build -f LifeManager.WebApi/Dockerfile .).

# Runtime image. It ships tzdata, which AppClock needs to resolve businessTimeZone (America/Sao_Paulo).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# Build stage. Every project file is copied before the restore, so the restore layer is only rebuilt when a .csproj changes.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["LifeManager.Domain/LifeManager.Domain.csproj", "LifeManager.Domain/"]
COPY ["LifeManager.Application/LifeManager.Application.csproj", "LifeManager.Application/"]
COPY ["LifeManager.Infrastructure/LifeManager.Infrastructure.csproj", "LifeManager.Infrastructure/"]
COPY ["LifeManager.WebApi/LifeManager.WebApi.csproj", "LifeManager.WebApi/"]
RUN dotnet restore "./LifeManager.WebApi/LifeManager.WebApi.csproj"
COPY . .
WORKDIR "/src/LifeManager.WebApi"
RUN dotnet build "./LifeManager.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./LifeManager.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# The API by default; the Cloud Run Job passes --run-jobs to run the background work once and exit.
ENTRYPOINT ["dotnet", "LifeManager.WebApi.dll"]
