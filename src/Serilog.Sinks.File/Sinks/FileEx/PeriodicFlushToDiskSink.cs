using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace Serilog.Sinks.FileEx;

/// <summary>
/// A sink wrapper that periodically flushes the wrapped sink to disk.
/// </summary>
[Obsolete("This type will be removed from the public API in a future version; use `WriteTo.PersistentFile(flushToDiskInterval:)` instead.")]
public class PeriodicFlushToDiskSink : ILogEventSink, IDisposable
{
    private readonly ILogEventSink _sink;
    private readonly Timer _timer;
    private int _flushRequired;

    /// <summary>
    /// Construct a <see cref="PeriodicFlushToDiskSink"/> that wraps
    /// <paramref name="sink"/> and flushes it at the specified <paramref name="flushInterval"/>.
    /// </summary>
    /// <param name="sink">The sink to wrap.</param>
    /// <param name="flushInterval">The interval at which to flush the underlying sink.</param>
    /// <exception cref="ArgumentNullException"/>
    public PeriodicFlushToDiskSink(ILogEventSink sink, TimeSpan flushInterval)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));

        if (sink is IFlushableFileSink flushable)
        {
            _timer = new Timer(_ => FlushToDisk(flushable), null, flushInterval, flushInterval);
        }
        else
        {
            _timer = new Timer(_ => { }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            SelfLog.WriteLine("{0} configured to flush {1}, but {2} not implemented", typeof(PeriodicFlushToDiskSink), sink, nameof(IFlushableFileSink));
        }
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        _sink.Emit(logEvent);
        Interlocked.Exchange(ref _flushRequired, 1);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer.Dispose();
        (_sink as IDisposable)?.Dispose();
    }

    private void FlushToDisk(IFlushableFileSink flushable)
    {
        try
        {
            if (Interlocked.CompareExchange(ref _flushRequired, 0, 1) == 1)
            {
                // May throw ObjectDisposedException, since we're not trying to synchronize
                // anything here in the wrapper.
                flushable.FlushToDisk();
            }
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("{0} could not flush the underlying sink to disk: {1}", typeof(PeriodicFlushToDiskSink), ex);
        }
    }
}