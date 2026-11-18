using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace mRemoteUG.Tests.TestHelpers;

/// <summary>
/// Writes a file that identifies itself the way a Chocolatey ShimGen launcher does, so a test can
/// point PuTTY's path at one without Chocolatey on the machine.
/// </summary>
/// <remarks>
/// A shim is told apart only by its version resource (FileDescription "ShimGen generated shim -
/// Chocolatey Shim"), so the stub is a copy of an existing PE - this test assembly - with every
/// native resource replaced by a version resource carrying that description. It is never run.
/// </remarks>
internal static class LauncherShimStub
{
    /// <summary>The FileDescription of a real ShimGen shim, as read off one with FileVersionInfo.</summary>
    public const string ChocolateyShimDescription = "ShimGen generated shim - Chocolatey Shim";

    private const ushort EnglishUnitedStates = 0x0409;
    private const ushort UnicodeCodePage = 0x04B0;
    private static readonly IntPtr s_rtVersion = 16;
    private static readonly IntPtr s_versionResourceId = 1;

    /// <summary>Writes the stub at <paramref name="path"/> with the given FileDescription.</summary>
    public static void Write(string path, string fileDescription)
    {
        File.Copy(typeof(LauncherShimStub).Assembly.Location, path, true);
        byte[] resource = BuildVersionResource(fileDescription);

        IntPtr update = BeginUpdateResource(path, true);
        if (update == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), $"BeginUpdateResource({path})");
        }

        if (!UpdateResource(update, s_rtVersion, s_versionResourceId, EnglishUnitedStates, resource,
                            (uint)resource.Length))
        {
            int error = Marshal.GetLastPInvokeError();
            EndUpdateResource(update, true);
            throw new Win32Exception(error, $"UpdateResource({path})");
        }

        if (!EndUpdateResource(update, false))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), $"EndUpdateResource({path})");
        }
    }

    /// <summary>A VS_VERSIONINFO with one string table (en-US, Unicode) holding FileDescription.</summary>
    private static byte[] BuildVersionResource(string fileDescription)
    {
        byte[] fixedFileInfo = new byte[52];
        BitConverter.GetBytes(0xFEEF04BDu).CopyTo(fixedFileInfo, 0); // dwSignature
        BitConverter.GetBytes(0x00010000u).CopyTo(fixedFileInfo, 4); // dwStrucVersion
        BitConverter.GetBytes(0x00010000u).CopyTo(fixedFileInfo, 8); // dwFileVersionMS: 1.0
        BitConverter.GetBytes(0x00000004u).CopyTo(fixedFileInfo, 32); // dwFileOS: VOS__WINDOWS32
        BitConverter.GetBytes(0x00000001u).CopyTo(fixedFileInfo, 36); // dwFileType: VFT_APP

        // FileVersionInfo takes a string table only if it has a FileVersion; without one it moves
        // on to its fallback code pages and reads every string back empty.
        byte[] description = Text("FileDescription", fileDescription);
        byte[] fileVersion = Text("FileVersion", "1.0.0.0");
        byte[] stringTable = Block("040904B0", [], 0, true, description, fileVersion);
        byte[] stringFileInfo = Block("StringFileInfo", [], 0, true, stringTable);

        byte[] translation = new byte[4];
        BitConverter.GetBytes(EnglishUnitedStates).CopyTo(translation, 0);
        BitConverter.GetBytes(UnicodeCodePage).CopyTo(translation, 2);
        byte[] var = Block("Translation", translation, 4, false);
        byte[] varFileInfo = Block("VarFileInfo", [], 0, true, var);

        return Block("VS_VERSION_INFO", fixedFileInfo, 52, false, stringFileInfo, varFileInfo);
    }

    /// <summary>
    /// One version-resource block: wLength, wValueLength, wType, the key, padding, the value, then
    /// each child on a 32-bit boundary. wLength counts everything but trailing padding.
    /// </summary>
    private static byte[] Block(string key, byte[] value, ushort valueLength, bool isText, params byte[][] children)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); // wLength, patched below
        writer.Write(valueLength);
        writer.Write((ushort)(isText ? 1 : 0));
        writer.Write(Utf16Z(key));
        PadToDword(writer);
        writer.Write(value);
        foreach (byte[] child in children)
        {
            PadToDword(writer);
            writer.Write(child);
        }

        writer.Flush();
        byte[] block = stream.ToArray();
        BitConverter.GetBytes((ushort)block.Length).CopyTo(block, 0);
        return block;
    }

    /// <summary>A String block; its wValueLength counts characters, terminator included.</summary>
    private static byte[] Text(string key, string value) =>
        Block(key, Utf16Z(value), (ushort)(value.Length + 1), true);

    private static void PadToDword(BinaryWriter writer)
    {
        while (writer.BaseStream.Position % 4 != 0)
        {
            writer.Write((byte)0);
        }
    }

    private static byte[] Utf16Z(string text) => Encoding.Unicode.GetBytes(text + "\0");

    [DllImport("kernel32.dll", EntryPoint = "BeginUpdateResourceW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr BeginUpdateResource(
        string fileName, [MarshalAs(UnmanagedType.Bool)] bool deleteExistingResources);

    [DllImport("kernel32.dll", EntryPoint = "UpdateResourceW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language,
                                              byte[] data, uint size);

    [DllImport("kernel32.dll", EntryPoint = "EndUpdateResourceW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndUpdateResource(IntPtr update, [MarshalAs(UnmanagedType.Bool)] bool discard);
}
