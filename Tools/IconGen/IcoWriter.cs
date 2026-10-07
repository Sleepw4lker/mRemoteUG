namespace IconGen;

/// <summary>
/// Packs PNG frames into a multi-frame .ico.
/// </summary>
/// <remarks>
/// Window icons stay .ico because Windows itself picks the frame for the title bar, Alt-Tab and
/// the taskbar, and nothing tints them. The BCL has no ICO writer, so the container is written by
/// hand - which is 22 bytes of header per frame and the reason connection icons, which need
/// neither frame selection nor Windows' involvement, are plain PNGs instead.
/// <para>
/// Frames are stored PNG-compressed. That is a Vista-and-later feature and the baseline here is
/// Windows 11 (ADR-0010). Verified rather than assumed: --selftest resolves every window icon and
/// SelfTest reports the frame sizes it found.
/// </para>
/// </remarks>
public static class IcoWriter
{
    public static byte[] Pack(IReadOnlyList<(int Size, byte[] Png)> frames)
    {
        if (frames.Count is 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(frames), frames.Count, "1..255 frames.");

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);              // reserved
        writer.Write((ushort)1);              // 1 = icon
        writer.Write((ushort)frames.Count);

        // Directory entries come first, so every frame's offset is past all of them.
        var offset = 6 + (16 * frames.Count);
        foreach (var (size, png) in frames)
        {
            // 0 means 256 in this field, which is the only reason 256 fits in a byte.
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);            // palette entries
            writer.Write((byte)0);            // reserved
            writer.Write((ushort)1);          // colour planes
            writer.Write((ushort)32);         // bits per pixel
            writer.Write(png.Length);
            writer.Write(offset);
            offset += png.Length;
        }

        foreach (var (_, png) in frames)
            writer.Write(png);

        writer.Flush();
        return stream.ToArray();
    }
}
