# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

# PTL.InternalWeb references the shared PTL.ApiClient library, so its
# .csproj must be copied here too - otherwise restore fails on the
# ProjectReference and the cache layer below is never invalidated when
# PTL.ApiClient changes.
COPY ["src/PTL.InternalWeb/PTL.InternalWeb.csproj", "src/PTL.InternalWeb/"]
COPY ["src/PTL.ApiClient/PTL.ApiClient.csproj", "src/PTL.ApiClient/"]
COPY ["src/PTL.Core/PTL.Core.csproj", "src/PTL.Core/"]
COPY ["src/PTL.Contracts/PTL.Contracts.csproj", "src/PTL.Contracts/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/PTL.InternalWeb/PTL.InternalWeb.csproj"

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "src/PTL.InternalWeb/PTL.InternalWeb.csproj" -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-windowsservercore-ltsc2022 AS final
WORKDIR /app
# Must match the ECS task definition's containerPort (and ALB target group
# port, if fronted by one) - coordinate any change here with infra.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PTL.InternalWeb.dll"]
