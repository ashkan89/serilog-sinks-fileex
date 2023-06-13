namespace Serilog.Sinks.File;

// ReSharper disable once InconsistentNaming
internal static class IOErrors
{
    public static bool IsLockedFile(IOException ex)
    {
#if HRESULTS
            var errorCode = System.Runtime.InteropServices.Marshal.GetHRForException(ex) & ((1 << 16) - 1);
            return errorCode == 32 || errorCode == 33;
#else
        return true;
#endif
    }
}