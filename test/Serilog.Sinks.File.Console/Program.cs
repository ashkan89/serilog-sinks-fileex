using System.IO.Compression;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.File;
using Serilog.Sinks.File.GzArchive;

SelfLog.Enable(Console.Out);

var sw = System.Diagnostics.Stopwatch.StartNew();

//Log.Logger = new LoggerConfiguration()
//    .WriteTo
//    .FileEx("logs/IranCardExternalAPI.log", "-yyyy-MM-dd", LogEventLevel.Debug, rollingInterval: RollingInterval.Minute,
//        fileSizeLimitBytes: (1L * 1024 * 1024 * 1024), rollOnFileSizeLimit: true, retainedFileCountLimit: 5, preserveLogFilename: true,
//        hooks: new FileArchiveRollingHooks(CompressionLevel.SmallestSize,
//            targetDirectory: "logs", fileNameFormat: "-yyyy-MM-dd",
//            retainedFileCountLimit: 31))
//    .CreateLogger();

for (var i = 0; i < 1000000; ++i)
{
    Thread.Sleep(10);
    Log.Information("Hello, file logger!");
}

Log.CloseAndFlush();

sw.Stop();

Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds} ms");
Console.WriteLine($"Size: {new FileInfo("log.txt").Length}");

Console.WriteLine("Press any key to delete the temporary log file...");
Console.ReadKey(true);