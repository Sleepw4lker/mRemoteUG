using System.ComponentModel;

namespace mRemoteUG.UI.Window;

// A thin TabPage subclass adding what this window's owner-drawn tab strip needs that the native
// TabPage doesn't have: a per-tab icon, and a full title kept apart from the text the strip shows.
public class ConnectionTabPage : TabPage
{
    /// <summary>What a cut title ends with.</summary>
    internal const string Ellipsis = "…";

    /// <summary>How a title is measured: as the strip draws it, and as comctl32 measures it.</summary>
    /// <remarks>
    /// Measured: comctl32 sizes a Tab from the GDI text extent of <see cref="Control.Text"/>,
    /// without prefix processing, plus <c>TabControl.Padding.X</c> on each side - nothing else.
    /// <c>TextRenderer</c> with these flags gives the same extent; without <c>NoPadding</c> it adds
    /// a margin the native Tab has no room for, and the drawn title would be cut a second time.
    /// </remarks>
    internal const TextFormatFlags MeasureFlags =
        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;


    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Icon? Icon { get; set; }

    // The connection icon's name, kept so Icon can be rebuilt at a new DPI: an Icon here is a
    // single frame, and drawing it into a larger or smaller rectangle stretches it.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? IconName { get; set; }

    /// <summary>
    /// The Tab's full title, as the user sees it: what Rename Tab shows and the tooltip holds.
    /// </summary>
    /// <remarks>
    /// Not <see cref="Control.Text"/>, which is what the native control measures: the title
    /// shortened to the strip's maximum width, with every ampersand doubled.
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title { get; private set; } = "";

    /// <summary>The title as the strip draws it: <see cref="Title"/>, or as much of it as fits and "…".</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string DisplayTitle { get; private set; } = "";

    /// <summary>Gives the Tab a new full title, and shows it.</summary>
    internal void SetTitle(string title, Font font, int maxWidth)
    {
        Title = title;
        ShowTitle(font, maxWidth);
    }

    /// <summary>
    /// Shortens the title against a maximum width in the strip's font, and has the native control
    /// measure the Tab again.
    /// </summary>
    /// <remarks>
    /// <see cref="Control.Text"/> is assigned even when it has not changed, and that is what makes
    /// comctl32 measure the Tab again: measured, <c>TCM_SETPADDING</c> on its own leaves every Tab
    /// at its old width until an item is set, and setting one re-measures them all.
    /// <para>
    /// The ampersand is doubled where the native control or a tooltip reads the text: both strip a
    /// single one as a mnemonic prefix. The strip draws <see cref="DisplayTitle"/> itself with
    /// <c>NoPrefix</c>, so it shows the title as it is.
    /// </para>
    /// </remarks>
    internal void ShowTitle(Font font, int maxWidth)
    {
        DisplayTitle = Shorten(Title, font, maxWidth);
        ToolTipText = DisplayTitle == Title ? "" : EscapePrefix(Title);
        Text = EscapePrefix(DisplayTitle);
    }

    /// <summary>
    /// The title if it fits in <paramref name="maxWidth"/>; else its longest start that fits with "…".
    /// </summary>
    internal static string Shorten(string title, Font font, int maxWidth)
    {
        if (TextWidth(title, font) <= maxWidth)
        {
            return title;
        }

        // The widest start that still fits with the ellipsis: width grows with length, so a
        // binary search over the number of characters kept finds it.
        int fits = 0;
        int tooLong = title.Length;
        while (tooLong - fits > 1)
        {
            int middle = (fits + tooLong) / 2;
            if (TextWidth(Cut(title, middle), font) <= maxWidth)
            {
                fits = middle;
            }
            else
            {
                tooLong = middle;
            }
        }

        return Cut(title, fits);
    }

    private static string Cut(string title, int length)
    {
        // Never between the halves of a surrogate pair.
        if (length > 0 && char.IsHighSurrogate(title[length - 1]))
        {
            length--;
        }

        return title[..length].TrimEnd() + Ellipsis;
    }

    private static int TextWidth(string text, Font font) =>
        TextRenderer.MeasureText(text, font, Size.Empty, MeasureFlags).Width;

    private static string EscapePrefix(string text) => text.Replace("&", "&&", StringComparison.Ordinal);
}
