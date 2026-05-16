# AGENTS.md — Guide for AI coding agents

Brief orientation for AI coding agents (Claude Code, Copilot, Cursor, Aider, Amp, Codex) working in this repository.

## What this repo is

A standalone **MCP server** for [GroupDocs.Merger for .NET](https://products.groupdocs.com/merger) — exposes document merge / split / inspect operations as AI-callable tools via the Model Context Protocol.

Published to NuGet as `GroupDocs.Merger.Mcp` with the `McpServer` package type, and to `ghcr.io/groupdocs-merger/merger-net-mcp` + `docker.io/groupdocs/merger-net-mcp` as a container image.

## MCP tools exposed

| Tool | Description |
|---|---|
| `Merge` | Merges 2–4 documents into a single file. Returns a saved-path message + download URL. |
| `Split` | Extracts specific pages (1-based, comma-separated) from a document, saving each as its own file. |
| `GetDocumentInfo` | Returns file type, page count, size, and per-page dimensions as JSON (no modification). |

All tools accept `FileInput` (resolved via `IFileResolver`). `Split` and `GetDocumentInfo` accept an optional `password` for protected documents. All tools wrap engine-level exceptions in a descriptive error string (per-tool prefixes: `Merge failed for`, `Split failed for`, `Document-info lookup failed for`) instead of letting them bubble up to MCP's canned generic wrapper.

## Folder layout

```
src/                                           ← all projects + sln + Directory.Build.props
  GroupDocs.Merger.Mcp/
    Program.cs                                 ← host bootstrap + stdio transport
    MergerLicenseManager.cs                    ← applies GroupDocs.Total license
    Tools/
      MergeTool.cs                             ← [McpServerTool] — Merge
      SplitTool.cs                             ← [McpServerTool] — Split
      GetDocumentInfoTool.cs                   ← [McpServerTool] — GetDocumentInfo
    .mcp/
      server.json                              ← NuGet.org reads this to generate mcp.json snippet
    GroupDocs.Merger.Mcp.csproj                ← PackageType=McpServer + ToolCommandName
  GroupDocs.Merger.Mcp.Tests/
  GroupDocs.Merger.Mcp.sln
  Directory.Build.props
build/
  dependencies.props                           ← single source of truth for all versions
changelog/                                     ← one MD file per change (see changelog/README.md)
docker/
  Dockerfile                                   ← multi-stage, runtime on aspnet:10.0
  docker-compose.yml
.github/workflows/                             ← build_packages.yml, run_tests.yml, publish_prod.yml, publish_docker.yml
```

## Dependencies

- `GroupDocs.Mcp.Core` + `GroupDocs.Mcp.Local.Storage` — infrastructure NuGet packages
- `GroupDocs.Merger` — the actual merge/split engine (a meta-package → `GroupDocs.Merger.Net100` for net10.0)
- `ModelContextProtocol` — MCP SDK for .NET
- `Microsoft.Extensions.Hosting` — host builder for the stdio server
- `SkiaSharp.NativeAssets.Linux.NoDependencies` (3.119.2) — pinned because `GroupDocs.Merger.Net100` transitively requires it

## Commands you can run

```bash
# Restore + build
dotnet restore
dotnet build src/GroupDocs.Merger.Mcp.sln -c Release

# Run tests
dotnet test src/GroupDocs.Merger.Mcp.sln -c Release

# Run the server locally (stdio)
dotnet run --project src/GroupDocs.Merger.Mcp

# Local pack (writes to ./build_out) — validates server.json version matches dependencies.props
pwsh ./build.ps1

# Build + run the Docker image
docker build -f docker/Dockerfile -t merger-net-mcp:local .
docker run --rm -i -v $(pwd)/documents:/data merger-net-mcp:local
```

## Version scheme

CalVer `YY.M.N` (M not zero-padded). The version lives in **two** places that MUST stay in lockstep:
1. `build/dependencies.props` → `<GroupDocsMergerMcp>`
2. `src/GroupDocs.Merger.Mcp/.mcp/server.json` → both top-level `"version"` and `packages[0].version`

`build.ps1` enforces this at pack time (`Assert-ServerJsonVersionMatchesDependencies`) — if they drift, the build fails.

## House rules

1. **Tools must have rich `[Description("...")]` strings** — these are what AI agents read via the MCP protocol. Enumerate supported formats and describe the response format.
2. **Never add new env vars beyond** `GROUPDOCS_MCP_STORAGE_PATH`, `GROUPDOCS_MCP_OUTPUT_PATH`, `GROUPDOCS_LICENSE_PATH` without updating `server.json`, `docker-compose.yml`, and `README.md` together.
3. **JSON-emitting tools return raw JSON** — `GetDocumentInfoTool` returns `JsonSerializer.Serialize(...)` directly, never piped through `OutputHelper.TruncateText` (the truncation marker breaks `JsonDocument.Parse`).
4. **Engine calls live inside a `try/catch`** that returns a descriptive `<Operation> failed for '<file>': <ExceptionType>: <message>` string. Keep `resolver.ResolveAsync` OUTSIDE the catch so file-not-found errors propagate cleanly.
5. **Tests use xUnit + Moq** — mock `IFileResolver`, `IFileStorage`, `ILicenseManager`, `OutputHelper`.
6. **Changelog entries required** — any PR that changes behaviour adds `changelog/NNN-slug.md`.
7. **Target framework is `net10.0` only**.

## Release flow

See [RELEASE.md](RELEASE.md) for the per-release checklist.

## What NOT to change

- Do not hardcode the version in `.csproj` — it flows from `$(GroupDocsMergerMcp)` in `dependencies.props`.
- Do not remove the `<PackageType>McpServer</PackageType>` or `<ToolCommandName>groupdocs-merger-mcp</ToolCommandName>` from the csproj.
- Do not remove the `StripNativeRuntimePdbs` MSBuild target — defense against NuGet.org's 250 MB hard limit.
- Do not change the `.mcp/server.json` schema URL without cross-checking with the NuGet MCP docs.
