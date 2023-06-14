namespace Serilog.Sinks.FileEx;

internal class RollingLogFile
{
    public RollingLogFile(string filename, DateTime? dateTime, int? sequenceNumber)
    {
        Filename = filename;
        DateTime = dateTime;
        SequenceNumber = sequenceNumber;
    }

    public string Filename { get; }

    public DateTime? DateTime { get; }

    public int? SequenceNumber { get; }
}