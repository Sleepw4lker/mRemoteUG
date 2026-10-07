namespace IconGen;

/// <summary>Finding the tree, and moving generated files into it without destroying what isn't.</summary>
public static class Repo
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "mRemoteUG.slnx")))
            dir = dir.Parent;

        return dir?.FullName
               ?? throw new InvalidOperationException("mRemoteUG.slnx was not found above this assembly.");
    }

    /// <summary>
    /// Which files in a destination this tool is allowed to remove when it no longer produces them.
    /// </summary>
    /// <remarks>
    /// Pruning is by extension, and only where every file of that extension is generated.
    /// <para>
    /// <c>Resources\Icons</c> used to be excluded because it also held the upstream brand
    /// mark, which nothing generates and a blanket "delete what I did not write" would have
    /// taken. That mark has been removed, so every .ico there is output now and a stale one
    /// is worth deleting rather than shipping.
    /// </para>
    /// </remarks>
    private static bool Prunable(string destination, string file)
    {
        var name = Path.GetFileName(file);
        if (destination.EndsWith("Glyphs", StringComparison.OrdinalIgnoreCase))
            return name.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        // Connection icons: the generated set is PNG. The .ico files it replaced were deleted
        // by hand in the commit that made the switch, not silently by a tool run.
        if (destination.EndsWith(Path.Combine("mRemoteUG", "Icons"), StringComparison.OrdinalIgnoreCase))
            return name.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        // Window icons: every .ico here is generated now that the brand mark is gone.
        if (destination.EndsWith(Path.Combine("Resources", "Icons"), StringComparison.OrdinalIgnoreCase))
            return name.EndsWith(".ico", StringComparison.OrdinalIgnoreCase);

        return false;
    }

    public static void Sync(string from, string to)
    {
        Directory.CreateDirectory(to);

        var staged = Directory.GetFiles(from)
                              .ToDictionary(f => Path.GetFileName(f)!, StringComparer.Ordinal);

        foreach (var existing in Directory.GetFiles(to))
            if (!staged.ContainsKey(Path.GetFileName(existing)) && Prunable(to, existing))
                File.Delete(existing);

        foreach (var (name, path) in staged)
            File.Copy(path, Path.Combine(to, name), overwrite: true);
    }

    /// <summary>True when the tree already matches what was just generated.</summary>
    public static bool Diff((string Staged, string Final)[] targets, string staging, string root)
    {
        var differences = new List<string>();

        foreach (var (stagedDir, finalDir) in targets)
        {
            var staged = Directory.Exists(stagedDir) ? Directory.GetFiles(stagedDir) : [];
            var final = Directory.Exists(finalDir) ? Directory.GetFiles(finalDir) : [];

            var stagedNames = staged.Select(f => Path.GetFileName(f)!).ToHashSet(StringComparer.Ordinal);
            var finalNames = final.Select(f => Path.GetFileName(f)!).ToHashSet(StringComparer.Ordinal);

            // Prunable narrows only the "went away" question. Asking it about presence as well
            // reported every window icon as missing, because that directory is never pruned - it
            // holds mRemote_Icon.ico, which nothing generates.
            var prunableNames = final.Where(f => Prunable(finalDir, f))
                                     .Select(f => Path.GetFileName(f)!).ToHashSet(StringComparer.Ordinal);

            foreach (var name in stagedNames.Except(finalNames).Order(StringComparer.Ordinal))
                differences.Add($"missing from the tree: {Rel(root, Path.Combine(finalDir, name))}");
            foreach (var name in prunableNames.Except(stagedNames).Order(StringComparer.Ordinal))
                differences.Add($"not produced any more: {Rel(root, Path.Combine(finalDir, name))}");

            foreach (var name in stagedNames.Intersect(finalNames).Order(StringComparer.Ordinal))
                if (!File.ReadAllBytes(Path.Combine(stagedDir, name))
                         .SequenceEqual(File.ReadAllBytes(Path.Combine(finalDir, name))))
                    differences.Add($"differs: {Rel(root, Path.Combine(finalDir, name))}");
        }

        var generated = Path.Combine(root, "src", "mRemoteUG", "Resources", "Resources.g.cs");
        var stagedGenerated = Path.Combine(staging, "Resources.g.cs");
        if (!File.Exists(generated) ||
            File.ReadAllText(generated) != File.ReadAllText(stagedGenerated))
            differences.Add($"differs: {Rel(root, generated)}");

        if (differences.Count == 0)
        {
            Console.WriteLine("the tree matches a fresh render.");
            return true;
        }

        Console.Error.WriteLine($"{differences.Count} difference(s) between the tree and a fresh render:");
        foreach (var difference in differences)
            Console.Error.WriteLine($"  {difference}");
        Console.Error.WriteLine("run `dotnet run --project Tools/IconGen` to bring it back into step.");
        return false;
    }

    private static string Rel(string root, string path) => Path.GetRelativePath(root, path);
}
