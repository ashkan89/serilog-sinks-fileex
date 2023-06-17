using System.Globalization;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.FileEx;

internal class PathRoller
{
    private const string PeriodMatchGroup = "period";
    private const string SequenceNumberMatchGroup = "sequence";

    private readonly string _directory;
    private readonly string _filenamePrefix;
    private readonly string _filenameSuffix;
    private readonly Regex _filenameMatcher;

    private readonly RollingInterval _interval;
    private readonly string? _periodFormat;

    public PathRoller(string path, string? periodFormat, RollingInterval interval)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));

        _interval = interval;
        _periodFormat = periodFormat ?? interval.GetFormat();

        var pathDirectory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(pathDirectory))
            pathDirectory = Directory.GetCurrentDirectory();

        _directory = Path.GetFullPath(pathDirectory);
        _filenamePrefix = Path.GetFileNameWithoutExtension(path);
        _filenameSuffix = Path.GetExtension(path);
        _filenameMatcher = new Regex(
            "^" +
            Regex.Escape(_filenamePrefix) +
            "(?<" + PeriodMatchGroup + ">.{" + _periodFormat.Length + "})" +
            "(?<" + SequenceNumberMatchGroup + ">_[0-9]{3,}){0,1}" +
            Regex.Escape(_filenameSuffix) +
            "$");

        DirectorySearchPattern = $"{_filenamePrefix}*{_filenameSuffix}";
    }

    public string LogFileDirectory => _directory;

    public string DirectorySearchPattern { get; }

    public void GetLogFilePath(DateTime date, int? sequenceNumber, out string path)
    {
        var currentCheckpoint = GetCurrentCheckpoint(date);

        var tok = currentCheckpoint?.ToString(_periodFormat, CultureInfo.InvariantCulture) ?? "";

        if (sequenceNumber != null)
            tok += "_" + sequenceNumber.Value.ToString("000", CultureInfo.InvariantCulture);

        path = Path.Combine(_directory, _filenamePrefix + tok + _filenameSuffix);
    }

    public void GetLogFilePath(out string path)
    {
        path = Path.Combine(_directory, _filenamePrefix + _filenameSuffix);
    }

    public IEnumerable<RollingLogFile> SelectMatches(IEnumerable<string> fileNames)
    {
        foreach (var filename in fileNames)
        {
            var match = _filenameMatcher.Match(filename);
            if (!match.Success)
                continue;

            int? inc = null;
            var incGroup = match.Groups[SequenceNumberMatchGroup];
            if (incGroup.Captures.Count != 0)
            {
                var incPart = incGroup.Captures[0].Value.Substring(1);
                inc = int.Parse(incPart, CultureInfo.InvariantCulture);
            }

            DateTime? period = null;
            var periodGroup = match.Groups[PeriodMatchGroup];

            if (periodGroup.Captures.Count != 0)
            {
                var dateTimePart = periodGroup.Captures[0].Value;
                if (DateTime.TryParseExact(
                    dateTimePart,
                    _periodFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dateTime))
                {
                    period = dateTime;
                }
            }

            yield return new RollingLogFile(filename, period, inc);
        }
    }

    public DateTime? GetCurrentCheckpoint(DateTime instant) => _interval.GetCurrentCheckpoint(instant);

    public DateTime? GetNextCheckpoint(DateTime instant) => _interval.GetNextCheckpoint(instant);
}