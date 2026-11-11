# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

# PTL.ExternalWeb references PTL.ApiClient, which references PTL.Contracts. It also references
# PTL.Auth.Cidm directly (CIDM OpenID Connect authentication).
# All referenced .csproj files must be copied before restore so transitive
# ProjectReference restores can resolve correctly and use cache invalidation.
COPY ["src/PTL.ExternalWeb/PTL.ExternalWeb.csproj", "src/PTL.ExternalWeb/"]
COPY ["src/PTL.ApiClient/PTL.ApiClient.csproj", "src/PTL.ApiClient/"]
COPY ["src/PTL.Auth.Cidm/PTL.Auth.Cidm.csproj", "src/PTL.Auth.Cidm/"]
COPY ["src/PTL.Contracts/PTL.Contracts.csproj", "src/PTL.Contracts/"]
COPY ["src/PTL.Common/PTL.Common.csproj", "src/PTL.Common/"]
COPY ["src/PTL.SharedUI/PTL.SharedUI.csproj", "src/PTL.SharedUI/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/PTL.ExternalWeb/PTL.ExternalWeb.csproj"

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "src/PTL.ExternalWeb/PTL.ExternalWeb.csproj" -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

# ---- .NET runtime source ----
# Only its runtime files are used; the final stage has a different base image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-windowsservercore-ltsc2022 AS dotnet-runtime

# ---- Runtime stage ----
# Chromium (used for the PDF reports) cannot run in a Windows Server Core image - it exits as soon as it opens a page -
# so this stage uses the full Windows Server image and copies in the ASP.NET Core runtime, which that image does not include.
FROM mcr.microsoft.com/windows/server:ltsc2022 AS final
WORKDIR /app
# Must match the ECS task definition's containerPort (and ALB target group
# port, if fronted by one) - coordinate any change here with infra.
EXPOSE 8081
ENV ASPNETCORE_URLS=http://+:8081 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    PLAYWRIGHT_BROWSERS_PATH=C:\\ms-playwright

COPY --from=dotnet-runtime ["/Program Files/dotnet", "/Program Files/dotnet"]
RUN setx /M PATH "%PATH%;C:\Program Files\dotnet"

# FortiGate egress performs TLS inspection on outbound calls to CIDM/Azure AD B2C (b2clogin.com) -
# without trusting its inspection CA, the OIDC handler's metadata/JWKS fetch fails with PartialChain.
COPY ["build/certs/fortigate-root-ca.cer", "C:/certs/fortigate-root-ca.cer"]
COPY ["build/certs/fortigate-ca.cer", "C:/certs/fortigate-ca.cer"]
RUN certutil -addstore -f Root C:/certs/fortigate-root-ca.cer && certutil -addstore -f CA C:/certs/fortigate-ca.cer

COPY --from=build /app/publish .

# Playwright's headless Chromium generates external PDF reports. Install browser binaries at
# image build time so the app never attempts runtime downloads. The same instruction then fails the build if
# the browser cannot print a page; DEBUG logs the browser's exit code and errors to the build output.
RUN powershell -NoProfile -ExecutionPolicy Bypass -File .\playwright.ps1 install chromium && \
    set DEBUG=pw:browser&& \
    powershell -NoProfile -ExecutionPolicy Bypass -File .\playwright.ps1 pdf about:blank smoke.pdf && \
    del smoke.pdf

USER $APP_UID
ENTRYPOINT ["dotnet", "PTL.ExternalWeb.dll"]
