using System.Text;

namespace Serilog.Sinks.FileEx;

/// <summary>
/// Enables hooking into log file lifecycle events.
/// Hooks run synchronously and therefore may affect responsiveness of the application if long operations are performed.
/// </summary>
public abstract class FileLifecycleHooks
{
    /// <summary>
    /// Initialize or wrap the <paramref name="underlyingStream"/> opened on the log file. This can be used to write
    /// file headers, or wrap the stream in another that adds buffering, compression, encryption, etc. The underlying
    /// file may or may not be empty when this method is called.
    /// </summary>
    /// <remarks>
    /// A value must be returned from overrides of this method. Serilog will flush and/or dispose the returned value, but will not
    /// dispose the stream initially passed in unless it is itself returned.
    /// </remarks>
    /// <param name="path">The full path to the log file.</param>
    /// <param name="underlyingStream">The underlying <see cref="Stream"/> opened on the log file.</param>
    /// <param name="encoding">The encoding to use when reading/writing to the stream.</param>
    /// <returns>The <see cref="Stream"/> Serilog should use when writing events to the log file.</returns>
    public virtual Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding) => OnFileOpened(underlyingStream, encoding);

    /// <summary>
    /// Initialize or wrap the <paramref name="underlyingStream"/> opened on the log file. This can be used to write
    /// file headers, or wrap the stream in another that adds buffering, compression, encryption, etc. The underlying
    /// file may or may not be empty when this method is called.
    /// </summary>
    /// <remarks>
    /// A value must be returned from overrides of this method. Serilog will flush and/or dispose the returned value, but will not
    /// dispose the stream initially passed in unless it is itself returned.
    /// </remarks>
    /// <param name="underlyingStream">The underlying <see cref="Stream"/> opened on the log file.</param>
    /// <param name="encoding">The encoding to use when reading/writing to the stream.</param>
    /// <returns>The <see cref="Stream"/> Serilog should use when writing events to the log file.</returns>
    public virtual Stream OnFileOpened(Stream underlyingStream, Encoding encoding) => underlyingStream;

    /// <summary>
    /// Called before an obsolete (rolling) log file is deleted.
    /// This can be used to copy old logs to an archive location or send to a backup server.
    /// </summary>
    /// <param name="path">The full path to the file being deleted.</param>
    public virtual void OnFileDeleting(string path) { }

    /// <summary>
    /// Called before the rolling log file is performed.
    /// This can be used to copy the current rolling log to an archive location or send to a backup server.
    /// </summary>
    /// <param name="path">The full path to the file being rolled.</param>
    public virtual void OnFileRolling(string path) { }

    /// <summary>
    /// Called after the rolling log file is performed.
    /// This can be used to copy the current rolled log to an archive location or send to a backup server.
    /// </summary>
    /// <param name="path">The full path (with new file name) to the rolled file.</param>
    public virtual void OnFileRolled(string path) { }

    /// <summary>
    /// Creates a chain of <see cref="FileLifecycleHooks"/> that have their methods called sequentially
    /// Can be used to compose <see cref="FileLifecycleHooks"/> together; e.g. add header information to each log file and
    /// compress it.
    /// </summary>
    /// <example>
    /// <code>
    /// var hooks = new GZipHooks().Then(new HeaderWriter("File Header"));
    /// </code>
    /// </example>
    /// <param name="next">The next <see cref="FileLifecycleHooks"/> to have its methods called in the chain</param>
    /// <returns></returns>
    public FileLifecycleHooks Then(FileLifecycleHooks next)
    {
        return new FileLifeCycleHookChain(this, next);
    }
}