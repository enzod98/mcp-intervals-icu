FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY IntervalsMcp/IntervalsMcp.csproj IntervalsMcp/
RUN dotnet restore IntervalsMcp/IntervalsMcp.csproj

COPY IntervalsMcp/ IntervalsMcp/
RUN dotnet publish IntervalsMcp/IntervalsMcp.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Dokploy/Traefik hablan con el contenedor por red interna, así que Kestrel debe
# escuchar en 0.0.0.0 (no localhost) dentro del contenedor. El puerto lo elige Dokploy
# al mapear el servicio; 8080 es el default acá.
ENV MCP_TRANSPORT=http
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "IntervalsMcp.dll"]
