using System.Globalization;

namespace mRemoteUG.App;

/// <summary>
/// The ID that keeps one <c>--selftest</c> run's registry key and report apart from another's.
/// </summary>
/// <remarks>
/// Two runs on one host share the account's profile: the CI runner runs every job as the same
/// account and lets builds of two branches overlap. With fixed names, one run could delete the
/// registry key the other was watching, or replace the report the other was about to be graded
/// on. Each run therefore names both after its ID: <c>--selftest-id &lt;id&gt;</c> when the caller
/// gives one, the process ID otherwise.
/// <para>
/// The ID becomes part of a registry path and of a file name, so it is held to ASCII letters,
/// digits, '-' and '_', starting with a letter or digit: no path separator, no '.', nothing a file
/// system or the registry treats specially.
/// </para>
/// </remarks>
public static class SelfTestRunId
{
    /// <summary>The switch that names the run: <c>--selftest-id &lt;id&gt;</c>.</summary>
    public const string Switch = "--selftest-id";

    /// <summary>The longest ID accepted; a CI run ID plus a job name fits with room to spare.</summary>
    public const int MaxLength = 64;

    /// <summary>The key every run's own scratch key sits under, relative to HKCU.</summary>
    public const string RegistryParent = @"Software\mRemoteUG\SelfTest";

    /// <summary>
    /// The ID the command line asks for, or the process ID when it asks for none.
    /// </summary>
    /// <returns>
    /// False, with <paramref name="error"/> saying why and <paramref name="id"/> set to the process
    /// ID, when the switch is given without a value or with one that is not a valid ID. The caller
    /// still has a usable ID and can report the problem in the run it names.
    /// </returns>
    public static bool TryParse(IReadOnlyList<string>? args, int processId, out string id, out string? error)
    {
        string fallback = processId.ToString(CultureInfo.InvariantCulture);
        id = fallback;
        error = null;

        if (args == null)
        {
            return true;
        }

        int index = IndexOfSwitch(args);
        if (index < 0)
        {
            return true;
        }

        if (index + 1 >= args.Count)
        {
            error = $"{Switch} was given without an ID.";
            return false;
        }

        string candidate = args[index + 1];
        if (!IsValid(candidate))
        {
            error = $"'{candidate}' is not a valid {Switch}: use 1 to {MaxLength} letters, digits, '-' or '_', " +
                    "starting with a letter or digit.";
            return false;
        }

        id = candidate;
        return true;
    }

    /// <summary>Whether <paramref name="id"/> is safe to put into a registry path and a file name.</summary>
    public static bool IsValid(string? id)
    {
        // A letter or digit first, so that the next switch - "--selftest-id --largefont" - is
        // never taken for the ID.
        return !string.IsNullOrEmpty(id) &&
               id.Length <= MaxLength &&
               IsLetterOrDigit(id[0]) &&
               id.All(c => IsLetterOrDigit(c) || c is '-' or '_');
    }

    private static bool IsLetterOrDigit(char c)
    {
        // ASCII only: char.IsLetterOrDigit would let through characters a file system or a log
        // reader may not round-trip.
        return c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');
    }

    /// <summary>This run's scratch key, relative to HKCU. Only this key is ever deleted.</summary>
    public static string RegistryPath(string id)
    {
        return RegistryParent + @"\" + id;
    }

    /// <summary>The name of this run's report, written next to the application log.</summary>
    public static string ReportFileName(string id)
    {
        return $"mRemoteUG-selftest-{id}.log";
    }

    private static int IndexOfSwitch(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], Switch, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
