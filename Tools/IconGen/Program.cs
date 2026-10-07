using System.Globalization;
using System.Text;
using IconGen;
using SkiaSharp;

// Paths are resolved from the repository root so the tool can be run from anywhere.
var root = Repo.Root();
var manifestPath = Path.Combine(root, "src", "mRemoteUG", "Resources", "icon-manifest.json");

var check = args.Contains("--check", StringComparer.OrdinalIgnoreCase);
var sheet = args.Contains("--contact-sheet", StringComparer.OrdinalIgnoreCase);
// Renders and reports without touching the tree, so a manifest can be proved before the
// artwork it describes is committed.
var validate = args.Contains("--validate", StringComparer.OrdinalIgnoreCase);

var manifest = Manifest.Load(manifestPath);
var catalogue = new Catalogue();

// ---------------------------------------------------------------- validate, before writing a byte
var errors = new List<string>();
var fallbacks = new List<string>();

if (manifest.Ladder.Length == 0) errors.Add("ladder is empty.");
if (manifest.WindowIconFrames.Length == 0) errors.Add("windowIconFrames is empty.");

// Glyphs and window icons both become members of Resources, so they share one namespace and
// must not collide. Connection icons are reached through their own manifest-resource prefix and
// are free to reuse a name - "Cloud" the connection icon and a "Cloud" glyph would never meet.
var resourceMembers = new Dictionary<string, Shape>(StringComparer.Ordinal);
var connectionNames = new HashSet<string>(StringComparer.Ordinal);
var seen = new Dictionary<string, Shape>(StringComparer.Ordinal);
var legacyOwners = new Dictionary<string, string>(StringComparer.Ordinal);

foreach (var (entry, shape) in manifest.All())
{
    var where = $"{shape}/{entry.Name}";

    if (string.IsNullOrWhiteSpace(entry.Name)) { errors.Add($"{shape}: an entry has no name."); continue; }
    seen[entry.Name] = shape;
    if (shape is Shape.ConnectionIcon)
    {
        if (!connectionNames.Add(entry.Name))
            errors.Add($"{where}: another connection icon is already called that.");
    }
    else if (resourceMembers.TryGetValue(entry.Name, out var already))
        errors.Add($"{where}: {already}/{entry.Name} already declares that Resources member.");
    else resourceMembers[entry.Name] = shape;

    // A dot would make {Name}.{size}.png ambiguous to parse back.
    if (entry.Name.Contains('.')) errors.Add($"{where}: names may not contain '.'.");

    if (entry.Style is not ("Regular" or "Filled"))
        errors.Add($"{where}: style \"{entry.Style}\" is neither Regular nor Filled.");

    if (entry.Tint is Tint.Fixed && string.IsNullOrWhiteSpace(entry.Color))
        errors.Add($"{where}: tint is fixed, so it needs a color.");
    if (entry.Tint is Tint.Mask && !string.IsNullOrWhiteSpace(entry.Color))
        errors.Add($"{where}: tint is mask, so it must not carry a color.");
    if (entry.Color is { } c && !SKColor.TryParse(c, out _))
        errors.Add($"{where}: \"{c}\" is not a colour.");

    if (entry.Badge && shape is not Shape.Glyph)
        errors.Add($"{where}: only a glyph can be a badge.");

    if (shape is not Shape.ConnectionIcon && entry.Legacy.Length > 0)
        errors.Add($"{where}: only connection icons heal legacy names.");

    foreach (var legacy in entry.Legacy)
    {
        if (legacyOwners.TryGetValue(legacy, out var owner))
            errors.Add($"{where}: legacy name \"{legacy}\" is already healed by {owner}.");
        else legacyOwners[legacy] = entry.Name;
    }

    if (!catalogue.Has(entry.Style, entry.Fluent))
    {
        var near = string.Join(", ", catalogue.Nearest(entry.Style, entry.Fluent));
        errors.Add($"{where}: no {entry.Style} icon named \"{entry.Fluent}\". Did you mean: {near}?");
        continue;
    }

    var targets = shape is Shape.WindowIcon ? manifest.WindowIconFrames : manifest.Ladder;
    foreach (var target in targets)
    {
        var source = catalogue.SourceSizeFor(entry.Style, entry.Fluent, target);
        if (source != target)
            fallbacks.Add($"{where} @{target} <- {entry.Style}/{entry.Fluent} size {source}");

        var markup = catalogue.Content(entry.Style, entry.Fluent, source);
        if (entry.Tint is Tint.Mask && Renderer.WhyNotAMask(markup) is { } why)
            errors.Add($"{where} @{target}: {entry.Style}/{entry.Fluent} {why}. Make it fixed, or pick another icon.");
    }
}

// A legacy name that is also a current connection icon would shadow itself: the healing table
// would rewrite a name that already resolves. Only the connection-icon namespace can collide.
foreach (var (legacy, owner) in legacyOwners)
    if (connectionNames.Contains(legacy))
        errors.Add($"ConnectionIcon/{owner}: legacy name \"{legacy}\" is also a current connection icon.");

if (errors.Count > 0)
{
    Console.Error.WriteLine($"{errors.Count} problem(s) in {Path.GetRelativePath(root, manifestPath)}:");
    foreach (var error in errors.Order(StringComparer.Ordinal))
        Console.Error.WriteLine($"  {error}");
    return 1;
}

// ------------------------------------------------------------------------------------- generate
var staging = Directory.CreateTempSubdirectory("icongen-");
try
{
    var glyphDir = Directory.CreateDirectory(Path.Combine(staging.FullName, "Glyphs"));
    var windowDir = Directory.CreateDirectory(Path.Combine(staging.FullName, "WindowIcons"));
    var connDir = Directory.CreateDirectory(Path.Combine(staging.FullName, "ConnectionIcons"));
    var written = 0;

    foreach (var (entry, shape) in manifest.All())
    {
        // A mask ships colourless: white RGB with the shape carried entirely in alpha, so the
        // application can tint it to SystemColors.ControlText with a LockBits pass.
        var color = entry.Tint is Tint.Mask ? SKColors.White : SKColor.Parse(entry.Color!);

        if (shape is Shape.WindowIcon)
        {
            var frames = manifest.WindowIconFrames
                .Select(f => (f, Renderer.ToPng(
                    catalogue.Content(entry.Style, entry.Fluent,
                        catalogue.SourceSizeFor(entry.Style, entry.Fluent, f)),
                    catalogue.SourceSizeFor(entry.Style, entry.Fluent, f), f, color)))
                .ToList();

            File.WriteAllBytes(Path.Combine(windowDir.FullName, $"{entry.Name}.ico"), IcoWriter.Pack(frames));
            written++;
            continue;
        }

        var into = shape is Shape.Glyph ? glyphDir : connDir;
        foreach (var target in manifest.Ladder)
        {
            var source = catalogue.SourceSizeFor(entry.Style, entry.Fluent, target);
            var png = Renderer.ToPng(catalogue.Content(entry.Style, entry.Fluent, source), source, target,
                                     color, entry.Badge);
            File.WriteAllBytes(Path.Combine(into.FullName, $"{entry.Name}.{target}.png"), png);
            written++;
        }
    }

    File.WriteAllText(Path.Combine(staging.FullName, "Resources.g.cs"),
                      Emit.ResourcesClass(manifest), new UTF8Encoding(false));

    if (sheet)
    {
        var sheetPath = Path.Combine(staging.FullName, "contact-sheet.png");
        Emit.ContactSheet(manifest, catalogue, sheetPath);
        Console.WriteLine($"contact sheet: {sheetPath}");
    }

    Console.WriteLine($"{written} file(s) generated from {seen.Count} manifest entries.");
    if (fallbacks.Count > 0)
    {
        Console.WriteLine($"{fallbacks.Count} size(s) rendered from nearest-below geometry " +
                          "(vector, so still crisp - what is lost is the hand-tuning):");
        foreach (var fallback in fallbacks.Order(StringComparer.Ordinal))
            Console.WriteLine($"  {fallback}");
    }

    var targets = new (string Staged, string Final)[]
    {
        (glyphDir.FullName,  Path.Combine(root, "src", "mRemoteUG", "Resources", "Glyphs")),
        (windowDir.FullName, Path.Combine(root, "src", "mRemoteUG", "Resources", "Icons")),
        (connDir.FullName,   Path.Combine(root, "src", "mRemoteUG", "Icons")),
    };

    if (validate)
    {
        Console.WriteLine("validate only - the tree was not touched.");
        return 0;
    }

    if (check)
        return Repo.Diff(targets, staging.FullName, root) ? 0 : 2;

    foreach (var (from, to) in targets) Repo.Sync(from, to);
    File.Copy(Path.Combine(staging.FullName, "Resources.g.cs"),
              Path.Combine(root, "src", "mRemoteUG", "Resources", "Resources.g.cs"), overwrite: true);

    Console.WriteLine("written into the tree.");
    return 0;
}
finally
{
    if (!sheet && !validate) staging.Delete(recursive: true);
    else Console.WriteLine($"staged output kept at {staging.FullName}");
}
