using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.FileEx;
/// <summary>
/// An instance of this sink may be substituted when an instance of the
/// <see cref="NullSink"/> is unable to be constructed.
/// </summary>
internal class NullSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
    }
}