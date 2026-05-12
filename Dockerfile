FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ProjectTalaria.slnx .
COPY src/ ./src/
COPY scripts/ ./scripts/
RUN dotnet restore

RUN dotnet publish src/ProjectTalaria.ControlPlane.Api/ProjectTalaria.ControlPlane.Api.csproj -c Release -o /app/controlplane --no-restore
RUN dotnet publish src/ProjectTalaria.DataPlane.Streamer/ProjectTalaria.DataPlane.Streamer.csproj -c Release -o /app/streamer --no-restore

# Copy scripts to app folder for runtime stages
COPY scripts/ /app/scripts/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS controlplane
WORKDIR /app
COPY --from=build /app/controlplane .
COPY --from=build /app/scripts ./scripts
EXPOSE 5000
ENTRYPOINT ["dotnet", "ProjectTalaria.ControlPlane.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS streamer
WORKDIR /app
COPY --from=build /app/streamer .
EXPOSE 5001
ENTRYPOINT ["dotnet", "ProjectTalaria.DataPlane.Streamer.dll"]