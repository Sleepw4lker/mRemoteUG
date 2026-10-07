using System.Text.Json;
using System.Text.Json.Serialization;

namespace IconGen;

/// <summary>How a glyph's colour is decided.</summary>
public enum Tint
{
    /// <summary>Ships colourless - white RGB, shape in alpha - and is tinted at load.</summary>
    Mask,

    /// <summary>Carries meaning in its colour and is never tinted.</summary>
    Fixed
}

/// <summary>What kind of artwork an entry produces.</summary>
public enum Shape
{
    /// <summary>A menu, toolbar or list mark. PNG at every ladder size.</summary>
    Glyph,

    /// <summary>A form's title-bar icon. Multi-frame .ico; Windows picks the frame.</summary>
    WindowIcon,

    /// <summary>A mark the user picks for a saved connection. PNG at every ladder size.</summary>
    ConnectionIcon
}

public sealed class IconEntry
{
    /// <summary>The name this application knows the artwork by - the Resources member name.</summary>
    public string Name { get; init; } = "";

    /// <summary>The Fluent type name, e.g. "PlugConnected".</summary>
    public string Fluent { get; init; } = "";

    /// <summary>"Regular" or "Filled".</summary>
    public string Style { get; init; } = "Regular";

    public Tint Tint { get; init; } = Tint.Mask;

    /// <summary>Required when <see cref="Tint"/> is Fixed, forbidden otherwise. "#RRGGBB".</summary>
    public string? Color { get; init; }

    /// <summary>Names from older builds that heal to this one. Connection icons only.</summary>
    public string[] Legacy { get; init; } = [];

    /// <summary>Why this mapping, when it is not obvious. Kept next to the choice.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Renders inset into the bottom-right quadrant instead of filling the frame.
    /// </summary>
    /// <remarks>
    /// A badge is composited onto another icon. Rendered full-bleed it covers the icon it is
    /// meant to annotate - measured on the contact sheet, the connected badge was a 32px green
    /// disc over a 32px node icon. Insetting it here rather than at the draw site keeps
    /// StatusImageList.Overlay a straight 1:1 blit and makes the badge correct at every ladder
    /// size by construction.
    /// </remarks>
    public bool Badge { get; init; }
}

public sealed class Manifest
{
    /// <summary>The device-pixel sizes every glyph is rendered at. A request snaps up.</summary>
    public int[] Ladder { get; init; } = [];

    /// <summary>The frame sizes a window icon carries.</summary>
    public int[] WindowIconFrames { get; init; } = [];

    public IconEntry[] Glyphs { get; init; } = [];
    public IconEntry[] WindowIcons { get; init; } = [];
    public IconEntry[] ConnectionIcons { get; init; } = [];

    public IEnumerable<(IconEntry Entry, Shape Shape)> All() =>
        Glyphs.Select(g => (g, Shape.Glyph))
              .Concat(WindowIcons.Select(w => (w, Shape.WindowIcon)))
              .Concat(ConnectionIcons.Select(c => (c, Shape.ConnectionIcon)));

    public static Manifest Load(string path)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), options)
               ?? throw new InvalidDataException($"{path} deserialized to null.");
    }
}
