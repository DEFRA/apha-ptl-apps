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

## Code Quality

- **`.editorconfig`** defines formatting and style conventions.
- **`Directory.Build.props`** enables Roslyn analyzers (`EnableNETAnalyzers`, `AnalysisLevel=latest`) for every project.
- **Husky.Net** runs `dotnet format --verify-no-changes` on staged `.cs`/`.cshtml` files before each commit (see [`.husky/task-runner.json`](.husky/task-runner.json)). Run `dotnet format` locally to fix violations before committing.

## Testing

Each app under `src/` has a matching test project under `tests/` (xUnit + `Microsoft.AspNetCore.Mvc.Testing`). Coverage is
collected with coverlet and enforced at an 80% line-coverage threshold per project via `tests/Directory.Build.props`; the
pre-commit hook also runs the full suite via `dotnet test PTL.slnx`.

```powershell
dotnet test PTL.slnx
```
