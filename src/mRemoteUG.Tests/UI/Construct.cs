using NUnit.Framework;

namespace mRemoteUG.Tests.UI;

/// <summary>
/// Builds a control from its type, for the fixtures that walk every form, window or tool strip
/// in the application.
/// </summary>
internal static class Construct
{
    /// <summary>
    /// A new instance of <paramref name="type"/>, as <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Activator.CreateInstance(Type)"/> is declared to return null, which it only does
    /// for a nullable value type. A control type never is one, so a null here fails the test
    /// rather than standing in for a control.
    /// </remarks>
    public static T Instance<T>(Type type)
    {
        return (T)(Activator.CreateInstance(type)
                   ?? throw new AssertionException($"{type.FullName} could not be constructed."));
    }
}
