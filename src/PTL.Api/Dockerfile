# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

COPY ["src/PTL.Api/PTL.Api.csproj", "src/PTL.Api/"]
COPY ["src/PTL.Core/PTL.Core.csproj", "src/PTL.Core/"]
COPY ["src/PTL.Data/PTL.Data.csproj", "src/PTL.Data/"]
COPY ["src/PTL.Contracts/PTL.Contracts.csproj", "src/PTL.Contracts/"]
COPY ["src/PTL.Common/PTL.Common.csproj", "src/PTL.Common/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/PTL.Api/PTL.Api.csproj"

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "src/PTL.Api/PTL.Api.csproj" -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-windowsservercore-ltsc2022 AS final
WORKDIR /app
# Must match the ECS task definition's containerPort (and ALB target group
# port, if fronted by one) - coordinate any change here with infra.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

# FortiGate egress performs TLS inspection on outbound calls to CIDM/Entra ID (bearer token
# validation) - without trusting its inspection CA, metadata/JWKS fetches fail with PartialChain.
COPY ["build/certs/fortigate-root-ca.cer", "C:/certs/fortigate-root-ca.cer"]
COPY ["build/certs/fortigate-ca.cer", "C:/certs/fortigate-ca.cer"]
RUN certutil -addstore -f Root C:/certs/fortigate-root-ca.cer && certutil -addstore -f CA C:/certs/fortigate-ca.cer

USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PTL.Api.dll"]
