using System.Diagnostics;

using mRemoteUG.Connection.Protocol;

namespace mRemoteUG.Tools;

public class PuttyTypeDetector
{
    public static PuttyType GetPuttyType()
    {
        return GetPuttyType(PuttyBase.PuttyPath);
    }

    // A version string the executable does not carry reads as "does not say so", the same
    // answer as a file that is not there.
    public static PuttyType GetPuttyType(string? filename)
    {
        if (IsPuttyNg(filename))
        {
            return PuttyType.PuttyNg;
        }
        if (IsKitty(filename))
        {
            return PuttyType.Kitty;
        }
        if (IsXming(filename))
        {
            return PuttyType.Xming;
        }

        // Check this last
        if (IsPutty(filename))
        {
            return PuttyType.Putty;
        }

        return PuttyType.Unknown;
    }

    private static bool IsPutty(string? filename)
    {
        return !string.IsNullOrEmpty(filename)
               && File.Exists(filename)
               && FileVersionInfo.GetVersionInfo(filename).InternalName?.Contains("PuTTY") == true;
    }

    private static bool IsPuttyNg(string? filename)
    {
        return !string.IsNullOrEmpty(filename)
               && File.Exists(filename)
               && FileVersionInfo.GetVersionInfo(filename).InternalName?.Contains("PuTTYNG") == true;
    }

    private static bool IsKitty(string? filename)
    {
        return !string.IsNullOrEmpty(filename)
               && File.Exists(filename)
               && FileVersionInfo.GetVersionInfo(filename).InternalName?.Contains("PuTTY") == true
               && FileVersionInfo.GetVersionInfo(filename).Comments?.Contains("KiTTY") == true;
    }

    private static bool IsXming(string? filename)
    {
        return !string.IsNullOrEmpty(filename)
               && File.Exists(filename)
               && FileVersionInfo.GetVersionInfo(filename).InternalName?.Contains("PuTTY") == true
               && FileVersionInfo.GetVersionInfo(filename).ProductVersion?.Contains("Xming") == true;
    }

    public enum PuttyType
    {
        Unknown = 0,
        Putty,
        PuttyNg,
        Kitty,
        Xming
    }
}
