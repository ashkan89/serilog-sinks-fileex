# Serilog.Sinks.FileEx [![Build status](https://ci.appveyor.com/api/projects/status/hh9gymy0n6tne46j?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-file) [![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.FileEx.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.FileEx/) [![Documentation](https://img.shields.io/badge/docs-wiki-yellow.svg)](https://github.com/serilog/serilog/wiki) [![Join the chat at https://gitter.im/serilog/serilog](https://img.shields.io/gitter/room/serilog/serilog.svg)](https://gitter.im/serilog/serilog)

Writes [Serilog](https://serilog.net) events to one or more text files.

### Getting started

Install the [Serilog.Sinks.FileEx](https://www.nuget.org/packages/Serilog.Sinks.FileEx/) package from NuGet:

```powershell
Install-Package Serilog.Sinks.FileEx
```

To configure the sink in C# code, call `WriteTo.FileEx()` during logger configuration:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.FileEx("log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

This will append the time period to the filename, creating a file set like:

```
log20180631.txt
log20180701.txt
log20180702.txt
```

If you want to preserve filename when rolling the logs, so it's always the filename that gets written to.
It's mostly useful for other tools like fail2ban and elastic filebeat to be able to continuously read the log.
Set the parameter preserveLogFileName to true. log.txt will always have the latest logs,     content will be copied to a new file and then flushed on file rolling.

```csharp
var log = new LoggerConfiguration()
    .WriteTo.FileEx("log.txt", rollingInterval: RollingInterval.Day, preserveLogFileName: true)
    .CreateLogger();
```

This will preserve the log file like:

```
log.txt
log20180631.txt
log20180701.txt
log20180702.txt
```

If you want to configure a custom rollInterval date format, Set the parameter periodFormat to a date format string.

```csharp
var log = new LoggerConfiguration()
    .WriteTo.FileEx("log.txt", "_yyyy-MM-dd", rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

This will append the date and time period to the filename using the custom format, create a log set like:

```
log_2018-06-31.txt
log_2018-07-01.txt
log_2018-07-02.txt
```

If you want to create a new log file on every application startup, Set the parameter rollOnEachProcessRun to true.a new log file will create and roll the latest one on every application startup.

```csharp
var log = new LoggerConfiguration()
    .WriteTo.FileEx("log.txt", rollOnEachProcessRun: true)
    .CreateLogger();
```

This will create a new log file on every application startup, create a log set like:

```
log_001.txt
log_002.txt
log_003.txt
```

If you want to use the log file last write time as the rolling date and time instead of the checkpoint, Set the parameter useLastWriteAsTimestamp to true. 

```csharp
var log = new LoggerConfiguration()
    .WriteTo.FileEx("log.txt", "_yyyy-MM-dd", rollingInterval: RollingInterval.Day, useLastWriteAsTimestamp: true)
    .CreateLogger();
```

This will append the last write time for the datetime format, create a log set like:

```
log_2018-06-31.txt
log_2018-07-01.txt
log_2018-07-02.txt
```

> **Important**: By default, only one process may write to a log file at a given time. See _Shared log files_ below for information on multi-process sharing.

### Limits

To avoid bringing down apps with runaway disk usage the file sink **limits file size to 1GB by default**. Once the limit is reached, no further events will be written until the next roll point (see also: [Rolling policies](#rolling-policies) below).

The limit can be changed or removed using the `fileSizeLimitBytes` parameter.

```csharp
    .WriteTo.FileEx("log.txt", fileSizeLimitBytes: null)
``` 

For the same reason, only **the most recent 31 files** are retained by default (i.e. one long month). To change or remove this limit, pass the `retainedFileCountLimit` parameter.

```csharp
    .WriteTo.FileEx("log.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: null)
```

### Rolling policies

To create a log file per day or other time period, specify a `rollingInterval` as shown in the examples above.

To roll when the file reaches `fileSizeLimitBytes`, specify `rollOnFileSizeLimit`:

```csharp
    .WriteTo.FileEx("log.txt", rollOnFileSizeLimit: true)
```

This will create a file set like:

```
log.txt
log_001.txt
log_002.txt
```

Specifying both `rollingInterval` and `rollOnFileSizeLimit` will cause both policies to be applied, while specifying neither will result in all events being written to a single file.

Old files will be cleaned up as per `retainedFileCountLimit` - the default is 31.

### XML `<appSettings>` configuration

To use the file sink with the [Serilog.Settings.AppSettings](https://github.com/serilog/serilog-settings-appsettings) package, first install that package if you haven't already done so:

```powershell
Install-Package Serilog.Settings.AppSettings
```

Instead of configuring the logger in code, call `ReadFrom.AppSettings()`:

```csharp
var log = new LoggerConfiguration()
    .ReadFrom.AppSettings()
    .CreateLogger();
```

In your application's `App.config` or `Web.config` file, specify the file sink assembly and required path format under the `<appSettings>` node:

```xml
<configuration>
  <appSettings>
    <add key="serilog:using:FileEx" value="Serilog.Sinks.FileEx" />
    <add key="serilog:write-to:FileEx.path" value="log.txt" />
```

The parameters that can be set through the `serilog:write-to:FileEx` keys are the method parameters accepted by the `WriteTo.FileEx()` configuration method. This means, for example, that the `fileSizeLimitBytes` parameter can be set with:

```xml
    <add key="serilog:write-to:FileEx.fileSizeLimitBytes" value="1234567" />
```

Omitting the `value` will set the parameter to `null`:

```xml
    <add key="serilog:write-to:FileEx.fileSizeLimitBytes" />
```

In XML and JSON configuration formats, environment variables can be used in setting values. This means, for instance, that the log file path can be based on `TMP` or `APPDATA`:

```xml
    <add key="serilog:write-to:FileEx.path" value="%APPDATA%\MyApp\log.txt" />
```

### JSON `appsettings.json` configuration

To use the file sink with _Microsoft.Extensions.Configuration_, for example with ASP.NET Core or .NET Core, use the [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) package. First install that package if you have not already done so:

```powershell
Install-Package Serilog.Settings.Configuration
```

Instead of configuring the file directly in code, call `ReadFrom.Configuration()`:

```csharp
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();
```

In your `appsettings.json` file, under the `Serilog` node, :

```json
{
  "Serilog": {
    "WriteTo": [
      { "Name": "FileEx", "Args": { "path": "log.txt", "rollingInterval": "Day" } }
    ]
  }
}
```

See the XML `<appSettings>` example above for a discussion of available `Args` options.

### Controlling event formatting

The file sink creates events in a fixed text format by default:

```
2018-07-06 09:02:17.148 +10:00 [INF] HTTP GET / responded 200 in 1994 ms
```

The format is controlled using an _output template_, which the file configuration method accepts as an `outputTemplate` parameter.

The default format above corresponds to an output template like:

```csharp
  .WriteTo.FileEx("log.txt",
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
```

##### JSON event formatting

To write events to the file in an alternative format such as [JSON](https://github.com/serilog/serilog-formatting-compact), pass an `ITextFormatter` as the first argument:

```csharp
    // Install-Package Serilog.Formatting.Compact
    .WriteTo.FileEx(new CompactJsonFormatter(), "log.txt")
```

### Shared log files

To enable multi-process shared log files, set `shared` to `true`:

```csharp
    .WriteTo.FileEx("log.txt", shared: true)
```

### Auditing

The file sink can operate as an audit file through `AuditTo`:

```csharp
    .AuditTo.FileEx("audit.txt")
```

Only a limited subset of configuration options are currently available in this mode.

### Performance

By default, the file sink will flush each event written through it to disk. To improve write performance, specifying `buffered: true` will permit the underlying stream to buffer writes.

The [Serilog.Sinks.Async](https://github.com/serilog/serilog-sinks-async) package can be used to wrap the file sink and perform all disk access on a background worker thread.

### Extensibility
[`FileLifecycleHooks`](https://github.com/ashkan89/serilog-sinks-fileex/blob/master/src/Serilog.Sinks.FileEx/Sinks/FileEx/FileLifecycleHooks.cs) provide an extensibility point that allows hooking into different parts of the life cycle of a log file.

You can create a hook by extending from [`FileLifecycleHooks`](https://github.com/ashkan89/serilog-sinks-fileex/blob/master/src/Serilog.Sinks.FileEx/Sinks/FileEx/FileLifecycleHooks.cs) and overriding the `OnFileOpened` and/or `OnFileDeleting` and/or `OnFileRolling` and/or `OnFileRolled` methods.

- `OnFileOpened` provides access to the underlying stream that log events are written to, before Serilog begins writing events. You can use this to write your own data to the stream (for example, to write a header row), or to wrap the stream in another stream (for example, to add buffering, compression or encryption)

- `OnFileDeleting` provides a means to work with obsolete rolling log files, *before* they are deleted by Serilog's retention mechanism - for example, to archive log files to another location

- `OnFileRolling` provides a means to work with rolling log files, *before* they are rolled by Serilog's retention mechanism - for example, to archive log files to another location

- `OnFileRolled` provides a means to work with rolling log files, *after* they are rolled by Serilog's retention mechanism - for example, to archive log files to another

Available hooks:
- [serilog-sinks-file-gzarchive](https://github.com/ashkan89/serilog-sinks-fileex/tree/master/src/Serilog.Sinks.File.GzArchive): compresses logs as they are written, using streaming GZIP compression and archives obsolete rolling log files before they are deleted and while rolling by Serilog's retention mechanism

_Copyright &copy; 2023 Ashkan Shirian and Serilog Contributors - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._

# Serilog.Sinks.File.GzArchive [![Build status](https://ci.appveyor.com/api/projects/status/hh9gymy0n6tne46j?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-file) [![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.File.GzArchive.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.File.GzArchive/) [![Documentation](https://img.shields.io/badge/docs-wiki-yellow.svg)](https://github.com/serilog/serilog/wiki) [![Join the chat at https://gitter.im/serilog/serilog](https://img.shields.io/gitter/room/serilog/serilog.svg)](https://gitter.im/serilog/serilog)

A [`FileLifecycleHooks`](https://github.com/ashkan89/serilog-sinks-fileex/blob/master/src/Serilog.Sinks.FileEx/Sinks/FileEx/FileLifecycleHooks.cs)-based plugin for the [Serilog FileEx Sink](https://github.com/ashkan89/serilog-sinks-fileex) that works with rolling log files, archiving completed log files before they are deleted or being rolled by Serilog's retention mechanism.

The following archive methods are supported:

- Compress logs in the same directory (using GZip compression)
- Copying logs to another directory
- Compress logs (using GZip compression) and write them to another directory
- Compress the current log stream (using GZip compression)

### Getting started

Install the [Serilog.Sinks.File.GzArchive](https://www.nuget.org/packages/Serilog.Sinks.File.GzArchive/) package from NuGet:

```powershell
Install-Package Serilog.Sinks.File.GzArchive
```

To enable archiving, use one of the new `LoggerSinkConfiguration` extensions that has a `FileLifecycleHooks` argument, and create a new `FileArchiveRollingHooks`. For example, to write GZip compressed logs to another directory (the directory will be created if it doesn't already exist):

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.FileEx("log.txt", hooks: new FileArchiveRollingHooks(CompressionLevel.SmallestSize, targetDirectory: "C:\\My\\Archive\\Path"))
    .CreateLogger();
```

Or to copy logs as-is to another directory:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.FileEx("log.txt", hooks: new FileArchiveRollingHooks(CompressionLevel.NoCompression, targetDirectory: "C:\\My\\Archive\\Path"))
    .CreateLogger();
```

Or to write GZip compressed logs to the same directory the logs are written to:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.FileEx("log.txt", hooks: new FileArchiveRollingHooks(CompressionLevel.SmallestSize))
    .CreateLogger();
```

If you want to configure a custom rolling date format, Set the parameter fileNameFormat to a date format string.

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.FileEx("log.txt", hooks: new FileArchiveRollingHooks(CompressionLevel.SmallestSize), targetDirectory: "C:\\My\\Archive\\Path", fileNameFormat: "-yyyy-MM-dd")
    .CreateLogger();
```

This will append the date and time format to the filename using the custom format, create a archive set like:

```
log_2018-06-31.gz
log_2018-07-01.gz
log_2018-07-02.gz
```

If you want to configure a custom senario for archiving, Set the parameter compressSenario to a `CompressSenario` enum value.

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.FileEx("log.txt", hooks: new FileArchiveRollingHooks(CompressionLevel.SmallestSize), targetDirectory: "C:\\My\\Archive\\Path", fileNameFormat: "-yyyy-MM-dd", compressSenario: CompressSenario.OnDelete)
    .CreateLogger();
```
Available Values for CompressSenario:

```csharp
CompressSenario.OnDelete
CompressSenario.OnRole
CompressSenario.CompressStream
```

Note that you cannot set `CompressSenario.OnDelete` or `CompressSenario.OnRolle` with `CompressSenario.CompressStream` together.

Note that archival only works with *rolling* log files, as files are only deleted or being rolled by Serilog's rolling file retention mechanism.
As is [standard with Serilog](https://github.com/serilog/serilog/wiki/Lifecycle-of-Loggers#in-all-apps), it's important to call `Log.CloseAndFlush();` before your application ends.

### Token Replacement
The `targetDirectory` constructor parameter supports replacement of tokens at runtime.

Tokens take the form `{Name:FormatString}`, where `Name` is the name of a supported token, and `FormatString` defines how the token value should be formatted.

At present, 2 tokens are supported, `UtcDate` and `Date`. These use [standard .NET date format strings](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings) to insert components of the current date/time into the path. For example, you may wish to organise archived files into folders based on the current **year** and **month**:

```csharp
new FileArchiveRollingHooks(CompressionLevel.SmallestSize, "C:\\Archive\\{UtcDate:yyyy}\\{UtcDate:MM}")
```

### Archiving policies

### JSON `appsettings.json` configuration

To use the file sink with _Microsoft.Extensions.Configuration_, for example with ASP.NET Core or .NET Core, use the [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) package. First install that package if you have not already done so:

```powershell
Install-Package Serilog.Settings.Configuration
```

Instead of configuring the file directly in code, call `ReadFrom.Configuration()`:

```csharp
using Serilog.Sinks.File.GzArchive;

namespace MyApp.Logging
{
    public class SerilogHooks
    {
        public static FileArchiveRollingHooks MyFileArchiveRollingHooks => new FileArchiveRollingHooks(CompressionLevel.SmallestSize, "C:\\My\\Archive\\Path");
    }
}
```

In your `appsettings.json` file, under the `Serilog` node, The `hooks` argument should be configured as follows:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "FileEx",
        "Args": {
          "path": "log.txt",
          "fileSizeLimitBytes": 10485760,
          "rollOnFileSizeLimit": true,
          "retainedFileCountLimit": 5,
          "hooks": "MyApp.Logging.SerilogHooks::MyFileArchiveRollingHooks, MyApp"
        }
      }
    ]
  }
}
```

To break this down a bit, what you are doing is specifying the fully qualified type name of the static class that provides your `MyFileArchiveRollingHooks`, using `Serilog.Settings.Configuration`'s special `::` syntax to point to the `MyFileArchiveRollingHooks` member.

See the XML `<appSettings>` example above for a discussion of available `Args` options.

### About `FileLifecycleHooks`
`FileLifecycleHooks` is a Serilog File Sink mechanism that allows hooking into log file lifecycle events, enabling scenarios such as wrapping the Serilog output stream in another stream, or capturing files before they are deleted or being rolled by Serilog's retention mechanism.

_Copyright &copy; 2023 Ashkan Shirian and cocowalla - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._