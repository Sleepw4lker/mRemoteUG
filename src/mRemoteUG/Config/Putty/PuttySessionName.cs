using System.Text;

namespace mRemoteUG.Config.Putty;

/// <summary>
/// Turns the registry key name PuTTY stores a session under back into the session's name.
/// </summary>
internal static class PuttySessionName
{
    /// <summary>
    /// Decodes the <c>%XX</c> escapes PuTTY writes and nothing else. In particular <c>+</c> stays a
    /// plus sign: PuTTY leaves it unescaped, and "plus means space" belongs to HTML forms.
    /// </summary>
    /// <remarks>
    /// Escaped bytes are read in the system ANSI code page: PuTTY stores the session name with the
    /// ANSI registry functions and reads its command line in that code page too, so only that
    /// decoding makes a name round-trip through <c>-load</c>. A byte sequence that is not valid in the
    /// code page decodes to the code page's fallback character (a lone Shift-JIS lead byte gives
    /// U+30FB, as in Windows) and never throws. A <c>%</c> not followed by two hex
    /// digits is kept as it is.
    /// </remarks>
    public static string Decode(string storedName)
    {
        return Decode(storedName, AnsiEncoding);
    }

    /// <summary>The system ANSI code page (never <see cref="Encoding.Default"/>, which is UTF-8 on .NET).</summary>
    internal static Encoding AnsiEncoding { get; } = CreateAnsiEncoding();

    private static Encoding CreateAnsiEncoding()
    {
        // The legacy code pages (1250, 1251, 932, ...) are not built in on .NET; the provider ships
        // in the shared framework. Registering is idempotent.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(0);
    }

    internal static string Decode(string storedName, Encoding encoding)
    {
        if (string.IsNullOrEmpty(storedName) || !storedName.Contains('%'))
        {
            return storedName;
        }

        var result = new StringBuilder(storedName.Length);
        var pendingBytes = new List<byte>();
        for (int i = 0; i < storedName.Length; i++)
        {
            if (IsEscapeAt(storedName, i))
            {
                pendingBytes.Add(Convert.ToByte(storedName.Substring(i + 1, 2), 16));
                i += 2;
                continue;
            }

            Flush(result, pendingBytes, encoding);
            result.Append(storedName[i]);
        }

        Flush(result, pendingBytes, encoding);
        return result.ToString();
    }

    private static bool IsEscapeAt(string text, int index)
    {
        return text[index] == '%'
               && index + 2 < text.Length
               && Uri.IsHexDigit(text[index + 1])
               && Uri.IsHexDigit(text[index + 2]);
    }

    private static void Flush(StringBuilder result, List<byte> pendingBytes, Encoding encoding)
    {
        if (pendingBytes.Count == 0)
        {
            return;
        }

        result.Append(encoding.GetString(pendingBytes.ToArray()));
        pendingBytes.Clear();
    }
}
