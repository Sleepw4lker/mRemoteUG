using System.Globalization;

namespace mRemoteUG.Security.KeyDerivation;

/// <summary>
/// The range of PBKDF2 iteration counts, in one place: what a connection file may declare
/// in its KdfIterations attribute, and what the Options page offers.
/// </summary>
public static class KeyDerivationIterationLimits
{
    /// <summary>
    /// The count a file without a KdfIterations attribute is read with: the provider's own
    /// default, and the one every writer used before the setting existed.
    /// </summary>
    public const int Default = 1000;

    /// <summary>The smallest count <see cref="Pkcs5S2KeyGenerator"/> accepts.</summary>
    public const int Minimum = 1000;

    /// <summary>The largest count the Options page lets the user choose.</summary>
    public const int OptionsMaximum = 50000;

    /// <summary>
    /// The largest count a file may declare and still load. Higher than
    /// <see cref="OptionsMaximum"/> because the setting behind the page is a plain user.config
    /// value that nothing validates, so a hand-edited one could have written a larger count.
    /// A bound exists at all so that a hostile or corrupt value cannot stall the load in PBKDF2.
    /// </summary>
    public const int ReadMaximum = 1_000_000;

    /// <summary>
    /// The iteration count a connection file declares. A missing attribute
    /// (<paramref name="value"/> null) is <see cref="Default"/>.
    /// </summary>
    /// <exception cref="EncryptionException">
    /// The value is not a whole number, or lies outside <see cref="Minimum"/> to
    /// <see cref="ReadMaximum"/>.
    /// </exception>
    internal static int ParseFromFile(string? value)
    {
        if (value == null)
        {
            return Default;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iterations)
            || iterations < Minimum
            || iterations > ReadMaximum)
        {
            throw new EncryptionException(
                $"The connections file declares KdfIterations=\"{value}\", which is not a whole number " +
                $"from {Minimum.ToString(CultureInfo.InvariantCulture)} to " +
                $"{ReadMaximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        return iterations;
    }
}
