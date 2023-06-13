using System.Text;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.File;

/// <summary>
/// Write log events to a disk file.
/// </summary>
public sealed class FileSink : IFileSink, IDisposable
{
    private readonly TextWriter _output;
    private readonly FileStream _underlyingStream;
    private readonly ITextFormatter _textFormatter;
    private readonly long? _fileSizeLimitBytes;
    private readonly bool _buffered;
    private readonly object _syncRoot = new();
    private readonly WriteCountingStream? _countingStreamWrapper;

    /// <summary>Construct a <see cref="FileSink"/>.</summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="textFormatter">Formatter used to convert log events to text.</param>
    /// <param name="fileSizeLimitBytes">The approximate maximum size, in bytes, to which a log file will be allowed to grow.
    /// For unrestricted growth, pass null. The default is 1 GB. To avoid writing partial events, the last event within the limit
    /// will be written in full even if it exceeds the limit.</param>
    /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
    /// <param name="buffered">Indicates if flushing to the output file can be buffered or not. The default
    /// is false.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    /// <remarks>This constructor preserves compatibility with early versions of the public API. New code should not depend on this type.</remarks>
    /// <exception cref="IOException"></exception>
    [Obsolete("This type and constructor will be removed from the public API in a future version; use `WriteTo.FileEx()` instead.")]
    public FileSink(string path, ITextFormatter textFormatter, long? fileSizeLimitBytes, Encoding encoding = null!, bool buffered = false)
        : this(path, textFormatter, fileSizeLimitBytes, encoding, buffered, null)
    {
    }

    // This overload should be used internally; the overload above maintains compatibility with the earlier public API.
    internal FileSink(
        string path,
        ITextFormatter textFormatter,
        long? fileSizeLimitBytes,
        Encoding encoding,
        bool buffered,
        FileLifecycleHooks? hooks)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));

        if (fileSizeLimitBytes is < 0) throw new ArgumentException("Negative value provided; file size limit must be non-negative.");

        _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
        _fileSizeLimitBytes = fileSizeLimitBytes;
        _buffered = buffered;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Stream outputStream = _underlyingStream = System.IO.File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        if (_fileSizeLimitBytes != null)
        {
            outputStream = _countingStreamWrapper = new WriteCountingStream(_underlyingStream);
        }

        if (hooks != null)
        {
            outputStream = hooks.OnFileOpened(outputStream, encoding) ??
                           throw new InvalidOperationException($"The file lifecycle hook `{nameof(FileLifecycleHooks.OnFileOpened)}(...)` returned `null`.");
        }

        _output = new StreamWriter(outputStream, encoding);
    }

    bool IFileSink.EmitOrOverflow(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
        lock (_syncRoot)
        {
            if (_fileSizeLimitBytes != null)
            {
                if (_countingStreamWrapper!.CountedLength >= _fileSizeLimitBytes.Value)
                    return false;
            }

            _textFormatter.Format(logEvent, _output);
            if (!_buffered)
                _output.Flush();

            return true;
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
        }
    }

    /// <inheritdoc cref="FlushToDisk" />
    public void FlushToDisk()
    {
        lock (_syncRoot)
        {
            _output.Flush();
            _underlyingStream.Flush(true);
        }
    }
}