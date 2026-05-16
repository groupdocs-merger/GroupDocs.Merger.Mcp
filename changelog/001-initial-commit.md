---
id: 001
date: 2026-05-16
version: 26.5.0
type: feature
---

# Initial public release of GroupDocs.Merger MCP Server

## What changed
- NuGet package `GroupDocs.Merger.Mcp` published with `McpServer` package type.
- Three MCP tools exposed:
  - `Merge` — merge 2–4 documents into a single file and save the result to storage.
  - `Split` — extract specific pages (1-based, comma-separated) from a document, saving each extracted page as its own document.
  - `GetDocumentInfo` — return the file type, page count, size, and per-page dimensions of a document as JSON, without modifying it.
- Installable via `dnx GroupDocs.Merger.Mcp@26.5.0 --yes` (.NET 10 SDK required) or `dotnet tool install -g`.
- Docker image published to `ghcr.io/groupdocs-merger/merger-net-mcp` and `docker.io/groupdocs/merger-net-mcp`.
- Environment variables: `GROUPDOCS_MCP_STORAGE_PATH`, optional `GROUPDOCS_MCP_OUTPUT_PATH`, `GROUPDOCS_LICENSE_PATH`.
- Linux native graphics deps wired up: `SkiaSharp.NativeAssets.Linux.NoDependencies` (3.119.2) is referenced because `GroupDocs.Merger` 26.4.0 (via the `GroupDocs.Merger.Net100` runtime package) transitively requires SkiaSharp ≥ 3.119.2. `libgdiplus` + `libfontconfig1` are installed in the Docker image because Merger's image-format paths use `System.Drawing.Common` (transitively pulled at 6.0.0); the `System.Drawing.EnableUnixSupport` runtime flag is set in the csproj for the same reason. `ttf-mscorefonts-installer` is NOT installed — Merger does structural page operations, not text-glyph rendering (Pitfall #17 tier 1).

## Tool surface note
The framework subproject (`groupdocs-mcp-framework/src/GroupDocs.Merger.Mcp/`) ships only `Merge` and `Split`. `GetDocumentInfo` was added at clone time per the cross-product MCP standard (every GroupDocs MCP server exposes `GetDocumentInfo`) — modelled on the upstream `BasicUsage/GetDocumentInformation.cs` example.

## Pre-shipped pitfall remediations
- **Pitfall #18 (engine exceptions surface diagnostically)** — all three tools wrap their engine calls in `try/catch (Exception ex)` and return per-tool descriptive failure strings (`Merge failed for '<files>': <ExceptionType>: <message> | inner(0): ...`, etc.) instead of letting them bubble up to MCP's canned `"An error occurred invoking '<tool>'"` wrapper. The framework's `MergeTool` / `SplitTool` shipped with a `try/finally` (cleanup) but no `catch` — the catch was added at clone time.
- **Pitfall #16 (JSON tools return raw JSON)** — `GetDocumentInfoTool` returns `JsonSerializer.Serialize(...)` directly, never through `OutputHelper.TruncateText`.
- **License class** — the framework's `MergerLicenseManager.cs` uses `new GroupDocs.Merger.License().SetLicense(licensePath)` with no caveats; used verbatim (Metadata pattern).

## Why
Seventh product MCP server in the GroupDocs MCP framework family (after Metadata, Conversion, Comparison, Viewer, Watermark, Parser). Exposes GroupDocs.Merger for .NET as AI-callable tools for Claude, Cursor, VS Code / GitHub Copilot, and other MCP-compatible agents.

## Migration / impact
First release — no migration required.

## TODO before MCP registry publish
- [ ] Polish `[Description("...")]` strings on the 3 tools after first dogfooding with AI clients.
- [ ] Consider additional Merger tools (e.g. page rearrange, rotate, remove pages, extract page range) — deferred per the initial-clone scope.
