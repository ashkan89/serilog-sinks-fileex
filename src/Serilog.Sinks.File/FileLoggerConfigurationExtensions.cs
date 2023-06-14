using System.ComponentModel;
using System.Text;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;
using Serilog.Sinks.FileEx;

namespace Serilog;

/// <summary>Extends <see cref="LoggerConfiguration"/> with methods to add file sinks.</summary>
public static class FileLoggerConfigurationExtensions
{
    private const int DefaultRetainedFileCountLimit = 31; // A long month of logs
    private const long DefaultFileSizeLimitBytes = 1L * 1024 * 1024 * 1024;
    private const string DefaultOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Write log events to the specified file.
    /// </summary>
    /// <param name="sinkConfiguration">Logger sink configuration.</param>
    /// <param name="path">Path to the file.</param>
    /// <param name="periodFormat"></param>
    /// <param name="restrictedToMinimumLevel">The minimum level for
    /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
    /// <param name="levelSwitch">A switch allowing the pass-through minimum level
    /// to be changed at runtime.</param>
    /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
    /// <param name="outputTemplate">A message template describing the format used to write to the sink.
    /// the default is "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}".</param>
    /// <param name="fileSizeLimitBytes">The approximate maximum size, in bytes, to which a log file will be allowed to grow.
    /// For unrestricted growth, pass null. The default is 1 GB. To avoid writing partial events, the last event within the limit
    /// will be written in full even if it exceeds the limit.</param>
    /// <param name="buffered">Indicates if flushing to the output file can be buffered or not. The default
    /// is false.</param>
    /// <param name="shared">Allow the log file to be shared by multiple processes. The default is false.</param>
    /// <param name="flushToDiskInterval">If provided, a full disk flush will be performed periodically at the specified interval.</param>
    /// <param name="rollingInterval">The interval at which logging will roll over to a new file.</param>
    /// <param name="rollOnFileSizeLimit">If <code>true</code>, a new file will be created when the file size limit is reached. FileNames
    /// will have a number appended in the format <code>_NNN</code>, with the first filename given no number.</param>
    /// <param name="retainedFileCountLimit">The maximum number of log files that will be retained,
    /// including the current log file. For unlimited retention, pass null. The default is 31.</param>
    /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
    /// <param name="hooks">Optionally enables hooking into log file lifecycle events.</param>
    /// <param name="preserveLogFilename">Avoid the log file name to change after each roll, on roll the log file is copied to a new file and the current file is restarted empty</param>
    /// <param name="rollOnEachProcessRun">Roll the name of the log file every time the process starts.</param>
    /// <param name="useLastWriteAsTimestamp">When the file is rolled, the last write timestamp of the log file is used instead of the current timestamp.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    public static LoggerConfiguration FileEx(
        this LoggerSinkConfiguration sinkConfiguration,
        string path,
        string? periodFormat = default,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        string outputTemplate = DefaultOutputTemplate,
        IFormatProvider formatProvider = null!,
        long? fileSizeLimitBytes = DefaultFileSizeLimitBytes,
        LoggingLevelSwitch levelSwitch = null!,
        bool buffered = false,
        bool shared = false,
        TimeSpan? flushToDiskInterval = null,
        RollingInterval rollingInterval = RollingInterval.Infinite,
        bool rollOnFileSizeLimit = false,
        int? retainedFileCountLimit = DefaultRetainedFileCountLimit,
        Encoding encoding = null!,
        FileLifecycleHooks hooks = null!,
        bool preserveLogFilename = true,
        bool rollOnEachProcessRun = true,
        bool useLastWriteAsTimestamp = false)
    {
        if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (outputTemplate == null) throw new ArgumentNullException(nameof(outputTemplate));

        var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
        return FileEx(sinkConfiguration, formatter, path, periodFormat, restrictedToMinimumLevel, fileSizeLimitBytes,
            levelSwitch, buffered, shared, flushToDiskInterval,
            rollingInterval, rollOnFileSizeLimit, retainedFileCountLimit, encoding, hooks,
            preserveLogFilename, rollOnEachProcessRun, useLastWriteAsTimestamp);
    }

    /// <summary>
    /// Write log events to the specified file.
    /// </summary>
    /// <param name="sinkConfiguration">Logger sink configuration.</param>
    /// <param name="formatter">A formatter, such as <see cref="JsonFormatter"/>, to convert the log events into
    /// text for the file. If control of regular text formatting is required, use the other
    /// overload of <see />
    /// and specify the outputTemplate parameter instead.
    /// </param>
    /// <param name="path">Path to the file.</param>
    /// <param name="periodFormat"></param>
    /// <param name="restrictedToMinimumLevel">The minimum level for
    /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
    /// <param name="levelSwitch">A switch allowing the pass-through minimum level
    /// to be changed at runtime.</param>
    /// <param name="fileSizeLimitBytes">The approximate maximum size, in bytes, to which a log file will be allowed to grow.
    /// For unrestricted growth, pass null. The default is 1 GB. To avoid writing partial events, the last event within the limit
    /// will be written in full even if it exceeds the limit.</param>
    /// <param name="buffered">Indicates if flushing to the output file can be buffered or not. The default
    /// is false.</param>
    /// <param name="shared">Allow the log file to be shared by multiple processes. The default is false.</param>
    /// <param name="flushToDiskInterval">If provided, a full disk flush will be performed periodically at the specified interval.</param>
    /// <param name="rollingInterval">The interval at which logging will roll over to a new file.</param>
    /// <param name="rollOnFileSizeLimit">If <code>true</code>, a new file will be created when the file size limit is reached. FileNames
    /// will have a number appended in the format <code>_NNN</code>, with the first filename given no number.</param>
    /// <param name="retainedFileCountLimit">The maximum number of log files that will be retained,
    /// including the current log file. For unlimited retention, pass null. The default is 31.</param>
    /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
    /// <param name="hooks">Optionally enables hooking into log file lifecycle events.</param>
    /// <param name="preserveLogFilename">Preserve the name of the log file, and copy content on roll to a new file.</param>
    /// <param name="rollOnEachProcessRun">Roll the name of the log file every time the process starts.</param>
    /// <param name="useLastWriteAsTimestamp">When the file is rolled, the last write timestamp of the log file is used instead of the current timestamp.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    public static LoggerConfiguration FileEx(
        this LoggerSinkConfiguration sinkConfiguration,
        ITextFormatter formatter,
        string path,
        string? periodFormat = default,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        long? fileSizeLimitBytes = DefaultFileSizeLimitBytes,
        LoggingLevelSwitch levelSwitch = null!,
        bool buffered = false,
        bool shared = false,
        TimeSpan? flushToDiskInterval = null,
        RollingInterval rollingInterval = RollingInterval.Infinite,
        bool rollOnFileSizeLimit = false,
        int? retainedFileCountLimit = DefaultRetainedFileCountLimit,
        Encoding encoding = null!,
        FileLifecycleHooks hooks = null!,
        bool preserveLogFilename = true,
        bool rollOnEachProcessRun = true,
        bool useLastWriteAsTimestamp = false)
    {
        if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
        if (formatter == null) throw new ArgumentNullException(nameof(formatter));
        if (path == null) throw new ArgumentNullException(nameof(path));

        return ConfigureFile(sinkConfiguration.Sink, formatter, path, restrictedToMinimumLevel, fileSizeLimitBytes, levelSwitch,
            buffered, false, shared, flushToDiskInterval, encoding, rollingInterval, rollOnFileSizeLimit,
            retainedFileCountLimit, hooks, periodFormat, preserveLogFilename, rollOnEachProcessRun, useLastWriteAsTimestamp);
    }

    /// <summary>
    /// Write log events to the specified file.
    /// </summary>
    /// <param name="sinkConfiguration">Logger sink configuration.</param>
    /// <param name="formatter">A formatter, such as <see cref="JsonFormatter"/>, to convert the log events into
    /// text for the file. If control of regular text formatting is required, use the other
    /// overload of <see />
    /// and specify the outputTemplate parameter instead.
    /// </param>
    /// <param name="path">Path to the file.</param>
    /// <param name="restrictedToMinimumLevel">The minimum level for
    /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
    /// <param name="levelSwitch">A switch allowing the pass-through minimum level
    /// to be changed at runtime.</param>
    /// <param name="periodFormat"></param>
    /// <returns>Configuration object allowing method chaining.</returns>
    /// <remarks>The file will be written using the UTF-8 character set.</remarks>
    [Obsolete("New code should not be compiled against this obsolete overload"), EditorBrowsable(EditorBrowsableState.Never)]
    public static LoggerConfiguration FileEx(
        this LoggerAuditSinkConfiguration sinkConfiguration,
        ITextFormatter formatter,
        string path,
        LogEventLevel restrictedToMinimumLevel,
        LoggingLevelSwitch levelSwitch,
        string? periodFormat = default)
    {
        return FileEx(sinkConfiguration, formatter, path, periodFormat, restrictedToMinimumLevel, levelSwitch);
    }

    /// <summary>
    /// Write audit log events to the specified file.
    /// </summary>
    /// <param name="sinkConfiguration">Logger sink configuration.</param>
    /// <param name="path">Path to the file.</param>
    /// <param name="restrictedToMinimumLevel">The minimum level for
    /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
    /// <param name="levelSwitch">A switch allowing the pass-through minimum level
    /// to be changed at runtime.</param>
    /// <param name="periodFormat"></param>
    /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
    /// <param name="outputTemplate">A message template describing the format used to write to the sink.
    /// the default is "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}".</param>
    /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
    /// <param name="hooks">Optionally enables hooking into log file lifecycle events.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    public static LoggerConfiguration FileEx(
        this LoggerAuditSinkConfiguration sinkConfiguration,
        string path,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        string outputTemplate = DefaultOutputTemplate,
        string? periodFormat = default,
        IFormatProvider formatProvider = null!,
        LoggingLevelSwitch levelSwitch = null!,
        Encoding encoding = null!,
        FileLifecycleHooks hooks = null!)
    {
        if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (outputTemplate == null) throw new ArgumentNullException(nameof(outputTemplate));

        var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
        return FileEx(sinkConfiguration, formatter, path, periodFormat, restrictedToMinimumLevel, levelSwitch, encoding, hooks);
    }

    /// <summary>
    /// Write audit log events to the specified file.
    /// </summary>
    /// <param name="sinkConfiguration">Logger sink configuration.</param>
    /// <param name="formatter">A formatter, such as <see cref="JsonFormatter"/>, to convert the log events into
    /// text for the file. If control of regular text formatting is required, use the other
    /// overload of <see />
    /// and specify the outputTemplate parameter instead.
    /// </param>
    /// <param name="path">Path to the file.</param>
    /// <param name="periodFormat"></param>
    /// <param name="restrictedToMinimumLevel">The minimum level for
    /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
    /// <param name="levelSwitch">A switch allowing the pass-through minimum level
    /// to be changed at runtime.</param>
    /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
    /// <param name="hooks">Optionally enables hooking into log file lifecycle events.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    public static LoggerConfiguration FileEx(
        this LoggerAuditSinkConfiguration sinkConfiguration,
        ITextFormatter formatter,
        string path,
        string? periodFormat = default,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        LoggingLevelSwitch levelSwitch = null!,
        Encoding encoding = null!,
        FileLifecycleHooks hooks = null!)
    {
        if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
        if (formatter == null) throw new ArgumentNullException(nameof(formatter));
        if (path == null) throw new ArgumentNullException(nameof(path));

        return ConfigureFile(sinkConfiguration.Sink, formatter, path, restrictedToMinimumLevel, null, levelSwitch, false, true,
            false, null, encoding, RollingInterval.Infinite, false, null, hooks, periodFormat);
    }

    private static LoggerConfiguration ConfigureFile(
        this Func<ILogEventSink, LogEventLevel, LoggingLevelSwitch, LoggerConfiguration> addSink,
        ITextFormatter formatter,
        string path,
        LogEventLevel restrictedToMinimumLevel,
        long? fileSizeLimitBytes,
        LoggingLevelSwitch levelSwitch,
        bool buffered,
        bool propagateExceptions,
        bool shared,
        TimeSpan? flushToDiskInterval,
        Encoding encoding,
        RollingInterval rollingInterval,
        bool rollOnFileSizeLimit,
        int? retainedFileCountLimit,
        FileLifecycleHooks hooks,
        string? periodFormat = default,
        bool preserveLogFilename = true,
        bool rollOnEachProcessRun = true,
        bool useLastWriteAsTimestamp = false)
    {
        if (addSink == null) throw new ArgumentNullException(nameof(addSink));
        if (formatter == null) throw new ArgumentNullException(nameof(formatter));
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (fileSizeLimitBytes is < 0) throw new ArgumentException(@"Negative value provided; file size limit must be non-negative.", nameof(fileSizeLimitBytes));
        if (retainedFileCountLimit is < 1) throw new ArgumentException(@"At least one file must be retained.", nameof(retainedFileCountLimit));
        if (shared && buffered) throw new ArgumentException(@"Buffered writes are not available when file sharing is enabled.", nameof(buffered));
        if (shared && hooks != null) throw new ArgumentException(@"File lifecycle hooks are not currently supported for shared log files.", nameof(hooks));

        ILogEventSink sink;

        if (rollOnFileSizeLimit || rollingInterval != RollingInterval.Infinite)
        {
            sink = new RollingFileSink(path, formatter, fileSizeLimitBytes, retainedFileCountLimit,
                encoding, buffered, shared, rollingInterval, rollOnFileSizeLimit, hooks, periodFormat,
                preserveLogFilename, rollOnEachProcessRun, useLastWriteAsTimestamp);
        }
        else
        {
            try
            {
                if (shared)
                {
#pragma warning disable 618
                    sink = new SharedFileSink(path, formatter, fileSizeLimitBytes, encoding);
#pragma warning restore 618
                }
                else
                {
                    sink = new FileSink(path, formatter, fileSizeLimitBytes, encoding, buffered, hooks);
                }
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Unable to open file sink for {0}: {1}", path, ex);

                if (propagateExceptions)
                    throw;

                return addSink(new NullSink(), LevelAlias.Maximum, null!);
            }
        }

        if (flushToDiskInterval.HasValue)
        {
#pragma warning disable 618
            sink = new PeriodicFlushToDiskSink(sink, flushToDiskInterval.Value);
#pragma warning restore 618
        }

        return addSink(sink, restrictedToMinimumLevel, levelSwitch);
    }
}
