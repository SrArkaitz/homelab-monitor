# --- Compilación -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/HomelabMonitor.Api/HomelabMonitor.Api.csproj src/HomelabMonitor.Api/
RUN dotnet restore src/HomelabMonitor.Api/HomelabMonitor.Api.csproj
COPY src/ src/
RUN dotnet publish src/HomelabMonitor.Api/HomelabMonitor.Api.csproj -c Release -o /app --no-restore

# --- Ejecución -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /data && chown app:app /data
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Monitor="Data Source=/data/monitor.db"
EXPOSE 8080
ENTRYPOINT ["dotnet", "HomelabMonitor.Api.dll"]
