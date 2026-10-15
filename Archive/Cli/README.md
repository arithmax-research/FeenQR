# Archived: FeenQR CLI (console agent)

**Archived:** 2026-10-15
**Status:** shelved — not built, not shipped, kept for reference.
**Why:** the product surface is the Blazor web app (`WebApp/Server`). Dropping the CLI
removes ~14,100 lines and 4 NuGet packages (incl. `Microsoft.ML.OnnxRuntime`, ~285 MB)
from every restore/build/publish.

## What is in here

| File | Original path | Purpose |
|---|---|---|
| `Program.cs` | `Program.cs` | CLI entry point + CLI-only dependency injection |
| `InteractiveCLI.cs` | `InteractiveCLI.cs` | 12,610-line Spectre.Console command loop |
| `ApiTester.cs` | `ApiTester.cs` | CLI-only news-API smoke-test harness |
| `UIHelper.cs` | `Core/UIHelper.cs` | Spectre.Console rendering helpers (CLI only) |

These were the only CLI-only sources: nothing else in the repo referenced `InteractiveCLI`,
`UIHelper` or `ApiTester`. `Services/**` (the shared service library, incl. `AchestService`)
and `WebApp/**` are untouched and still build.

## What changed in the build

`QuantResearchAgent.csproj`:

- `OutputType` is now `Library` — the project continues to be the shared service library
  that `WebApp/Server` references via `<ProjectReference Include="..\..\QuantResearchAgent.csproj" />`.
- added `<Compile Remove="Archive/**/*.cs" />` (+ matching `EmbeddedResource`/`None` removes),
  so nothing in this folder is compiled.

Removed package references (CLI-only or entirely unused):

| Package | Version | Why it could go |
|---|---|---|
| `Terminal.Gui` | 1.19.0 | zero references anywhere in compiled code |
| `Spectre.Console` | 0.49.1 | used only by `InteractiveCLI.cs` / `UIHelper.cs` |
| `NAudio` | 2.2.1 | zero references anywhere in compiled code |
| `Microsoft.ML.OnnxRuntime` | 1.20.1 | zero `OnnxRuntime`/`InferenceSession`/`.onnx` references in compiled code |

Measured effect: fresh `dotnet publish -c Release` of `WebApp/Server/Server.csproj` went
from **601 MB to 400 MB** (dead `runtimes/**/onnxruntime*` natives: 489 MB → gone).

## How to restore it

```bash
# 1. put the sources back
git mv Archive/Cli/Program.cs        Program.cs
git mv Archive/Cli/InteractiveCLI.cs InteractiveCLI.cs
git mv Archive/Cli/ApiTester.cs      ApiTester.cs
git mv Archive/Cli/UIHelper.cs       Core/UIHelper.cs

# 2. QuantResearchAgent.csproj: Library -> Exe,
#    and delete the three `<... Remove="Archive/**/*" />` lines

# 3. re-add the package references
dotnet add QuantResearchAgent.csproj package Terminal.Gui --version 1.19.0
dotnet add QuantResearchAgent.csproj package Spectre.Console --version 0.49.1
dotnet add QuantResearchAgent.csproj package NAudio --version 2.2.1
dotnet add QuantResearchAgent.csproj package Microsoft.ML.OnnxRuntime --version 1.20.1

# 4. build and run
dotnet run --project QuantResearchAgent.csproj
```

Everything above is also recoverable straight from git history (`git log --follow
Archive/Cli/InteractiveCLI.cs`) — the files were moved, not deleted.

## Known defect in the archived CLI (fix before relying on it)

The CLI does **not** start. It throws:

```
System.InvalidOperationException: This instance has already started one or more requests.
Properties can only be modified before sending the first request.
   at QuantResearchAgent.Services.IMFService..ctor  (Services/IMFService.cs:25)
```

Cause: `Program.cs` registers **one shared `HttpClient`** for ~20 services, while the
`IMFService` factory sets `HttpClient.BaseAddress` unconditionally. Qdrant requests fired
during startup run before `IMFService` is constructed, so the `BaseAddress` assignment
throws. Verified against a pristine `HEAD` worktree — pre-existing, unrelated to the
`AchestService` / sentiment work.

Two clean fixes:

- preferred: register per-service clients — `builder.Services.AddHttpClient<IMFService>()`;
- or stop mutating the shared client: have `IMFService` use absolute URIs or a private
  `HttpClient` (the pattern `AchestService` / `AchestClient` already uses).

## Work that lives only in this archive

The `social-sentiment` console command (in `InteractiveCLI.cs`, plus its registration and
`AchestService` constructor wiring in `Program.cs`) can only be exercised from here. Its
web equivalents are live and unaffected:

- `AchestService.GetSocialSentimentAsync(...)` / `GetSocialSentimentRawAsync(...)`
- `GET /api/sentiment/reddit-social/{symbol}` and `.../raw` (`SentimentController`)
- the sentiment panel in `WebApp/Client/Pages/Sentiment.razor`
