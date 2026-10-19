using BinOcular.Api.Models;
using System.Diagnostics;

namespace BinOcular.Api.Tools;

public sealed class ToolRunner
{
    public async Task<ToolResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new ToolResult
        {
            Tool = executable,
            ExitCode = process.ExitCode,
            StandardOutput = await stdoutTask,
            StandardError = await stderrTask
        };
    }
}
