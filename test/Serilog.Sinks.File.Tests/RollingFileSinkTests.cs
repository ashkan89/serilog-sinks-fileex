using System.IO.Compression;
using System.Reflection;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.File.Tests.Support;
using Serilog.Sinks.FileEx;

namespace Serilog.Sinks.File.Tests
{
    public class RollingFileSinkTests
    {
        [Fact]
        public void LogEventsAreEmittedToTheFileNamedAccordingToTheEventTimestamp()
        {
            TestRollingEventSequence(Some.InformationEvent());
        }

        [Fact]
        public void EventsAreWrittenWhenSharingIsEnabled()
        {
            TestRollingEventSequence(
                (pf, wt) => wt.FileEx(pf, shared: true, rollingInterval: RollingInterval.Day),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void EventsAreWrittenWhenBufferingIsEnabled()
        {
            TestRollingEventSequence(
                (pf, wt) => wt.FileEx(pf, buffered: true, rollingInterval: RollingInterval.Day),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void EventsAreWrittenWhenDiskFlushingIsEnabled()
        {
            // Doesn't test flushing, but ensures we haven't broken basic logging
            TestRollingEventSequence(
                (pf, wt) => wt.FileEx(pf, flushToDiskInterval: TimeSpan.FromMilliseconds(50), rollingInterval: RollingInterval.Day),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void WhenTheDateChangesTheCorrectFileIsWritten()
        {
            var e1 = Some.InformationEvent();
            var e2 = Some.InformationEvent(e1.Timestamp.AddDays(1));
            TestRollingEventSequence(e1, e2);
        }

        [Fact]
        public void WhenRetentionCountIsSetOldFilesAreDeleted()
        {
            LogEvent e1 = Some.InformationEvent(),
                e2 = Some.InformationEvent(e1.Timestamp.AddDays(-1)),
                e3 = Some.InformationEvent(e2.Timestamp.AddDays(-5));

            var pathFormat = "";

            TestRollingEventSequence(
                (pf, wt) =>
                {
                    pathFormat = pf;
                    foreach (var @event in new[] { e1, e2, e3 })
                    {
                        var dummyFile = pf.Replace(".txt", @event.Timestamp.ToString("yyyyMMdd") + ".txt");
                        System.IO.File.WriteAllText(dummyFile, "");
                    }

                    wt.FileEx(pf, retainedFileCountLimit: 2,
                        rollingInterval: RollingInterval.Day);
                },
                new[] { e1, e2, e3 },
                files =>
                {
                    Assert.Equal(1, files.Count);
                    Assert.True(System.IO.File.Exists(files[0]));

                    var folder = new FileInfo(pathFormat).Directory?.FullName ?? "";
                    Assert.Equal(2, Directory.GetFiles(folder).Length);
                });
        }

        [Fact]
        public void WhenSizeLimitIsBreachedNewFilesCreated()
        {
            var fileName = Some.String() + ".txt";
            using var temp = new TempFolder();
            using var log = new LoggerConfiguration()
                .WriteTo.FileEx(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1)
                .CreateLogger();
            LogEvent e1 = Some.InformationEvent(),
                e2 = Some.InformationEvent(e1.Timestamp),
                e3 = Some.InformationEvent(e1.Timestamp);

            log.Write(e1); log.Write(e2); log.Write(e3);

            var files = Directory.GetFiles(temp.Path)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.Equal(3, files.Length);
            Assert.True(files[0].EndsWith(fileName), files[0]);
            Assert.True(files[1].EndsWith("_001.txt"), files[1]);
            Assert.True(files[2].EndsWith("_002.txt"), files[2]);
        }

        [Fact]
        public void WhenStreamWrapperSpecifiedIsUsedForRolledFiles()
        {
            var gzipWrapper = new GZipHooks();
            var fileName = Some.String() + ".txt";

            using var temp = new TempFolder();
            string[] files;
            var logEvents = new[]
            {
                Some.InformationEvent(),
                Some.InformationEvent(),
                Some.InformationEvent()
            };

            using (var log = new LoggerConfiguration()
                       .WriteTo.FileEx(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1, hooks: gzipWrapper)
                       .CreateLogger())
            {

                foreach (var logEvent in logEvents)
                {
                    log.Write(logEvent);
                }

                files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(3, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith("_001.txt"), files[1]);
                Assert.True(files[2].EndsWith("_002.txt"), files[2]);
            }
            //with persistent file name we must reverse the first and last file of the array because, the last file we write to is always the same file
            //sorting by name will put this file at the first place instead of the last.
            var t = files[0];
            for (var i = 0; i < files.Length - 1; i++)
                files[i] = files[i + 1];
            files[files.Length - 1] = t;



            // Ensure the data was written through the wrapping GZipStream, by decompressing and comparing against
            // what we wrote
            foreach (var f1 in files)
            {
                using var textStream = new MemoryStream();
                using (var fs = System.IO.File.OpenRead(f1))
                using (var decompressStream = new GZipStream(fs, CompressionMode.Decompress))
                {
                    decompressStream.CopyTo(textStream);
                }

                textStream.Position = 0;
                var lines = textStream.ReadAllLines();

                Assert.Single(lines);
                Assert.EndsWith(logEvents[0].MessageTemplate.Text, logEvents[0].MessageTemplate.Text);
            }
        }

        [Fact]
        public void IfTheLogFolderDoesNotExistItWillBeCreated()
        {
            var fileName = Some.String() + "-{Date}.txt";
            var temp = Some.TempFolderPath();
            var folder = Path.Combine(temp, Guid.NewGuid().ToString());
            var pathFormat = Path.Combine(folder, fileName);

            ILogger log = null!;

            try
            {
                log = new LoggerConfiguration()
                    .WriteTo.FileEx(pathFormat, retainedFileCountLimit: 3, rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                log.Write(Some.InformationEvent());

                Assert.True(Directory.Exists(folder));
            }
            finally
            {
                var disposable = (IDisposable)log;
                disposable.Dispose();
                Directory.Delete(temp, true);
            }
        }

        [Fact]
        public void AssemblyVersionIsFixedAt200()
        {
            var assembly = typeof(FileLoggerConfigurationExtensions).GetTypeInfo().Assembly;
            Assert.Equal("2.0.0.0", assembly.GetName().Version!.ToString(4));
        }

        [Fact]
        public void LogFilenameShouldNotChangeAfterRollOnFileSize()
        {
            var fileName = Some.String() + ".txt";
            using var temp = new TempFolder();
            using var log = new LoggerConfiguration()
                .WriteTo.FileEx(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1, preserveLogFilename: true)
                .CreateLogger();
            LogEvent e1 = Some.InformationEvent(),
                e2 = Some.InformationEvent(e1.Timestamp),
                e3 = Some.InformationEvent(e1.Timestamp);

            log.Write(e1); log.Write(e2); log.Write(e3);

            var files = Directory.GetFiles(temp.Path)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Assert.Equal(3, files.Length);
            Assert.True(files[0].EndsWith(fileName), files[0]);
            Assert.True(files[1].EndsWith("_001.txt"), files[1]);
            Assert.True(files[2].EndsWith("_002.txt"), files[2]);
        }

        //  var e1 = Some.InformationEvent();
        //var e2 = Some.InformationEvent(e1.Timestamp.AddDays(1));
        [Fact]
        static void LogFilenameShouldNotChangeAfterRollOnDate()
        {

            const string fileName = "Serilog.log";
            using var temp = new TempFolder();
            using var log = new LoggerConfiguration()
                .WriteTo.FileEx(Path.Combine(temp.Path, fileName), retainedFileCountLimit: null,
                    preserveLogFilename: true, rollingInterval: RollingInterval.Day)
                .CreateLogger();
            LogEvent e1 = Some.InformationEvent(),
                e2 = Some.InformationEvent(e1.Timestamp.AddDays(1)),
                e3 = Some.InformationEvent(e2.Timestamp.AddDays(5));
            Clock.SetTestDateTimeNow(e1.Timestamp.DateTime);
            log.Write(e1);
            Clock.SetTestDateTimeNow(e2.Timestamp.DateTime);
            log.Write(e2);
            Clock.SetTestDateTimeNow(e3.Timestamp.DateTime);
            log.Write(e3);

            var files = Directory.GetFiles(temp.Path)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.Equal(3, files.Length);
            Assert.True(files[0].EndsWith(fileName), files[0]);
            Assert.True(files[1].EndsWith(e2.Timestamp.DateTime.ToString("yyyyMMdd") + ".txt"), files[1]);
            Assert.True(files[2].EndsWith(e3.Timestamp.DateTime.ToString("yyyyMMdd") + ".txt"), files[2]);
        }

        [Fact]
        static void LogFilenameShouldNotChangeOnMultipleRunsWhenRollOnEachProcessRunIsFalse()
        {
            const string fileName = "Serilog.log";
            using (var temp = new TempFolder())
            {
                MakeRunAndWriteLog(temp);
                MakeRunAndWriteLog(temp);
                MakeRunAndWriteLog(temp);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Single(files);
                Assert.True(files[0].EndsWith(fileName), files[0]);
            }

            void MakeRunAndWriteLog(TempFolder temp)
            {
                using var log = new LoggerConfiguration()
                    .WriteTo.FileEx(Path.Combine(temp.Path, fileName), retainedFileCountLimit: null,
                        preserveLogFilename: true, rollingInterval: RollingInterval.Day,
                        rollOnEachProcessRun: false)
                    .CreateLogger();
                var e1 = Some.InformationEvent();
                Clock.SetTestDateTimeNow(e1.Timestamp.DateTime);
                log.Write(e1);
            }
        }

        [Fact]
        static void LogFilenameShouldChangeOnMultipleRunsWhenRollOnEachProcessRunIsTrue()
        {
            const string fileName = "Serilog.log";
            using (var temp = new TempFolder())
            {
                MakeRunAndWriteLog(temp, out var t1);
                MakeRunAndWriteLog(temp, out _);
                MakeRunAndWriteLog(temp, out _);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(3, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith(t1.ToString("yyyyMMdd") + ".txt"), files[1]);
                Assert.True(files[2].EndsWith(t1.ToString("yyyyMMdd") + "_001.txt"), files[1]);
            }

            void MakeRunAndWriteLog(TempFolder temp, out DateTime timestamp)
            {
                var file = Path.Combine(temp.Path, fileName);

                using (var log = new LoggerConfiguration()
                    .WriteTo.FileEx(file, retainedFileCountLimit: null,
                        preserveLogFilename: true, rollingInterval: RollingInterval.Day,
                        rollOnEachProcessRun: true)
                    .CreateLogger())
                {
                    var e1 = Some.InformationEvent();
                    timestamp = e1.Timestamp.DateTime;
                    Clock.SetTestDateTimeNow(timestamp);
                    log.Write(e1);
                }

                System.IO.File.SetLastWriteTime(file, timestamp);
            }
        }

        [Fact]
        static void LogFilenameRollsCorrectlyWhenRollOnEachProcessRunIsTrue()
        {
            const string fileName = "Serilog.log";
            using (var temp = new TempFolder())
            {
                MakeRunAndWriteLog(temp, 0, out var t0);
                MakeRunAndWriteLog(temp, 0, out _);
                MakeRunAndWriteLog(temp, 2, out var t1);
                MakeRunAndWriteLog(temp, 2, out _);
                MakeRunAndWriteLog(temp, 3, out var t2);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(5, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith(t0.ToString("yyyyMMddHH") + ".txt"), files[1]);
                Assert.True(files[2].EndsWith(t1.ToString("yyyyMMddHH") + ".txt"), files[2]);
                Assert.True(files[3].EndsWith(t1.ToString("yyyyMMddHH") + "_001.txt"), files[3]);
                Assert.True(files[4].EndsWith(t2.ToString("yyyyMMddHH") + ".txt"), files[4]);
            }

            void MakeRunAndWriteLog(TempFolder temp, int hoursToAdd, out DateTime timestamp)
            {
                string file = Path.Combine(temp.Path, fileName);

                using (var log = new LoggerConfiguration()
                    .WriteTo.FileEx(file, retainedFileCountLimit: null,
                        preserveLogFilename: true, rollingInterval: RollingInterval.Hour,
                        rollOnEachProcessRun: true)
                    .CreateLogger())
                {
                    var e1 = Some.InformationEvent();
                    timestamp = e1.Timestamp.DateTime.AddHours(hoursToAdd);
                    Clock.SetTestDateTimeNow(timestamp);
                    log.Write(e1);
                }

                System.IO.File.SetLastWriteTime(file, timestamp);
            }
        }

        [Fact]
        static void LogFilenameRollsCorrectlyWhenRollOnEachProcessRunAndUseLastWriteAsTimestampAreTrue()
        {
            const string fileName = "Serilog.log";
            using (var temp = new TempFolder())
            {
                MakeRunAndWriteLog(temp, 0, out var t0);
                MakeRunAndWriteLog(temp, 0, out _);
                MakeRunAndWriteLog(temp, 2, out var t1);
                MakeRunAndWriteLog(temp, 2, out _);
                MakeRunAndWriteLog(temp, 3, out _);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(5, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith(t0.ToString("yyyyMMddHH") + ".txt"), string.Join(Environment.NewLine, files));
                Assert.True(files[2].EndsWith(t0.ToString("yyyyMMddHH") + "_001.txt"), string.Join(Environment.NewLine, files));
                Assert.True(files[3].EndsWith(t1.ToString("yyyyMMddHH") + ".txt"), string.Join(Environment.NewLine, files));
                Assert.True(files[4].EndsWith(t1.ToString("yyyyMMddHH") + "_001.txt"), string.Join(Environment.NewLine, files));
            }

            void MakeRunAndWriteLog(TempFolder temp, int hoursToAdd, out DateTime timestamp)
            {
                string file = Path.Combine(temp.Path, fileName);

                using (var log = new LoggerConfiguration()
                    .WriteTo.FileEx(file, retainedFileCountLimit: null,
                        preserveLogFilename: true, rollingInterval: RollingInterval.Hour,
                        rollOnEachProcessRun: true, useLastWriteAsTimestamp: true)
                    .CreateLogger())
                {
                    var e1 = Some.InformationEvent(DateTimeOffset.Parse("2021-06-03"));
                    timestamp = e1.Timestamp.DateTime.AddHours(hoursToAdd);
                    Clock.SetTestDateTimeNow(timestamp);
                    log.Write(e1);
                }

                System.IO.File.SetLastWriteTime(file, timestamp);
            }
        }

        [Fact]
        static void TestLogShouldRollWhenOverFlowed()
        {
            var temp = new TempFolder();
            const string fileName = "log.txt";
            for (var i = 0; i < 4; i++)
            {
                using var log = new LoggerConfiguration()
                    .WriteTo.FileEx(Path.Combine(temp.Path, fileName), fileSizeLimitBytes: 1000, rollOnFileSizeLimit: true, preserveLogFilename: true)
                    .CreateLogger();
                var longString = new string('0', 1000);
                log.Information(longString);
            }
            //we should have four files
            var files = Directory.GetFiles(temp.Path)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Assert.Equal(4, files.Length);
            Assert.True(files[0].EndsWith(fileName), files[0]);
            Assert.True(files[1].EndsWith("_001.txt"), files[1]);
            Assert.True(files[2].EndsWith("_002.txt"), files[2]);
            Assert.True(files[3].EndsWith("_003.txt"), files[2]);
            temp.Dispose();
        }

        static void TestRollingEventSequence(params LogEvent[] events)
        {
            TestRollingEventSequence(
                (pf, wt) => wt.FileEx(pf, retainedFileCountLimit: null, rollingInterval: RollingInterval.Day),
                events);
        }

        static void TestRollingEventSequence(
            Action<string, LoggerSinkConfiguration> configureFile,
            IEnumerable<LogEvent> events,
            Action<IList<string>> verifyWritten = null!)
        {
            var fileName = Some.String() + "-.txt";
            var folder = Some.TempFolderPath();
            var pathFormat = Path.Combine(folder, fileName);

            var config = new LoggerConfiguration();
            configureFile(pathFormat, config.WriteTo);
            var log = config.CreateLogger();

            var verified = new HashSet<string>();

            try
            {
                foreach (var @event in events)
                {
                    Clock.SetTestDateTimeNow(@event.Timestamp.DateTime);
                    log.Write(@event);
                    //we have persistent file therefore the current file is always the path
                    Assert.True(System.IO.File.Exists(pathFormat));
                    verified.Add(pathFormat);
                }
            }
            finally
            {
                log.Dispose();
                verifyWritten(verified.ToList());
                Directory.Delete(folder, true);
            }
        }
    }
}