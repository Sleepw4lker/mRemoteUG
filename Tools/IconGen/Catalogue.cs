using System.Reflection;

namespace IconGen;

/// <summary>
/// Every Fluent icon the referenced packages carry, indexed by style, name and size.
/// </summary>
/// <remarks>
/// The packages generate one nested type per icon per size - e.g.
/// <c>...Icons.Regular+Size24+Desktop</c> - whose <c>Content</c> property returns the SVG inner
/// markup. There is no public catalogue API, so reflection over the assembly is the index.
/// <para>
/// Coverage is uneven and that is the fact this whole tool is shaped around: of 3082 Regular
/// names only 374 carry all of 16/20/24/28/32/48. Every name used here carries at least 16, 20
/// and 24 - 100%, 125% and 150% - and larger ladder sizes fall back to the nearest geometry
/// below them. Because the source is vector and is rendered at the exact target size, a fallback
/// is still crisp; what it loses is the hand-tuning for that size, not sharpness.
/// </para>
/// </remarks>
public sealed class Catalogue
{
    private readonly Dictionary<(string Style, string Name), SortedDictionary<int, Type>> _icons = new();

    public Catalogue()
    {
        foreach (var style in new[] { "Regular", "Filled" })
        {
            var assembly = Assembly.Load($"Microsoft.FluentUI.AspNetCore.Components.Icons.{style}");
            foreach (var type in assembly.GetTypes())
            {
                var container = type.DeclaringType;
                if (container is null || !container.Name.StartsWith("Size", StringComparison.Ordinal))
                    continue;
                if (!int.TryParse(container.Name.AsSpan(4), out var size))
                    continue;

                var key = (style, type.Name);
                if (!_icons.TryGetValue(key, out var bySize))
                    _icons[key] = bySize = new SortedDictionary<int, Type>();
                bySize[size] = type;
            }
        }
    }

    public bool Has(string style, string name) => _icons.ContainsKey((style, name));

    public IReadOnlyCollection<int> SizesOf(string style, string name) =>
        _icons.TryGetValue((style, name), out var bySize) ? bySize.Keys : [];

    /// <summary>
    /// The source size to render a target from: the largest at or below it, else the smallest
    /// above it. Never guessed silently - <see cref="Program"/> prints every fallback it used.
    /// </summary>
    public int SourceSizeFor(string style, string name, int target)
    {
        var sizes = SizesOf(style, name);
        if (sizes.Count == 0)
            throw new InvalidOperationException($"{style}/{name} is not in the catalogue.");

        var atOrBelow = sizes.Where(s => s <= target).ToList();
        return atOrBelow.Count > 0 ? atOrBelow.Max() : sizes.Min();
    }

    /// <summary>The SVG inner markup for one icon at one of its own sizes.</summary>
    public string Content(string style, string name, int size)
    {
        var type = _icons[(style, name)][size];
        var instance = Activator.CreateInstance(type)
                       ?? throw new InvalidOperationException($"Could not construct {type.FullName}.");
        return type.GetProperty("Content")?.GetValue(instance) as string
               ?? throw new InvalidOperationException($"{type.FullName} has no Content.");
    }

    /// <summary>
    /// The names closest to one that did not resolve, so a typo reads like a spellcheck
    /// suggestion rather than a bare failure.
    /// </summary>
    public IEnumerable<string> Nearest(string style, string name, int count = 5) =>
        _icons.Keys.Where(k => k.Style == style)
                   .Select(k => k.Name)
                   .OrderBy(n => Distance(n, name))
                   .ThenBy(n => n, StringComparer.Ordinal)
                   .Take(count);

    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitute = previous[j - 1] +
                    (char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1);
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitute);
            }
            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
