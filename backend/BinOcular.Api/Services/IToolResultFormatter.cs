using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public interface IToolResultFormatter
{
    string ToolName { get; }

    FormattedOutput Format(ToolResult result);
}