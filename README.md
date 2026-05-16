# GroupDocs.Merger MCP Server

MCP server that exposes [GroupDocs.Merger](https://products.groupdocs.com/merger) as AI-callable tools
for Claude, Cursor, GitHub Copilot, and other MCP agents.

## Installation

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

**Run directly with `dnx` (recommended — no install step):**

```bash
dnx GroupDocs.Merger.Mcp --yes
```

Pulls the latest stable release on every invocation. To pin to a specific
version (recommended for shared configs and CI), append `@<version>`:

```bash
dnx GroupDocs.Merger.Mcp@26.5.0 --yes
```

**Or install as a global dotnet tool:**

```bash
dotnet tool install -g GroupDocs.Merger.Mcp
groupdocs-merger-mcp
```

**Or run via Docker:**

```bash
docker run --rm -i \
  -v $(pwd)/documents:/data \
  ghcr.io/groupdocs-merger/merger-net-mcp:latest
```

## Available MCP Tools

| Tool | Description |
|---|---|
| `Merge` | Merges 2–4 documents into a single file and saves the result to storage |
| `Split` | Splits a document by extracting specific pages, saving each as an individual file |
| `GetDocumentInfo` | Returns the file type, page count, size, and per-page dimensions of a document as JSON (without modifying it) |

All tools support PDF, DOCX, XLSX, PPTX, and 30+ more document, image, and archive formats.

## Example prompts for AI agents

Copy any of these into Claude Desktop, Cursor, or GitHub Copilot Chat after the
server is connected.

1. **Merge documents**: *"Merge intro.docx and appendix.docx into one file."*
2. **Combine PDFs**: *"Combine report-q1.pdf, report-q2.pdf, and report-q3.pdf into a single PDF."*
3. **Split out pages**: *"Split pages 3, 7, and 9 out of contract.pdf into separate files."*
4. **Inspect before merging**: *"How many pages does presentation.pptx have?"*
5. **Extract a page range**: *"Pull pages 1 through 5 of manual.docx into their own documents."*

## Configuration

| Variable | Description | Default |
|---|---|---|
| `GROUPDOCS_MCP_STORAGE_PATH` | Base folder for input and output files | current directory |
| `GROUPDOCS_MCP_OUTPUT_PATH` | *(Optional)* separate folder for output files | `GROUPDOCS_MCP_STORAGE_PATH` |
| `GROUPDOCS_LICENSE_PATH` | Path to GroupDocs license file. In evaluation mode, merged / split output may include watermarks | (evaluation mode) |

## Usage with Claude Desktop

```json
{
  "mcpServers": {
    "groupdocs-merger": {
      "type": "stdio",
      "command": "dnx",
      "args": ["GroupDocs.Merger.Mcp", "--yes"],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "/path/to/documents"
      }
    }
  }
}
```

> To pin: replace `"GroupDocs.Merger.Mcp"` with `"GroupDocs.Merger.Mcp@26.5.0"` in `args`.

## Usage with VS Code / GitHub Copilot

NuGet.org generates a ready-to-use `mcp.json` snippet on the [package page](https://www.nuget.org/packages/GroupDocs.Merger.Mcp).
Copy it directly into your `.vscode/mcp.json`.

Alternatively, add manually to `.vscode/mcp.json`:

```json
{
  "inputs": [
    {
      "type": "promptString",
      "id": "storage_path",
      "description": "Base folder for input and output files.",
      "password": false
    }
  ],
  "servers": {
    "groupdocs-merger": {
      "type": "stdio",
      "command": "dnx",
      "args": ["GroupDocs.Merger.Mcp", "--yes"],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "${input:storage_path}"
      }
    }
  }
}
```

## Usage with Docker Compose

```bash
cd docker
docker compose up
```

Edit `docker/docker-compose.yml` to point volumes at your local documents folder.

## Documentation & guides

Step-by-step deployment guides and a published-package integration test suite
live in the companion repo
[**GroupDocs.Merger.Mcp.Tests**](https://github.com/groupdocs-merger/GroupDocs.Merger.Mcp.Tests):

- [Install from NuGet](https://github.com/groupdocs-merger/GroupDocs.Merger.Mcp.Tests/blob/master/how-to/01-install-from-nuget.md)
- [Run via Docker](https://github.com/groupdocs-merger/GroupDocs.Merger.Mcp.Tests/blob/master/how-to/02-run-via-docker.md)
- [Verify on the MCP registry](https://github.com/groupdocs-merger/GroupDocs.Merger.Mcp.Tests/blob/master/how-to/03-verify-mcp-registry.md)
- [Use with Claude Desktop](https://github.com/groupdocs-merger/GroupDocs.Merger.Mcp.Tests/blob/master/how-to/04-use-with-claude-desktop.md)
- [Use with VS Code / GitHub Copilot](https://github.com/groupdocs-merger/GroupDocs.Merger.Mcp.Tests/blob/master/how-to/05-use-with-vscode-copilot.md)
- [Run the integration tests](https://github.com/groupdocs-merger/GroupDocs.Merger.Mcp.Tests/blob/master/how-to/06-run-integration-tests.md)

## License

MIT — see [LICENSE](LICENSE)

<!-- mcp-name: io.github.groupdocs-merger/groupdocs-merger-mcp -->
