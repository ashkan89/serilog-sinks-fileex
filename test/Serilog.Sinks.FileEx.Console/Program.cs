using System.IO.Compression;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.File.GzArchive;
using Serilog.Sinks.FileEx;

SelfLog.Enable(Console.Out);

var sw = System.Diagnostics.Stopwatch.StartNew();

Log.Logger = new LoggerConfiguration()
    .WriteTo
    .FileEx("logs/LogFile.log", "-yyyy-MM-dd-HHmm", LogEventLevel.Debug, rollingInterval: RollingInterval.Minute, rollOnEachProcessRun: false,
        fileSizeLimitBytes: (10L * 1024), rollOnFileSizeLimit: true, retainedFileCountLimit: 5, preserveLogFilename: true,
        hooks: new FileArchiveRollingHooks(CompressionLevel.SmallestSize,
            targetDirectory: "logs", fileNameFormat: "-yyyy-MM-dd",
            //compressSenario: CompressSenario.OnDelete | CompressSenario.OnRoll,
            compressSenario: CompressSenario.OnRoll,
            retainedFileCountLimit: 31))
    .CreateLogger();

for (var i = 0; i < 2000000; ++i)
{
    Thread.Sleep(100);
    Log.Information("Hello, file logger!");
}

Log.CloseAndFlush();

sw.Stop();

Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds} ms");

Console.WriteLine("Press any key to delete the temporary log file...");
Console.ReadKey(true);