# Serilog.Sinks.File.GzArchive [![Build status](https://ci.appveyor.com/api/projects/status/hh9gymy0n6tne46j?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-file) [![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.File.GzArchive.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.File.GzArchive/) [![Documentation](https://img.shields.io/badge/docs-wiki-yellow.svg)](https://github.com/serilog/serilog/wiki) [![Join the chat at https://gitter.im/serilog/serilog](https://img.shields.io/gitter/room/serilog/serilog.svg)](https://gitter.im/serilog/serilog)

A `FileLifecycleHooks`-based plugin for the [Serilog FileEx Sink](https://github.com/ashkan89/serilog-sinks-fileex) that works with rolling log files, archiving completed log files before they are deleted or being rolled by Serilog's retention mechanism.

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