using BinOcular.Api.Models;
using BinOcular.Api.Analysis;

namespace BinOcular.Api.Tools;

public class Sha256Tool : IAnalysisTool
{
    public string Name => "SHA256 Hash Tool";

    public bool IsCompatible(string filePath)
    {
        // This tool is compatible with any file type, so we return true.
        return true;
    }

    public async Task<ToolResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Use the ToolRunner to execute the sha256sum command on the provided file.
        var toolRunner = new ToolRunner();
        var result = await toolRunner.RunAsync("sha256sum", new[] { filePath }, cancellationToken);

        return result;
    }
}