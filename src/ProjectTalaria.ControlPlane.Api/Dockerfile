FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ProjectTalaria.ControlPlane.Api/ProjectTalaria.ControlPlane.Api.csproj", "ProjectTalaria.ControlPlane.Api/"]
COPY ["ProjectTalaria.Domain/ProjectTalaria.Domain.csproj", "ProjectTalaria.Domain/"]
COPY ["ProjectTalaria.Infrastructure/ProjectTalaria.Infrastructure.csproj", "ProjectTalaria.Infrastructure/"]
COPY ["ProjectTalaria.Application/ProjectTalaria.Application.csproj", "ProjectTalaria.Application/"]
COPY ["ProjectTalaria.Shared/ProjectTalaria.Shared.csproj", "ProjectTalaria.Shared/"]

RUN dotnet restore "ProjectTalaria.ControlPlane.Api/ProjectTalaria.ControlPlane.Api.csproj"

COPY . .
WORKDIR "/src/ProjectTalaria.ControlPlane.Api"
RUN dotnet build "ProjectTalaria.ControlPlane.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ProjectTalaria.ControlPlane.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health/live || exit 1

ENTRYPOINT ["dotnet", "ProjectTalaria.ControlPlane.Api.dll"]