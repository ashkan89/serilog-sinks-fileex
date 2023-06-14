using System.Text;

namespace Serilog.Sinks.FileEx;

/// <summary>
/// FileLifeCycleHookChain
/// </summary>
public class FileLifeCycleHookChain : FileLifecycleHooks
{
    private readonly FileLifecycleHooks _first;
    private readonly FileLifecycleHooks _second;

    /// <summary>
    /// FileLifeCycleHookChain
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public FileLifeCycleHookChain(FileLifecycleHooks first, FileLifecycleHooks second)
    {
        _first = first ?? throw new ArgumentNullException(nameof(first));
        _second = second ?? throw new ArgumentNullException(nameof(second));
    }

    /// <summary>
    /// OnFileOpened
    /// </summary>
    /// <param name="path"></param>
    /// <param name="underlyingStream"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
    {
        var firstStreamResult = _first.OnFileOpened(path, underlyingStream, encoding);
        var secondStreamResult = _second.OnFileOpened(path, firstStreamResult, encoding);

        return secondStreamResult;
    }

    /// <summary>
    /// OnFileDeleting
    /// </summary>
    /// <param name="path"></param>
    public override void OnFileDeleting(string path)
    {
        _first.OnFileDeleting(path);
        _second.OnFileDeleting(path);
    }
}