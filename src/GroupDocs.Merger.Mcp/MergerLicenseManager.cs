using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GroupDocs.Merger.Mcp;

public class MergerLicenseManager : LicenseManager
{
    public MergerLicenseManager(IOptions<McpConfig> config, ILogger<LicenseManager> logger) : base(config, logger) { }
    protected override void SetLicenseFromPath(string licensePath)
    {
        new GroupDocs.Merger.License().SetLicense(licensePath);
    }
}
