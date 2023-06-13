using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Serilog.Sinks.File.Tests.Support;

internal class TempFolder : IDisposable
{
    private static readonly Guid Session = Guid.NewGuid();

    private readonly string _tempFolder;

    public TempFolder(string name = null!)
    {
        _tempFolder = System.IO.Path.Combine(
            Environment.GetEnvironmentVariable("TMP") ?? Environment.GetEnvironmentVariable("TMPDIR") ?? "/tmp",
            "Serilog.Sinks.File.Tests",
            Session.ToString("n"),
            name);

        Directory.CreateDirectory(_tempFolder);
    }

    public string Path => _tempFolder;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempFolder))
                Directory.Delete(_tempFolder, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public static TempFolder ForCaller([CallerMemberName] string caller = null!, [CallerFilePath] string sourceFileName = "")
    {
        if (caller == null) throw new ArgumentNullException(nameof(caller));
        if (sourceFileName == null) throw new ArgumentNullException(nameof(sourceFileName));

        var folderName = System.IO.Path.GetFileNameWithoutExtension(sourceFileName) + "_" + caller;

        return new TempFolder(folderName);
    }

    public string AllocateFilename(string ext = null!)
    {
        return System.IO.Path.Combine(Path, Guid.NewGuid().ToString("n") + "." + ext);
    }
}