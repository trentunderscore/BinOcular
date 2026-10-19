using BinOcular.Api.Models;

namespace BinOcular.Api.Analysis;

public interface IAnalysisTool
{
    string Name { get; }
    bool IsCompatible(string filePath) => true;

    Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}