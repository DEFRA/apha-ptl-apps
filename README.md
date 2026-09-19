# PTL Apps

ASP.NET Core solution for the PTL (Proficiency Testing) platform.

## Projects

| Project | Description | Default URL |
|---|---|---|
| `src/PTL.Api` | Backend API | http://localhost:5252 |
| `src/PTL.InternalWeb` | Internal-facing web portal (MVC + Razor) | http://localhost:5214 |
| `src/PTL.ExternalWeb` | External-facing web portal (MVC + Razor) | http://localhost:5215 |
| `tests/PTL.Api.Tests` | Unit/integration tests for `PTL.Api` | - |
| `tests/PTL.InternalWeb.Tests` | Unit/integration tests for `PTL.InternalWeb` | - |
| `tests/PTL.ExternalWeb.Tests` | Unit/integration tests for `PTL.ExternalWeb` | - |

`PTL.Api` uses a feature-folder convention: feature-specific code lives under `Features/<Name>/` (its own
Minimal API endpoint-mapping class per feature), instead of a flat `Endpoints/` folder. Cross-cutting
concerns that aren't tied to one feature (the DB connection factory, startup checks) live under
`Infrastructure/` instead:

```
src/PTL.Api/
  Features/
    Health/              # HealthEndpoints, DatabaseHealthCheck, ReadinessKeyFilter
  Infrastructure/         # StartupChecks, IDbConnectionFactory, SqlConnectionFactory
  Program.cs
```

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download)
- Git

## Getting Started

1. Clone the repository and open [`PTL.slnx`](PTL.slnx) in Visual Studio, VS Code, or Rider.
2. Restore local tooling (required once per clone, powers the pre-commit hook):
   ```powershell
   dotnet tool restore
   dotnet husky install
   ```
3. Build the solution:
   ```powershell
   dotnet build PTL.slnx
   ```
4. Run a project (from its folder or with `--project`):
   ```powershell
   dotnet run --project src/PTL.Api/PTL.Api.csproj
   dotnet run --project src/PTL.InternalWeb/PTL.InternalWeb.csproj
   dotnet run --project src/PTL.ExternalWeb/PTL.ExternalWeb.csproj
   ```

## Database connection (PTL.Api)

`PTL.Api` connects to SQL Server (RDS for SQL Server) via **Dapper** (on top of `Microsoft.Data.SqlClient`) -
`Infrastructure/IDbConnectionFactory` is the one place that opens a connection; feature code should take a
dependency on it (not `SqlConnection` directly) and use Dapper's extension methods for queries. The app never
contains a hardcoded connection string or reads AWS directly - the connection details resolve differently per
environment:

- **Locally**: from `src/PTL.Api/appsettings.Development.json` (already checked in, using a placeholder
  local-only password). Point it at a local SQL Server instance - e.g. a Docker container:
  ```powershell
  docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Local_only_dev_pw1" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
  ```
- **In every deployed environment**: Parameter Store holds `Host`/`Name`/`User`/`Password` as four separate
  values (no pre-built connection string), so the app reads them as four separate config keys and composes
  the connection string itself in `SqlConnectionFactory` via `SqlConnectionStringBuilder`:

  | Config key | Env var (task definition `name`) |
  |---|---|
  | `Database:Host` | `Database__Host` |
  | `Database:Name` | `Database__Name` |
  | `Database:User` | `Database__User` |
  | `Database:Password` | `Database__Password` |

  These four env var names are **this app's contract** - hand them to the platform team exactly as listed;
  whatever they call the underlying Parameter Store entries (`db_host`, `db_name`, etc.) is irrelevant, since
  only the task definition's `secrets` block needs to know both names (`valueFrom` = the parameter's ARN,
  `name` = the env var name above). The app code never knows or needs to know the SSM parameter names.
  Nothing DB-related is ever baked into the Docker image.

`Program.cs` fails fast at startup if any of the four values is missing (a clear, immediate error is better
than an app that starts and only fails later on first DB use) - see `StartupChecks.RequireDatabaseOptions`.
If a config key is misspelled or missing from the task definition, the container won't start at all: it
throws `InvalidOperationException` immediately, which shows up in CloudWatch Logs for that task and (with the
ECS deployment circuit breaker enabled) triggers a rollback to the last healthy revision - not a silent
misconfiguration that only surfaces on first DB use.

`Database:TrustServerCertificate` defaults to `false` (secure - RDS presents a valid CA-signed certificate)
and is only set `true` in `appsettings.Development.json`, for the self-signed certificate a local Docker SQL
Server container presents.

`PTL.Api` is not internet-facing - it's only ever called by `PTL.InternalWeb`/`PTL.ExternalWeb` - so there
are two separate health endpoints with different exposure in mind:

- `GET /health` - liveness only (process is responsive), no DB dependency, cheap. This is what an ALB
  target-group or ECS container health check should point at; it's safe to leave reachable since it reveals
  nothing and can't be abused to generate load.
- `GET /health/ready` - actually opens a DB connection to confirm readiness. **Gated behind a required
  `X-Readiness-Key` request header**, checked in `ReadinessKeyFilter` before the DB is ever touched. A
  missing/wrong key returns a plain `404` (not `401`/`403`) so a scanner can't even tell the route exists,
  and the mismatch is rejected before any database call is made - so it can never be used to exhaust DB
  connections, even under a flood of guesses. The expected key comes from config key
  `HealthCheck:ReadinessKey` (→ env var `HealthCheck__ReadinessKey` in deployed environments, injected the
  same way as the connection string - though since this key only deters bot noise rather than gating real
  access, it doesn't need Secrets Manager/SecureString; a plain SSM String parameter is enough). The
  Parameter Store entry's own *name* is irrelevant (it can be `health_readiness_key` or anything else) -
  only the task definition's `secrets[].name` needs to be the exact literal `HealthCheck__ReadinessKey`.
  Unlike the DB config, `ReadinessKeyFilter` can't distinguish "not configured" from "wrong key" at request
  time (both must return an identical `404`, so a scanner can't tell them apart) - so `Program.cs` separately
  fails fast at startup via `StartupChecks.RequireReadinessKey` if the key is missing entirely, turning a
  broken secret wiring into an obvious boot-time crash instead of a silently-always-404 endpoint.
  **Do not wire this into an ALB/ECS health check** - besides the DB-dependency concern above, an AWS ALB
  target-group health check cannot send custom headers at all, so it could never pass this gate. This
  endpoint is for on-demand manual diagnostics or an internal monitoring tool that can set the header.

RDS for SQL Server doesn't support IAM database authentication (unlike Aurora MySQL/PostgreSQL), so the
stored connection string always contains a real username/password.

## Web front-ends' awareness of Api health (PTL.InternalWeb / PTL.ExternalWeb)

Neither web front-end chains its own liveness/readiness into `PTL.Api`'s DB check - that would turn a
transient RDS blip into a full web-app outage, since ECS/ALB would pull the web app out of rotation over a
dependency issue even though it can still serve everything that doesn't need Api. Instead:

- Every real call to `PTL.Api` already goes through `.AddStandardResilienceHandler()` (retries, circuit
  breaker, timeouts) via `AddPtlApiClient`, so a failing Api surfaces immediately at the point of actual use
  - the most accurate, real-time signal available, better than any periodic synthetic check.
- Both web apps get `/health` (pure liveness, no dependency calls - safe for ALB/ECS) and `/health/ready`
  (checks Api connectivity, gated behind `X-Readiness-Key` same as `PTL.Api`'s - **more** important to gate
  here since the web front-ends, unlike `PTL.Api`, are internet-facing) for free from `PTL.ApiClient`'s
  `MapHealthEndpoints()` - this plumbing is shared (`ApiConnectivityHealthCheck`, `ReadinessKeyFilter`,
  `HealthEndpoints` all live in `PTL.ApiClient`) rather than duplicated per web app, matching how the typed
  client itself is shared. `ApiConnectivityHealthCheck` reports `Degraded`, never `Unhealthy`, when Api is
  unreachable - Degraded still returns HTTP `200` from the health check middleware by default, so it's
  visible in the JSON body to a monitoring tool without ever affecting ALB/ECS routing decisions.

## Code Quality

- **`.editorconfig`** defines formatting and style conventions.
- **`Directory.Build.props`** enables Roslyn analyzers (`EnableNETAnalyzers`, `AnalysisLevel=latest`) for every project.
- **Husky.Net** runs `dotnet format --verify-no-changes` on staged `.cs`/`.cshtml` files before each commit (see [`.husky/task-runner.json`](.husky/task-runner.json)). Run `dotnet format` locally to fix violations before committing.

## Testing

Each app under `src/` has a matching test project under `tests/` (xUnit + `Microsoft.AspNetCore.Mvc.Testing`). Coverage is
collected with coverlet and enforced at a 90% line-coverage threshold per project via `tests/Directory.Build.props`; the
pre-commit hook also runs the full suite via `dotnet test PTL.slnx`.

```powershell
dotnet test PTL.slnx
```

## Docker images & deployment

`.github/workflows/build-test-publish-images.yml` builds and, on push to `main`, publishes images for
whichever of `PTL.Api` / `PTL.InternalWeb` / `PTL.ExternalWeb` changed, each to its own ECR repository
(configured via the `ECR_API_REPOSITORY` / `ECR_INTERNAL_REPOSITORY` / `ECR_EXTERNAL_REPOSITORY` variables
on the `ecr-production` environment). `PTL.InternalWeb` and `PTL.ExternalWeb` both depend on the shared
`PTL.ApiClient` library, so a change to `PTL.ApiClient` triggers a rebuild/publish of both consumers even if
their own project folder didn't change.

### Image tags

Every image is tagged `sha-<12-char-commit-sha>-<run-id>-<run-attempt>`, generated automatically by the
workflow - there is no hand-maintained version file. This is deliberate:

- It's always unique per push (or re-run), so ECR tag immutability never blocks a publish.
- It's directly traceable back to the exact commit and Actions run that produced it, with no separate
  version-bump step to keep in sync.
- There is no `latest` tag - the ECR repositories have tag immutability enabled, which would make a mutable
  `latest` tag impossible to update anyway.

**Finding the latest image for a component**: since the tag itself carries no ordering information, use
ECR's push timestamp instead of a tag convention:

```bash
aws ecr describe-images \
  --repository-name <repository> \
  --query 'sort_by(imageDetails,& imagePushedAt)[-1].imageTags[0]' \
  --output text
```

The `publish` job's step summary also records the exact image URI and digest for every run, so the Actions
run history for `build-test-publish-images.yml` on `main` is a complete, ordered audit trail of what was
published.

An earlier revision of this pipeline used a hand-bumped `VERSION` file per project with semver tags and a
required "was VERSION bumped?" PR check. That has been retired in favour of the fully-automatic scheme
above - there is no version file to remember to bump.

GitHub Actions builds and pushes images; it does not deploy. Jenkins (or other deployment tooling) picks a
specific `sha-*` tag from ECR and deploys it to ECS.

A separate, unrelated git tag (`ptl-pr-<pr-number>-<short-sha>`, from
[`.github/workflows/create-tag.yml`](.github/workflows/create-tag.yml)) is created on every merged PR as a
whole-repo traceability marker back to the PR and exact commit - it has no relationship to the image tags
above.

The only required PR status check is `gate`, which aggregates `changes` (path detection), `quality`
(formatting + analyzer-enforced build/test), `sonarcloud` (disabled - see below), `container-validation`
(container build for each changed component) and, on push to `main`, `publish`.

**SonarCloud** is wired up but only runs when the `SONAR_ENABLED` repository variable is `'true'`. To enable
it: create the project, configure its access token and project identifiers as repo secrets/variables (see
the `sonarcloud` job in `build-test-publish-images.yml` for what it expects), then set `SONAR_ENABLED` to
`'true'`.
