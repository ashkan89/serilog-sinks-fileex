#if ATOMIC_APPEND

using Serilog.Events;
using Serilog.Formatting;
using System.Security.AccessControl;
using System.Text;

namespace Serilog.Sinks.File.FileEx;

/// <summary>
/// Write log events to a disk file.
/// </summary>
[Obsolete("This type will be removed from the public API in a future version; use `WriteTo.PersistentFile(shared: true)` instead.")]
public sealed class SharedFileSink : IFileSink, IDisposable
{
    private readonly MemoryStream _writeBuffer;
    private readonly string _path;
    private readonly TextWriter _output;
    private readonly ITextFormatter _textFormatter;
    private readonly long? _fileSizeLimitBytes;
    private readonly object _syncRoot = new();

    // The stream is reopened with a larger buffer if atomic writes beyond the current buffer size are needed.
    private FileStream _fileOutput;
    private int _fileStreamBufferLength = DefaultFileStreamBufferLength;

    private const int DefaultFileStreamBufferLength = 4096;

    /// <summary>Construct a <see cref="FileSink"/>.</summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="textFormatter">Formatter used to convert log events to text.</param>
    /// <param name="fileSizeLimitBytes">The approximate maximum size, in bytes, to which a log file will be allowed to grow.
    /// For unrestricted growth, pass null. The default is 1 GB. To avoid writing partial events, the last event within the limit
    /// will be written in full even if it exceeds the limit.</param>
    /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    /// <exception cref="IOException"></exception>
    public SharedFileSink(string path, ITextFormatter textFormatter, long? fileSizeLimitBytes, Encoding encoding = null)
    {
        if (fileSizeLimitBytes is < 0)
            throw new ArgumentException("Negative value provided; file size limit must be non-negative");

        _path = path ?? throw new ArgumentNullException(nameof(path));
        _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
        _fileSizeLimitBytes = fileSizeLimitBytes;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // FileSystemRights.AppendData sets the Win32 FILE_APPEND_DATA flag. On Linux this is O_APPEND, but that API is not yet
        // exposed by .NET Core.
        _fileOutput = new FileStream(
            path,
            FileMode.Append,
            FileSystemRights.AppendData,
            FileShare.ReadWrite,
            _fileStreamBufferLength,
            FileOptions.None);

        _writeBuffer = new MemoryStream();
        _output = new StreamWriter(_writeBuffer,
            encoding);
    }

    bool IFileSink.EmitOrOverflow(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

        lock (_syncRoot)
        {
            try
            {
                _textFormatter.Format(logEvent, _output);
                _output.Flush();
                var bytes = _writeBuffer.GetBuffer();
                var length = (int)_writeBuffer.Length;
                if (length > _fileStreamBufferLength)
                {
                    var oldOutput = _fileOutput;

                    _fileOutput = new FileStream(
                        _path,
                        FileMode.Append,
                        FileSystemRights.AppendData,
                        FileShare.ReadWrite,
                        length,
                        FileOptions.None);
                    _fileStreamBufferLength = length;

                    oldOutput.Dispose();
                }

                if (_fileSizeLimitBytes != null)
                {
                    try
                    {
                        if (_fileOutput.Length >= _fileSizeLimitBytes.Value)
                            return false;
                    }
                    catch (FileNotFoundException) { } // Cheaper and more reliable than checking existence
                }

                _fileOutput.Write(bytes, 0, length);
                _fileOutput.Flush();
                return true;
            }
            catch
            {
                // Make sure there's no leftover cruft in there.
                _output.Flush();
                throw;
            }
            finally
            {
                _writeBuffer.Position = 0;
                _writeBuffer.SetLength(0);
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
            _fileOutput.Dispose();
        }
    }

    /// <inheritdoc />
    public void FlushToDisk()
    {
        lock (_syncRoot)
        {
            _output.Flush();
            _fileOutput.Flush(true);
        }
    }
}

#endif