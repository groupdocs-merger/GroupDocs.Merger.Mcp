using System.ComponentModel;
using System.Text;
using System.Text.Json;
using GroupDocs.Merger.Domain.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Merger.Mcp.Tools;

[McpServerToolType]
public static class GetDocumentInfoTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description(
        "Returns the file type, page count, size, and per-page dimensions of a document as JSON, without modifying the file. " +
        "Supports PDF, DOCX, XLSX, PPTX, and 30+ more document formats. " +
        "Call this tool whenever the user asks to inspect a document, check its page count, or get its details — " +
        "useful as a precondition check before Merge or Split (e.g. 'how many pages does this PDF have?'). " +
        "Do NOT pre-check whether the file exists — just pass the filename the user provided. " +
        "Returns a JSON object with fields `fileName`, `fileType`, `fileFormat`, `extension`, `pageCount`, `size`, and `pages` (array of `{ number, width, height, visible }`). " +
        "On failure, the response text starts with 'Document-info lookup failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> GetDocumentInfo(
        IFileResolver resolver,
        ILicenseManager licenseManager,
        FileInput file,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        try
        {
            // Build the engine straight from the resolved stream — no temp file.
            // GroupDocs.Merger.Merger accepts a Stream; spooling the content to
            // disk just to hand back a path would leak the OS file handle (Merger
            // releases input handles lazily, racing the cleanup delete on Windows).
            using var merger = password != null
                ? new GroupDocs.Merger.Merger(resolved.Stream, new LoadOptions(password))
                : new GroupDocs.Merger.Merger(resolved.Stream);

            var info = merger.GetDocumentInfo();

            if (info == null)
                return $"Could not retrieve document information for '{resolved.FileName}'.";

            var payload = new
            {
                fileName = resolved.FileName,
                fileType = info.Type?.ToString(),
                fileFormat = info.Type?.FileFormat,
                extension = info.Type?.Extension,
                pageCount = info.PageCount,
                size = info.Size,
                pages = info.Pages?
                    .Select(p => new { number = p.Number, width = p.Width, height = p.Height, visible = p.Visible })
                    .ToArray(),
            };

            // Raw JSON — never piped through OutputHelper.TruncateText (Pitfall #16).
            return JsonSerializer.Serialize(payload, JsonOptions);
        }
        catch (Exception ex)
        {
            // Surface the underlying engine exception instead of letting it bubble
            // to MCP's generic "An error occurred invoking 'get_document_info'."
            // wrapper. Pattern per Pitfall #18.
            return FormatException(ex, resolved.FileName);
        }
    }

    private static string FormatException(Exception ex, string fileName)
    {
        var sb = new StringBuilder();
        sb.Append($"Document-info lookup failed for '{fileName}': ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
