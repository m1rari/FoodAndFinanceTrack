# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY src/Domain/FinanceFoodTracker.Domain.csproj src/Domain/
COPY src/Application/FinanceFoodTracker.Application.csproj src/Application/
COPY src/Infrastructure/FinanceFoodTracker.Infrastructure.csproj src/Infrastructure/
COPY src/Api/FinanceFoodTracker.Api.csproj src/Api/
RUN dotnet restore src/Api/FinanceFoodTracker.Api.csproj

COPY . .
RUN dotnet publish src/Api/FinanceFoodTracker.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

RUN addgroup -S app && adduser -S app -G app \
    && mkdir -p /app/uploads \
    && chown -R app:app /app

USER app

ENTRYPOINT ["dotnet", "FinanceFoodTracker.Api.dll"]
