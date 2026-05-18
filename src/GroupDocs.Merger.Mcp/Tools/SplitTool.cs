using System.ComponentModel;
using System.Text;
using GroupDocs.Merger.Domain.Common;
using GroupDocs.Merger.Domain.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Merger.Mcp.Tools;

[McpServerToolType]
public static class SplitTool
{
    [McpServerTool, Description(
        "Splits a document by extracting specific pages, saving each extracted page as an individual document to storage. " +
        "Supports PDF, DOCX, XLSX, PPTX, and 30+ more multi-page document formats. " +
        "Call this tool immediately whenever the user asks to split, extract pages, or separate a document into parts. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "Pass page numbers as a comma-separated 1-based list, e.g. pages='3,6,8'. " +
        "Returns a message ('Split \"<file>\" into N file(s):') followed by the saved path of each extracted document. " +
        "On failure, the response text starts with 'Split failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> Split(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Page numbers to extract as separate documents (1-based), e.g. '3,6,8'")] string pages,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        int[] pageNumbers;
        try
        {
            pageNumbers = pages
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(int.Parse)
                .ToArray();
        }
        catch (FormatException)
        {
            return $"Could not parse page numbers from '{pages}'. Provide a comma-separated 1-based list, e.g. pages='3,6,8'.";
        }

        if (pageNumbers.Length == 0)
            return "Provide at least one page number, e.g. pages='3,6,8'.";

        var ext = Path.GetExtension(resolved.FileName);
        var baseName = Path.GetFileNameWithoutExtension(resolved.FileName);

        try
        {
            // Each extracted page is written to an in-memory stream supplied via
            // the CreateSplitStream callback — no temp files / temp directory.
            // `number` is the 1-based page number being written; ReleaseSplitStream
            // fires once that page is fully written, while still inside Split().
            var open = new Dictionary<int, MemoryStream>();
            var parts = new SortedDictionary<int, byte[]>();

            var createStream = new CreateSplitStream(number =>
            {
                var ms = new MemoryStream();
                open[number] = ms;
                return ms;
            });
            var releaseStream = new ReleaseSplitStream((number, _) =>
            {
                parts[number] = open[number].ToArray();
                open[number].Dispose();
            });

            using (var merger = password != null
                ? new GroupDocs.Merger.Merger(resolved.Stream, new LoadOptions(password))
                : new GroupDocs.Merger.Merger(resolved.Stream))
            {
                merger.Split(new SplitOptions(createStream, releaseStream, pageNumbers));
            }

            var savedPaths = new List<string>();
            foreach (var part in parts)
            {
                var outputName = $"{baseName}_{part.Key}{ext}";
                var savedPath = await storage.WriteFileAsync(outputName, part.Value, rewrite: false);
                savedPaths.Add(savedPath);
            }

            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            var fileList = string.Join("\n", savedPaths);
            return $"{prefix}Split '{resolved.FileName}' into {savedPaths.Count} file(s):\n{fileList}";
        }
        catch (Exception ex)
        {
            // Surface the underlying engine exception instead of letting it bubble
            // to MCP's generic "An error occurred invoking 'split'." wrapper.
            // Pattern per Pitfall #18.
            return FormatException(ex, resolved.FileName, pages);
        }
    }

    private static string FormatException(Exception ex, string fileName, string pages)
    {
        var sb = new StringBuilder();
        sb.Append($"Split failed for '{fileName}' (pages='{pages}'): ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
