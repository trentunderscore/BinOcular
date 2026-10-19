
namespace BinOcular.Api.Models;

public sealed class ToolResult
{
    public string Tool { get; set; } = "";
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = "";
    public string StandardError { get; set; } = "";
}
