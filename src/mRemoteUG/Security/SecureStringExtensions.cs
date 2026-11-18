using System.Runtime.InteropServices;
using System.Security;

namespace mRemoteUG.Security;

public static class SecureStringExtensions
{
    /// <summary>
    /// Method to marshall a SecureString out of protected memory into a standard String object that is required by most
    /// other functions.
    /// Code initially taken from Fabio Pintos
    /// Source: https://blogs.msdn.microsoft.com/fpintos/2009/06/12/how-to-properly-convert-securestring-to-string/
    /// </summary>
    /// <param name="securePassword"></param>
    /// <returns></returns>
    public static string ConvertToUnsecureString(this SecureString securePassword)
    {
        ArgumentNullException.ThrowIfNull(securePassword);

        nint unmanagedString = IntPtr.Zero;
        try
        {
            unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
            // Not null: SecureStringToGlobalAllocUnicode throws rather than returning a
            // null pointer, and PtrToStringUni only returns null for one.
            return Marshal.PtrToStringUni(unmanagedString)!;
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
        }
    }

    public static SecureString ConvertToSecureString(this string unsecuredPassword)
    {
        ArgumentNullException.ThrowIfNull(unsecuredPassword);

        var secureString = new SecureString();
        foreach (char character in unsecuredPassword.ToCharArray())
        {
            secureString.AppendChar(character);
        }

        return secureString;
    }
}
