FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/GroupDocs.Merger.Mcp/GroupDocs.Merger.Mcp.csproj \
    -c Release -o /app --no-self-contained

FROM base AS final

# Native graphics deps GroupDocs.Merger's image-format and document-handling
# paths need on Linux. libgdiplus is the GDI+ implementation that
# System.Drawing.Common 6.0.0 P/Invokes; libfontconfig1 is its hidden font-
# discovery dependency. Pair this with <RuntimeHostConfigurationOption Include=
# "System.Drawing.EnableUnixSupport" Value="true" /> in the consuming csproj.
# Merger performs structural page operations (merge / split) — it does NOT
# rasterize new text glyphs, so ttf-mscorefonts-installer is not required
# (per Pitfall #17 tier 1).
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgdiplus libfontconfig1 \
 && rm -rf /var/lib/apt/lists/*

# OCI image labels. VERSION is supplied by the publish workflow via
# --build-arg VERSION=YY.MM.N. Default 'dev' makes local `docker build` runs
# self-identify instead of inheriting the base image's misleading version.
ARG VERSION=dev
LABEL org.opencontainers.image.title="GroupDocs.Merger.Mcp" \
      org.opencontainers.image.description="MCP server exposing GroupDocs.Merger for .NET as AI-callable tools (Merge, Split, GetDocumentInfo) for Claude, Cursor, GitHub Copilot, and other MCP agents." \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.source="https://github.com/groupdocs-merger/GroupDocs.Merger.Mcp" \
      org.opencontainers.image.url="https://www.nuget.org/packages/GroupDocs.Merger.Mcp" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.vendor="Aspose Pty Ltd"

COPY --from=build /app .

ENV GROUPDOCS_MCP_STORAGE_PATH=/data
VOLUME /data

USER $APP_UID

ENTRYPOINT ["dotnet", "GroupDocs.Merger.Mcp.dll"]
