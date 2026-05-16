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

        var inputs = new List<FileInput> { file1, file2 };
        if (file3 != null) inputs.Add(file3);
        if (file4 != null) inputs.Add(file4);

        var tempFiles = new List<string>();
        var resolvedNames = new List<string>();
        var tempOutput = string.Empty;

        // Outer try carries only the `finally` cleanup. The resolution loop
        // sits here (NOT inside the engine catch) so resolver errors —
        // file-not-found is a caller error — propagate cleanly per Pitfall #18.
        try
        {
            foreach (var input in inputs)
            {
                using var resolved = await resolver.ResolveAsync(input);
                resolvedNames.Add(resolved.FileName);
                var tempPath = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{Path.GetExtension(resolved.FileName)}");
                await using (var fs = File.Create(tempPath))
                    await resolved.Stream.CopyToAsync(fs);
                tempFiles.Add(tempPath);
            }

            // Inner try/catch wraps the engine work — engine exceptions are
            // surfaced as a descriptive string, not bubbled to MCP's wrapper.
            try
            {
                var ext = Path.GetExtension(resolvedNames[0]);
                var outputName = $"{Path.GetFileNameWithoutExtension(resolvedNames[0])}_merged{ext}";
                tempOutput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{ext}");

                using (var merger = new GroupDocs.Merger.Merger(tempFiles[0]))
                {
                    for (int i = 1; i < tempFiles.Count; i++)
                        merger.Join(tempFiles[i]);
                    merger.Save(tempOutput);
                }

                var bytes = await File.ReadAllBytesAsync(tempOutput);
                var savedPath = await storage.WriteFileAsync(outputName, bytes, rewrite: false);

                var names = string.Join(" + ", resolvedNames);
                var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
                return await output.BuildFileOutputAsync(savedPath, $"{prefix}Merged {names} into '{outputName}'");
            }
            catch (Exception ex)
            {
                return FormatException(ex, resolvedNames);
            }
        }
        finally
        {
            foreach (var t in tempFiles)
                if (File.Exists(t)) File.Delete(t);
            if (!string.IsNullOrEmpty(tempOutput) && File.Exists(tempOutput))
                File.Delete(tempOutput);
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
