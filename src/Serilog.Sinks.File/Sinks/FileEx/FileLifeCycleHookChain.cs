using System.Text;

namespace Serilog.Sinks.FileEx;

internal class FileLifeCycleHookChain : FileLifecycleHooks
{
    private readonly FileLifecycleHooks _first;
    private readonly FileLifecycleHooks _second;

    public FileLifeCycleHookChain(FileLifecycleHooks first, FileLifecycleHooks second)
    {
        _first = first ?? throw new ArgumentNullException(nameof(first));
        _second = second ?? throw new ArgumentNullException(nameof(second));
    }

    public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
    {
        var firstStreamResult = _first.OnFileOpened(path, underlyingStream, encoding);
        var secondStreamResult = _second.OnFileOpened(path, firstStreamResult, encoding);

        return secondStreamResult;
    }

    public override void OnFileDeleting(string path)
    {
        _first.OnFileDeleting(path);
        _second.OnFileDeleting(path);
    }
}