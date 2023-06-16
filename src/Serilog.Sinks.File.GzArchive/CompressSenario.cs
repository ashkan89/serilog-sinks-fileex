namespace Serilog.Sinks.File.GzArchive;

[Flags]
public enum CompressSenario
{
    OnRoll = 1,
    OnDelete = 2
}