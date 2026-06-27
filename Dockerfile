FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/PicoERP.Domain/PicoERP.Domain.csproj", "PicoERP.Domain/"]
COPY ["src/PicoERP.Application/PicoERP.Application.csproj", "PicoERP.Application/"]
COPY ["src/PicoERP.Infrastructure/PicoERP.Infrastructure.csproj", "PicoERP.Infrastructure/"]
COPY ["src/PicoERP.Web/PicoERP.Web.csproj", "PicoERP.Web/"]

RUN dotnet restore "PicoERP.Web/PicoERP.Web.csproj"

COPY src/ .
WORKDIR "/src/PicoERP.Web"
RUN dotnet build "PicoERP.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PicoERP.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN mkdir -p /app/Data /app/Backups /app/logs
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "PicoERP.Web.dll"]
