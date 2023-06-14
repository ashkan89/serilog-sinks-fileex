﻿using System.Text;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.FileEx;

internal sealed class RollingFileSink : ILogEventSink, IFlushableFileSink, IDisposable
{
    private readonly PathRoller _roller;
    private readonly ITextFormatter _textFormatter;
    private readonly long? _fileSizeLimitBytes;
    private readonly int? _retainedFileCountLimit;
    private readonly Encoding _encoding;
    private readonly bool _buffered;
    private readonly bool _shared;
    private readonly bool _rollOnFileSizeLimit;
    private readonly FileLifecycleHooks? _hooks;
    private readonly bool _preserveLogFileName;
    private readonly bool _rollOnEachProcessRun;
    private readonly bool _useLastWriteAsTimestamp;

    private readonly object _syncRoot = new();
    private bool _isDisposed;
    private DateTime? _nextCheckpoint;
    private IFileSink _currentFile = null!;
    private int? _currentFileSequence;

    private readonly object _syncLock = new();


    public RollingFileSink(string path,
        ITextFormatter textFormatter,
        long? fileSizeLimitBytes,
        int? retainedFileCountLimit,
        Encoding encoding,
        bool buffered,
        bool shared,
        RollingInterval rollingInterval,
        bool rollOnFileSizeLimit,
        FileLifecycleHooks? hooks,
        string? periodFormat = default,
        bool preserveLogFileName = false,
        bool rollOnEachProcessRun = true,
        bool useLastWriteAsTimestamp = false)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));

        if (fileSizeLimitBytes is < 0)
            throw new ArgumentException("Negative value provided; file size limit must be non-negative.");

        if (retainedFileCountLimit is < 1)
            throw new ArgumentException("Zero or negative value provided; retained file count limit must be at least 1.");

        _roller = new PathRoller(path, periodFormat, rollingInterval);
        _textFormatter = textFormatter;
        _fileSizeLimitBytes = fileSizeLimitBytes;
        _retainedFileCountLimit = retainedFileCountLimit;
        _encoding = encoding;
        _buffered = buffered;
        _shared = shared;
        _rollOnFileSizeLimit = rollOnFileSizeLimit;
        _hooks = hooks;
        _preserveLogFileName = preserveLogFileName;
        _rollOnEachProcessRun = rollOnEachProcessRun;
        _useLastWriteAsTimestamp = useLastWriteAsTimestamp;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

        lock (_syncRoot)
        {
            if (_isDisposed) throw new ObjectDisposedException("The log file has been disposed.");

            var now = Clock.DateTimeNow;
            AlignCurrentFileTo(now);

            while (_currentFile.EmitOrOverflow(logEvent) == false && _rollOnFileSizeLimit)
            {
                AlignCurrentFileTo(now, nextSequence: true);
            }
        }
    }

    private void AlignCurrentFileTo(DateTime now, bool nextSequence = false)
    {
        if (!_nextCheckpoint.HasValue)
        {
            OpenFile(now);
        }
        else if (nextSequence || now >= _nextCheckpoint.Value)
        {
            int? minSequence = null;
            if (nextSequence)
            {
                if (_currentFileSequence == null)
                    minSequence = 1;
                else
                    minSequence = _currentFileSequence.Value + 1;
            }

            CloseFile();
            OpenFile(now, minSequence);
        }
    }

    private void OpenFile(DateTime now, int? minSequence = null)
    {
        var currentCheckpoint = _roller.GetCurrentCheckpoint(now);

        // We only try periodically because repeated failures
        // to open log files REALLY slow an app down.

        var existingFiles = Enumerable.Empty<string>();
        try
        {
            if (Directory.Exists(_roller.LogFileDirectory))
            {
                existingFiles = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
                    .Select(Path.GetFileName);
            }
        }
        catch (DirectoryNotFoundException)
        {
        }

        var latestForThisCheckpoint = _roller
            .SelectMatches(existingFiles)
            .Where(m => m.DateTime == currentCheckpoint)
            .OrderByDescending(m => m.SequenceNumber)
            .FirstOrDefault();

        var sequence = latestForThisCheckpoint?.SequenceNumber;
        if (_preserveLogFileName)
        {
            //Sequence number calculation is wrong when keeping filename. If there is an existing log file, latestForThisCheckpoint won't be null but will report
            // a sequence number of 0 because filename will be (log.txt), if there are two files: sequence number will report 1 (log.txt, log-001.txt).
            // But it should report 1 in the first case and 2 in the second case.
            //
            if (sequence == null)
            {
                if (latestForThisCheckpoint != null)
                    sequence = 1;
            }
            else
            {
                sequence++;
            }
        }
        if (minSequence != null)
        {
            if (sequence == null || sequence.Value < minSequence.Value)
                sequence = minSequence;
        }

        if (_preserveLogFileName)
        {
            const int maxAttempts = 3;

            // if current file exists we rename it with rolling date
            //we lock this portion of the code to avoid another process in shared mode to move the file
            //at the same time we are moving it. It might result in a missing file exception, because the second thread will try to move a file that has
            //been already moved.
            lock (_syncLock)
            {
                _roller.GetLogFilePath(out var currentPath);
                var fileInfo = new FileInfo(currentPath);
                var mustRoll = MustRoll(now);
                //we check of we have reach file size limit, if not we keep the same file. If we don't have roll on file size enable, we will create a new file as soon as one exists even if it is empty.
                if (fileInfo.Exists && mustRoll ||
                    (fileInfo.Exists && _rollOnFileSizeLimit ? fileInfo.Length >= _fileSizeLimitBytes : (fileInfo is { Exists: true, Length: > 0 } && mustRoll)))
                {
                    for (var attempt = 0; attempt < maxAttempts; attempt++)
                    {
                        _roller.GetLogFilePath(_useLastWriteAsTimestamp ? fileInfo.LastWriteTime : now,
                            sequence, out var path);
                        try
                        {
                            File.Move(currentPath, path);
                            _currentFileSequence = sequence;
                        }
                        catch (IOException ex)
                        {
                            if (IOErrors.IsLockedFile(ex) || File.Exists(path))
                            {
                                SelfLog.WriteLine(
                                    "File target {0} was locked or exists, attempting to open next in sequence (attempt {1})",
                                    path, attempt + 1);
                                sequence = (sequence ?? 0) + 1;
                                continue;
                            }

                            throw;
                        }

                        break;
                    }
                }

                //now we open the current file
                try
                {
                    _currentFile = _shared
                        ?
#pragma warning disable 618
                        new SharedFileSink(currentPath, _textFormatter, _fileSizeLimitBytes, _encoding)
                        :
#pragma warning restore 618
                        new FileSink(currentPath, _textFormatter, _fileSizeLimitBytes, _encoding, _buffered, _hooks);
                }
                catch (IOException ex)
                {
                    if (IOErrors.IsLockedFile(ex))
                    {
                        SelfLog.WriteLine("File target {0} was locked, this should not happen", currentPath);
                    }

                    throw;
                }

                ApplyRetentionPolicy(currentPath);
            }
        }
        else
        {
            const int maxAttempts = 3;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                _roller.GetLogFilePath(now, sequence, out var path);

                try
                {
                    _currentFile = _shared
                        ?
#pragma warning disable 618
                        new SharedFileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding)
                        :
#pragma warning restore 618
                        new FileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding, _buffered, _hooks);

                    _currentFileSequence = sequence;
                }
                catch (IOException ex)
                {
                    if (IOErrors.IsLockedFile(ex))
                    {
                        SelfLog.WriteLine(
                            "File target {0} was locked, attempting to open next in sequence (attempt {1})", path,
                            attempt + 1);
                        sequence = (sequence ?? 0) + 1;
                        continue;
                    }

                    throw;
                }

                ApplyRetentionPolicy(path);
                return;
            }
        }

        _nextCheckpoint = _roller.GetNextCheckpoint(now) ?? now.AddMinutes(30);
    }

    private bool MustRoll(DateTime now)
    {
        if (_rollOnEachProcessRun)
            return true;

        var currentCheckpoint = _roller.GetCurrentCheckpoint(now);
        if (!currentCheckpoint.HasValue)
            return false;

        _roller.GetLogFilePath(out var currentPath);
        var fileInfo = new FileInfo(currentPath);

        return fileInfo.Exists && fileInfo.LastWriteTime < currentCheckpoint;
    }

    private void ApplyRetentionPolicy(string currentFilePath)
    {
        if (_retainedFileCountLimit == null) return;

        var currentFileName = Path.GetFileName(currentFilePath);


        // We consider the current file to exist, even if nothing's been written yet,
        // because files are only opened on response to an event being processed.
        var potentialMatches = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
            .Select(Path.GetFileName)
            .Union(new[] { currentFileName });

        var newestFirst = _roller
            .SelectMatches(potentialMatches)
            .OrderByDescending(m => m.DateTime)
            .ThenByDescending(m => m.SequenceNumber)
            .Select(m => m.Filename);

        var toRemove = newestFirst
            .Where(n => _preserveLogFileName || StringComparer.OrdinalIgnoreCase.Compare(currentFileName, n) != 0)
            .Skip(_retainedFileCountLimit.Value - 1)
            .ToList();

        foreach (var fullPath in toRemove.Select(obsolete => Path.Combine(_roller.LogFileDirectory, obsolete)))
        {
            try
            {
                _hooks?.OnFileDeleting(fullPath);
                File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Error {0} while removing obsolete log file {1}", ex, fullPath);
            }
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            CloseFile();
            _isDisposed = true;
        }
    }

    private void CloseFile()
    {
        (_currentFile as IDisposable)?.Dispose();
        _currentFile = null!;

        _nextCheckpoint = null;
    }

    public void FlushToDisk()
    {
        lock (_syncRoot)
        {
            _currentFile.FlushToDisk();
        }
    }
}