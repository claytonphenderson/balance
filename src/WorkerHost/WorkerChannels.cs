using System.Threading.Channels;
using Models;

namespace WorkerHost;

public sealed class WorkerChannels
{
    public Channel<Expense> Enrichment { get; } = Channel.CreateUnbounded<Expense>();
    public Channel<Expense> Summary { get; } = Channel.CreateUnbounded<Expense>();
}