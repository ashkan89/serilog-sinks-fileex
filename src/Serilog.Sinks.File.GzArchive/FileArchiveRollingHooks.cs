// Copyright 2023 Ashkan Shirian and cocowalla
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

using Serilog.Debugging;
using System.IO.Compression;
using Serilog.Sinks.FileEx;
using System.Text;
using System.Globalization;

namespace Serilog.Sinks.File.GzArchive;

/// <summary>
/// FileArchiveRollingHooks
/// </summary>
public class FileArchiveRollingHooks : FileLifecycleHooks
{
    private readonly CompressionLevel _compressionLevel;
    private readonly int _bufferSize;
    private readonly string? _fileNameFormat;
    private readonly int _retainedFileCountLimit;
    private readonly string? _targetDirectory;
    private readonly CompressScenario _compressScenario;
    private const int DefaultRetainedFileCountLimit = 31;
    private const string DefaultFileFormat = "yyyyMMdd";
    private const int DefaultBufferSize = 32 * 1024;

    /// <summary>
    /// Create a new FileArchiveRollingHooks, which will archive completed log files before they are deleted by Serilog's retention mechanism
    /// </summary>
    /// <param name="compressionLevel">
    /// Level of GZIP compression to use. Use CompressionLevel.NoCompression if no compression is required
    /// </param>
    /// <param name="targetDirectory">
    /// Directory in which to archive files to. Use null if compressed, archived files should remain in the same folder
    /// </param>
    public FileArchiveRollingHooks(CompressionLevel compressionLevel = CompressionLevel.Fastest, string targetDirectory = null!)
    {
        if (compressionLevel == CompressionLevel.NoCompression && targetDirectory == null)
            throw new ArgumentException($"Either {nameof(compressionLevel)} or {nameof(targetDirectory)} must be set");

        _fileNameFormat = DefaultFileFormat;
        _compressionLevel = compressionLevel;
        _bufferSize = DefaultBufferSize;
        _targetDirectory = targetDirectory;
        _compressScenario = CompressScenario.OnDelete;
    }

    /// <summary>
    /// FileArchiveRollingHooks
    /// </summary>
    /// <param name="retainedFileCountLimit"></param>
    /// <param name="compressionLevel"></param>
    /// <param name="compressScenario"></param>
    /// <param name="bufferSize"></param>
    /// <param name="fileNameFormat"></param>
    /// <param name="targetDirectory"></param>
    /// <exception cref="ArgumentException"></exception>
    public FileArchiveRollingHooks(CompressionLevel compressionLevel = CompressionLevel.Fastest,
        int retainedFileCountLimit = DefaultRetainedFileCountLimit,
        CompressScenario compressScenario = CompressScenario.OnDelete,
        int bufferSize = DefaultBufferSize,
      string? fileNameFormat = default,
      string? targetDirectory = default)
    {
        if (retainedFileCountLimit <= 0)
            throw new ArgumentException($@"{nameof(retainedFileCountLimit)} must be greater than zero", nameof(retainedFileCountLimit));

        if (targetDirectory != null && TokenExpander.IsTokenised(targetDirectory))
            throw new ArgumentException($@"{nameof(targetDirectory)} must not be tokenised when using {nameof(retainedFileCountLimit)}", nameof(targetDirectory));

        if (compressionLevel == CompressionLevel.NoCompression)
            throw new ArgumentException($@"{nameof(compressionLevel)} must not be 'NoCompression' when using {nameof(retainedFileCountLimit)}", nameof(compressionLevel));

        _compressionLevel = compressionLevel;
        _bufferSize = bufferSize;
        _fileNameFormat = fileNameFormat ?? DefaultFileFormat;
        _retainedFileCountLimit = retainedFileCountLimit;
        _targetDirectory = targetDirectory;
        _compressScenario = compressScenario;
    }

    /// <summary>
    /// OnFileDeleting
    /// </summary>
    /// <param name="underlyingStream"></param>
    /// <param name="_"></param>
    public override Stream OnFileOpened(Stream underlyingStream, Encoding _)
    {
        if (_compressScenario.HasFlag(CompressScenario.CompressStream))
        {
            var compressStream = new GZipStream(underlyingStream, _compressionLevel);
            return new BufferedStream(compressStream, _bufferSize);
        }

        return underlyingStream;
    }

    /// <summary>
    /// OnFileDeleting
    /// </summary>
    /// <param name="path"></param>
    public override void OnFileDeleting(string path)
    {
        try
        {
            if (_compressScenario.HasFlag(CompressScenario.OnDelete))
                CompressFile(path);
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("Error while archiving file {0}: {1}", path, ex);
            throw;
        }
    }

    /// <summary>
    /// OnFileRolling
    /// </summary>
    /// <param name="path"></param>
    public override void OnFileRolling(string path)
    {
        try
        {
            if (_compressScenario.HasFlag(CompressScenario.OnRoll))
                CompressFile(path);
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("Error while archiving file {0}: {1}", path, ex);
            throw;
        }
    }

    private bool IsArchivePathTokenised => _targetDirectory != null && TokenExpander.IsTokenised(_targetDirectory);

    private void RemoveExcessFiles(string folder)
    {
        var searchPattern = _compressionLevel != CompressionLevel.NoCompression ? "*.gz" : "*.*";

        foreach (var fileInfo in Directory.GetFiles(folder, searchPattern)
                     .Select(f => new FileInfo(f))
                     .OrderByDescending(f => f, LogFileComparer.Default)
                     .Skip(_retainedFileCountLimit).ToList())
        {
            try
            {
                fileInfo.Delete();
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Error while deleting file {0}: {1}", fileInfo.FullName, ex);
            }
        }
    }

    private string GenerateFileName(string path)
    {
        int? sequenceNumber = default;
        var currentDirectory = Path.GetDirectoryName(path);
        var filenamePrefix = Path.GetFileNameWithoutExtension(path);
        var filenameSuffix = Path.GetExtension(path);
        var currentCheckPoint = DateTime.Now.ToString(_fileNameFormat, CultureInfo.InvariantCulture);
        string filePath;

        do
        {
            var token = currentCheckPoint;

            if (sequenceNumber != null)
                token += "_" + sequenceNumber.Value.ToString("000", CultureInfo.InvariantCulture);

            filePath = _compressionLevel != CompressionLevel.NoCompression
                ? Path.Combine(currentDirectory!, $"{filenamePrefix}{token}.gz")
                : Path.Combine(currentDirectory!, $"{filenamePrefix}{token}{filenameSuffix}");

            if (sequenceNumber == null)
            {
                sequenceNumber = 1;
            }
            else
            {
                sequenceNumber++;
            }

        } while (System.IO.File.Exists(filePath));

        return Path.GetFileName(filePath);
    }

    private void CompressFile(string path)
    {
        var path2 = GenerateFileName(path);
        var str1 = _targetDirectory != null ? TokenExpander.Expand(_targetDirectory) : Path.GetDirectoryName(path)!;

        if (!Directory.Exists(str1))
            Directory.CreateDirectory(str1);

        var str2 = Path.Combine(str1, path2);

        if (_compressionLevel == CompressionLevel.NoCompression)
        {
            System.IO.File.Copy(path, str2, true);
        }
        else
        {
            using var fileStream1 = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var fileStream2 = new FileStream(str2, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            using var destination = new GZipStream(fileStream2, _compressionLevel);

            fileStream1.CopyTo(destination);
        }

        if (_retainedFileCountLimit <= 0 || IsArchivePathTokenised)
            return;

        RemoveExcessFiles(str1);
    }

    private class LogFileComparer : IComparer<FileInfo>
    {
        public static readonly IComparer<FileInfo> Default = new LogFileComparer();

        public int Compare(FileInfo? x, FileInfo? y)
        {
            if (x == null && y == null)
                return 0;

            if (x == null)
                return -1;

            return y == null ? 1 : string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}