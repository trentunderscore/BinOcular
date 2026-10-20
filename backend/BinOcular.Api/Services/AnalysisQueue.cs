
using System.Threading.Channels;

namespace BinOcular.Api.Services;

public sealed class AnalysisQueue
{
    private readonly Channel<Guid> _queue =
        Channel.CreateBounded<Guid>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

    public async ValueTask EnqueueAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        await _queue.Writer.WriteAsync(
            analysisId,
            cancellationToken);
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }
}
