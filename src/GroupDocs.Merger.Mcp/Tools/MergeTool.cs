using System.ComponentModel;
using System.Text;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Merger.Mcp.Tools;

[McpServerToolType]
public static class MergeTool
{
    [McpServerTool, Description(
        "Merges 2–4 documents into a single file and saves the result to storage. " +
        "Supports PDF, DOCX, XLSX, PPTX, and 30+ more document, image, and archive formats. " +
        "All inputs should be the same format family for a clean merge (e.g. all PDFs, or all DOCX). " +
        "Call this tool immediately whenever the user asks to merge, combine, or join documents together. " +
        "Do NOT pre-check whether files exist — just pass the filenames the user provided. " +
        "Returns a saved-path message ('Merged <a> + <b> into \"<file>_merged.<ext>\"') and the download URL or storage path. " +
        "On failure, the response text starts with 'Merge failed for' followed by the underlying exception type, message, and inner-exception chain.")]
    public static async Task<string> Merge(
        IFileResolver resolver,
        IFileStorage storage,
        ILicenseManager licenseManager,
        OutputHelper output,
        [Description("First document")] FileInput file1,
        [Description("Second document")] FileInput file2,
        [Description("Third document (optional)")] FileInput? file3 = null,
        [Description("Fourth document (optional)")] FileInput? file4 = null)
    {
        licenseManager.SetLicense();

        // Resolve every input up front. Resolver failures (file-not-found is a
        // caller error) propagate cleanly — they are deliberately NOT wrapped by
        // the engine catch below. The resolved files stay open for the whole
        // method (method-scoped `using`) so the engine reads their streams
        // directly: no temp files, nothing to leak or race on cleanup.
        using var r1 = await resolver.ResolveAsync(file1);
        using var r2 = await resolver.ResolveAsync(file2);
        using var r3 = file3 != null ? await resolver.ResolveAsync(file3) : null;
        using var r4 = file4 != null ? await resolver.ResolveAsync(file4) : null;

        var resolvedNames = new List<string> { r1.FileName, r2.FileName };
        if (r3 != null) resolvedNames.Add(r3.FileName);
        if (r4 != null) resolvedNames.Add(r4.FileName);

        try
        {
            var ext = Path.GetExtension(resolvedNames[0]);
            var outputName = $"{Path.GetFileNameWithoutExtension(resolvedNames[0])}_merged{ext}";

            // Merge entirely in memory — Merger reads the input streams and writes
            // the result to a MemoryStream. No temp files.
            using var outputMs = new MemoryStream();
            using (var merger = new GroupDocs.Merger.Merger(r1.Stream))
            {
                merger.Join(r2.Stream);
                if (r3 != null) merger.Join(r3.Stream);
                if (r4 != null) merger.Join(r4.Stream);
                merger.Save(outputMs);
            }

            var savedPath = await storage.WriteFileAsync(outputName, outputMs.ToArray(), rewrite: false);

            var names = string.Join(" + ", resolvedNames);
            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            return await output.BuildFileOutputAsync(savedPath, $"{prefix}Merged {names} into '{outputName}'");
        }
        catch (Exception ex)
        {
            // Surface the underlying engine exception instead of letting it bubble
            // to ModelContextProtocol's generic "An error occurred invoking
            // 'merge'." wrapper. Pattern per Pitfall #18.
            return FormatException(ex, resolvedNames);
        }
    }

    private static string FormatException(Exception ex, List<string> fileNames)
    {
        var sb = new StringBuilder();
        var names = fileNames.Count > 0 ? string.Join(" + ", fileNames) : "(inputs)";
        sb.Append($"Merge failed for '{names}': ");
        sb.Append($"{ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int depth = 0; inner != null && depth < 5; depth++, inner = inner.InnerException)
            sb.Append($" | inner({depth}): {inner.GetType().FullName}: {inner.Message}");
        return sb.ToString();
    }
}
