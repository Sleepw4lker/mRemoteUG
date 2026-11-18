using System.Reflection;

using NUnit.Framework;

namespace mRemoteUG.Tests.TestHelpers;

/// <summary>
/// Picks a valid value for a connection record property that differs from the one it holds, so a
/// data-driven test can tell an assignment, or an inherited value, from the value already there.
/// </summary>
public static class DifferentPropertyValue
{
    /// <summary>A value of <paramref name="property"/>'s type that differs from <paramref name="current"/>.</summary>
    public static object For(PropertyInfo property, object? current)
    {
        Type type = property.PropertyType;
        if (type == typeof(bool))
        {
            return current is not true;
        }

        if (type == typeof(string))
        {
            return current is "a different value" ? "another value" : "a different value";
        }

        if (type == typeof(int))
        {
            // 1 and 2 are inside every clamped range on the record (the idle timeout is 0-240).
            return current is 1 ? 2 : 1;
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).Cast<object>().FirstOrDefault(v => !v.Equals(current))
                   ?? throw new AssertionException($"{type.Name} has no second value");
        }

        throw new AssertionException($"no different value known for {property.Name} of type {type.Name}");
    }
}
