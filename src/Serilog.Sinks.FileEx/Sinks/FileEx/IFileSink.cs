using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.FileEx;

/// <summary>
/// Exists only for the convenience of <see cref="RollingFileSink"/>, which
/// switches implementations based on sharing. Would refactor, but preserving
/// backwards compatibility.
/// </summary>
internal interface IFileSink : ILogEventSink, IFlushableFileSink
{
    bool EmitOrOverflow(LogEvent logEvent);
}