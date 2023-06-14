using Serilog.Debugging;
using System.IO.Compression;
using Serilog.Sinks.FileEx;

namespace Serilog.Sinks.File.GzArchive;

/// <summary>
/// FileArchiveRollingHooks
/// </summary>
public class FileArchiveRollingHooks : FileLifecycleHooks
{
    private readonly CompressionLevel _compressionLevel;
    private readonly string? _fileNameFormat;
    private readonly int _retainedFileCountLimit;
    private readonly string? _targetDirectory;
    private const int DefaultRetainedFileCountLimit = 31;
    private const string DefaultFileFormat = "yyyyMMdd";

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
        _targetDirectory = targetDirectory;
    }

    /// <summary>
    /// FileArchiveRollingHooks
    /// </summary>
    /// <param name="retainedFileCountLimit"></param>
    /// <param name="compressionLevel"></param>
    /// <param name="fileNameFormat"></param>
    /// <param name="targetDirectory"></param>
    /// <exception cref="ArgumentException"></exception>
    public FileArchiveRollingHooks(CompressionLevel compressionLevel = CompressionLevel.Fastest,
        int retainedFileCountLimit = DefaultRetainedFileCountLimit,
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
        _fileNameFormat = fileNameFormat ?? DefaultFileFormat;
        _retainedFileCountLimit = retainedFileCountLimit;
        _targetDirectory = targetDirectory;
    }

    /// <summary>
    /// OnFileDeleting
    /// </summary>
    /// <param name="path"></param>
    public override void OnFileDeleting(string path)
    {
        try
        {
            var path2 = GenerateFileName(path);
            var str1 = _targetDirectory != null ? TokenExpander.Expand(_targetDirectory) : Path.GetDirectoryName(path);

            if (!Directory.Exists(str1))
                Directory.CreateDirectory(str1!);

            var str2 = Path.Combine(str1!, path2);

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

            RemoveExcessFiles(str1!);
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
        return _compressionLevel != CompressionLevel.NoCompression ?
            $"{Path.GetFileName(path)}{DateTime.Now.ToString(_fileNameFormat)}.gz" :
            $"{Path.GetFileNameWithoutExtension(path)}{DateTime.Now.ToString(_fileNameFormat)}.{Path.GetExtension(path)}";
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