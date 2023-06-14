#if OS_MUTEX

using System.Text;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.FileEx;

/// <summary>
/// Write log events to a disk file.
/// </summary>
[Obsolete("This type will be removed from the public API in a future version; use `WriteTo.PersistentFile(shared: true)` instead.")]
public sealed class SharedFileSink : IFileSink, IDisposable
{
    private readonly TextWriter _output;
    private readonly FileStream _underlyingStream;
    private readonly ITextFormatter _textFormatter;
    private readonly long? _fileSizeLimitBytes;
    private readonly object _syncRoot = new();

    private const string MutexNameSuffix = ".serilog";
    private const int MutexWaitTimeout = 10000;
    private readonly Mutex _mutex;

    /// <summary>Construct a <see cref="FileSink"/>.</summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="textFormatter">Formatter used to convert log events to text.</param>
    /// <param name="fileSizeLimitBytes">The approximate maximum size, in bytes, to which a log file will be allowed to grow.
    /// For unrestricted growth, pass null. The default is 1 GB. To avoid writing partial events, the last event within the limit
    /// will be written in full even if it exceeds the limit.</param>
    /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    /// <remarks>The file will be written using the UTF-8 character set.</remarks>
    /// <exception cref="IOException"></exception>
    public SharedFileSink(string path, ITextFormatter textFormatter, long? fileSizeLimitBytes, Encoding encoding = null!)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (fileSizeLimitBytes is < 0)
            throw new ArgumentException("Negative value provided; file size limit must be non-negative");
        _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
        _fileSizeLimitBytes = fileSizeLimitBytes;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var mutexName = Path.GetFullPath(path).Replace(Path.DirectorySeparatorChar, ':') + MutexNameSuffix;
        _mutex = new Mutex(false, mutexName);
        _underlyingStream = System.IO.File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _output = new StreamWriter(_underlyingStream, encoding);
    }

    bool IFileSink.EmitOrOverflow(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

        lock (_syncRoot)
        {
            if (!TryAcquireMutex())
                return true; // We didn't overflow, but, roll-on-size should not be attempted

            try
            {
                _underlyingStream.Seek(0, SeekOrigin.End);
                if (_fileSizeLimitBytes != null)
                {
                    if (_underlyingStream.Length >= _fileSizeLimitBytes.Value)
                        return false;
                }

                _textFormatter.Format(logEvent, _output);
                _output.Flush();
                _underlyingStream.Flush();
                return true;
            }
            finally
            {
                ReleaseMutex();
            }
        }
    }

    /// <summary>
    /// Emit the provided log event to the sink.
    /// </summary>
    /// <param name="logEvent">The log event to write.</param>
    public void Emit(LogEvent logEvent)
    {
        ((IFileSink)this).EmitOrOverflow(logEvent);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_syncRoot)
        {
            _output.Dispose();
            _mutex.Dispose();
        }
    }

    /// <inheritdoc />
    public void FlushToDisk()
    {
        lock (_syncRoot)
        {
            if (!TryAcquireMutex())
                return;

            try
            {
                _underlyingStream.Flush(true);
            }
            finally
            {
                ReleaseMutex();
            }
        }
    }

    private bool TryAcquireMutex()
    {
        try
        {
            if (!_mutex.WaitOne(MutexWaitTimeout))
            {
                SelfLog.WriteLine("Shared file mutex could not be acquired within {0} ms", MutexWaitTimeout);
                return false;
            }
        }
        catch (AbandonedMutexException)
        {
            SelfLog.WriteLine("Inherited shared file mutex after abandonment by another process");
        }

        return true;
    }

    private void ReleaseMutex()
    {
        _mutex.ReleaseMutex();
    }
}

#endif