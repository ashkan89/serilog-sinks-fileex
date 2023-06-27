// Copyright 2013-2017 Serilog Contributors and Ashkan Shirian
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Text;
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
    private readonly TimeSpan? _retainedFileTimeLimit;
    private readonly Encoding? _encoding;
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
    private IFileSink? _currentFile;
    private int? _currentFileSequence;
    private bool _initialCall = true;
    private string? _currentFilePath;

    private readonly object _syncLock = new();


    public RollingFileSink(string path,
        ITextFormatter textFormatter,
        long? fileSizeLimitBytes,
        int? retainedFileCountLimit,
        Encoding? encoding,
        bool buffered,
        bool shared,
        RollingInterval rollingInterval,
        bool rollOnFileSizeLimit,
        FileLifecycleHooks? hooks,
        TimeSpan? retainedFileTimeLimit,
        string? periodFormat = default,
        bool preserveLogFileName = false,
        bool rollOnEachProcessRun = true,
        bool useLastWriteAsTimestamp = false)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (fileSizeLimitBytes is < 1) throw new ArgumentException("Invalid value provided; file size limit must be at least 1 byte, or null.");
        if (retainedFileCountLimit is < 1) throw new ArgumentException("Zero or negative value provided; retained file count limit must be at least 1");
        if (retainedFileTimeLimit.HasValue && retainedFileTimeLimit < TimeSpan.Zero) throw new ArgumentException("Negative value provided; retained file time limit must be non-negative.", nameof(retainedFileTimeLimit));

        _roller = new PathRoller(path, periodFormat, rollingInterval);
        _textFormatter = textFormatter;
        _fileSizeLimitBytes = fileSizeLimitBytes;
        _retainedFileCountLimit = retainedFileCountLimit;
        _retainedFileTimeLimit = retainedFileTimeLimit;
        _encoding = encoding;
        _buffered = buffered;
        _shared = shared;
        _rollOnFileSizeLimit = rollOnFileSizeLimit;
        _hooks = hooks;
        _preserveLogFileName = preserveLogFileName;
        _rollOnEachProcessRun = rollOnEachProcessRun;
        _useLastWriteAsTimestamp = useLastWriteAsTimestamp;
        _currentFileSequence = GetCurrentSequence();
        _currentFilePath = GetCurrentFilePath();
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

        lock (_syncRoot)
        {
            if (_isDisposed) throw new ObjectDisposedException("The log file has been disposed.");

            var now = Clock.DateTimeNow;
            AlignCurrentFileTo(now);

            while (_currentFile?.EmitOrOverflow(logEvent) == false && _rollOnFileSizeLimit)
            {
                AlignCurrentFileTo(now, nextSequence: true);
            }
        }
    }

    private void AlignCurrentFileTo(DateTime now, bool nextSequence = false)
    {
        if (!_nextCheckpoint.HasValue && _rollOnEachProcessRun && !nextSequence)
        {
            int? minSequence;

            if (_currentFileSequence == null)
                minSequence = null;
            else
                minSequence = _currentFileSequence.Value + 1;

            CloseFile();
            OpenFile(now, minSequence);
        }
        else if (!_nextCheckpoint.HasValue)
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

    private int? GetCurrentSequence()
    {
        var potentialMatches = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
                .Select(Path.GetFileName);

        var newestFile = _roller
            .SelectMatches(potentialMatches!)
            .OrderByDescending(m => m.DateTime)
            .ThenByDescending(m => m.SequenceNumber)
            .FirstOrDefault();

        var currentSequence = newestFile?.SequenceNumber;

        return currentSequence;
    }

    private string? GetCurrentFilePath()
    {
        var potentialMatches = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
            .Select(Path.GetFileName);

        var newestFile = _roller
            .SelectMatches(potentialMatches!)
            .OrderByDescending(m => m.DateTime)
            .ThenByDescending(m => m.SequenceNumber)
            .FirstOrDefault();

        var currentFileName = newestFile == null ? null : Path.Combine(_roller.LogFileDirectory, newestFile.FileName);

        return currentFileName;
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
                    .Select(Path.GetFileName)!;
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
            //Sequence number calculation is wrong when keeping fileName. If there is an existing log file, latestForThisCheckpoint won't be null but will report
            // a sequence number of 0 because fileName will be (log.txt), if there are two files: sequence number will report 1 (log.txt, log-001.txt).
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

        if (_preserveLogFileName && minSequence != null)
        {
            if (sequence != null)
            {
                sequence = minSequence;
            }
        }
        else if (minSequence != null)
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
                _currentFilePath = currentPath;

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
                            _hooks?.OnFileRolling(currentPath);
                            File.Move(currentPath, path);
                            _hooks?.OnFileRolled(path);

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

                ApplyRetentionPolicy(currentPath, now);
            }
        }
        else
        {
            const int maxAttempts = 3;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (currentCheckpoint.HasValue && !string.IsNullOrEmpty(_currentFilePath))
                {
                    var fileInfo = new FileInfo(_currentFilePath);

                    if (fileInfo.Exists && fileInfo.LastWriteTime < currentCheckpoint)
                    {
                        sequence = null;
                    }
                }

                _roller.GetLogFilePath(now, sequence, out var path);

                try
                {
                    var mustRoll = !string.IsNullOrEmpty(_currentFilePath) && Path.GetFileName(path) != Path.GetFileName(_currentFilePath);
                    if (mustRoll)
                    {
                        _hooks?.OnFileRolling(_currentFilePath!);
                    }

                    _currentFile = _shared
                        ?
#pragma warning disable 618
                        new SharedFileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding)
                        :
#pragma warning restore 618
                        new FileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding, _buffered, _hooks);

                    if (mustRoll)
                    {
                        _hooks?.OnFileRolled(_currentFilePath!);
                    }

                    _currentFilePath = path;
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

                ApplyRetentionPolicy(path, now);
                _nextCheckpoint = _roller.GetNextCheckpoint(now) ?? now.AddMinutes(30);

                return;
            }
        }

        _nextCheckpoint = _roller.GetNextCheckpoint(now) ?? now.AddMinutes(30);
    }

    private bool MustRoll(DateTime now)
    {
        if (_rollOnEachProcessRun && _initialCall)
        {
            _initialCall = false;
            return true;
        }

        var currentCheckpoint = _roller.GetCurrentCheckpoint(now);
        if (!currentCheckpoint.HasValue)
            return false;

        _roller.GetLogFilePath(out var currentPath);
        var fileInfo = new FileInfo(currentPath);

        return fileInfo.Exists && fileInfo.LastWriteTime < currentCheckpoint;
    }

    private void ApplyRetentionPolicy(string currentFilePath, DateTime now)
    {
        if (_retainedFileCountLimit == null && _retainedFileTimeLimit == null) return;

        var currentFileName = Path.GetFileName(currentFilePath);


        // We consider the current file to exist, even if nothing's been written yet,
        // because files are only opened on response to an event being processed.
        var potentialMatches = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
            .Select(Path.GetFileName)
            .Union(new[] { currentFileName });

        var newestFirst = _roller
            .SelectMatches(potentialMatches!)
            .OrderByDescending(m => m.DateTime)
            .ThenByDescending(m => m.SequenceNumber);
        //.Select(m => m.FileName);

        var toRemove = newestFirst
            .Where(n => _preserveLogFileName || StringComparer.OrdinalIgnoreCase.Compare(currentFileName, n.FileName) != 0)
            //.Skip(_retainedFileCountLimit.Value - 1)
            .SkipWhile((f, i) => ShouldRetainFile(f, i, now))
            .Select(x => x.FileName)
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

    private bool ShouldRetainFile(RollingLogFile file, int index, DateTime now)
    {
        if (_retainedFileCountLimit.HasValue && index >= _retainedFileCountLimit.Value - 1)
            return false;

        if (_retainedFileTimeLimit.HasValue && file.DateTime.HasValue &&
            file.DateTime.Value < now.Subtract(_retainedFileTimeLimit.Value))
        {
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_currentFile == null) return;
            CloseFile();
            _isDisposed = true;
        }
    }

    private void CloseFile()
    {
        if (_currentFile != null)
        {
            (_currentFile as IDisposable)?.Dispose();
            _currentFile = null;
        }

        _nextCheckpoint = null;
    }

    public void FlushToDisk()
    {
        lock (_syncRoot)
        {
            _currentFile?.FlushToDisk();
        }
    }
}