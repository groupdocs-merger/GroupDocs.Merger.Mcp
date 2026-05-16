using System.ComponentModel;
using System.Text;
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
        var tempInput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{ext}");
        var tempOutputDir = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempOutputDir);

        try
        {
            await using (var fs = File.Create(tempInput))
                await resolved.Stream.CopyToAsync(fs);

            var outputPattern = Path.Combine(tempOutputDir, $"{baseName}_{{0}}.{{1}}");
            var splitOptions = new SplitOptions(outputPattern, pageNumbers);

            using var merger = password != null
                ? new GroupDocs.Merger.Merger(tempInput, new LoadOptions(password))
                : new GroupDocs.Merger.Merger(tempInput);

            merger.Split(splitOptions);

            var outputFiles = Directory.GetFiles(tempOutputDir).OrderBy(f => f).ToList();
            var savedPaths = new List<string>();
            foreach (var outputFile in outputFiles)
            {
                var bytes = await File.ReadAllBytesAsync(outputFile);
                var savedPath = await storage.WriteFileAsync(Path.GetFileName(outputFile), bytes, rewrite: false);
                savedPaths.Add(savedPath);
            }

            var prefix = licenseManager.IsLicensed ? string.Empty : "[Evaluation mode] Output may include watermarks.\n\n";
            var fileList = string.Join("\n", savedPaths);
            return $"{prefix}Split '{resolved.FileName}' into {savedPaths.Count} file(s):\n{fileList}";
        }
        catch (Exception ex)
        {
            return FormatException(ex, resolved.FileName, pages);
        }
        finally
        {
            if (File.Exists(tempInput)) File.Delete(tempInput);
            if (Directory.Exists(tempOutputDir)) Directory.Delete(tempOutputDir, recursive: true);
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
