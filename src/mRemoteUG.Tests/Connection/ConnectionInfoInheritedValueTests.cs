using System.Reflection;

using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection;

/// <summary>
/// Every inheritance flag makes the matching property read its folder's value.
/// </summary>
/// <remarks>
/// Each getter passes its own name to the inheritance lookup, and that name is what selects the
/// flag. A getter that passed the wrong name read its own value whatever the flag said, and no
/// test noticed for the 21 non-string RDP properties: only the string properties were covered.
/// The cases come from <see cref="ConnectionInfoInheritance.GetProperties"/>, so a property added
/// later is covered without touching this file.
/// </remarks>
[TestFixture]
public class ConnectionInfoInheritedValueTests
{
    [TestCaseSource(nameof(InheritableProperties))]
    public void AChildWithTheFlagOnReadsItsFoldersValue(PropertyInfo property)
    {
        var folder = new ContainerInfo();
        var child = new ConnectionInfo();
        folder.AddChild(child);
        child.Inheritance.TurnOffInheritanceCompletely();

        object? childValue = property.GetValue(child);
        object folderValue = DifferentPropertyValue.For(property, childValue);
        property.SetValue(folder, folderValue);
        Assert.That(property.GetValue(folder), Is.EqualTo(folderValue), "the folder did not keep the value");
        Assert.That(property.GetValue(child), Is.EqualTo(childValue), "inherited with the flag off");

        InheritanceFlagFor(property).SetValue(child.Inheritance, true);

        Assert.That(property.GetValue(child), Is.EqualTo(folderValue));
    }

    /// <summary>
    /// Each property of the record that has an inheritance flag, discovered rather than listed.
    /// </summary>
    private static IEnumerable<TestCaseData> InheritableProperties()
    {
        foreach (PropertyInfo flag in new ConnectionInfoInheritance(new object()).GetProperties())
        {
            PropertyInfo? property = typeof(ConnectionInfo).GetProperty(flag.Name);
            if (property is { CanRead: true, CanWrite: true })
            {
                yield return new TestCaseData(property).SetArgDisplayNames(property.Name);
            }
        }
    }

    private static PropertyInfo InheritanceFlagFor(PropertyInfo property)
    {
        return typeof(ConnectionInfoInheritance).GetProperty(property.Name)
               ?? throw new AssertionException($"no inheritance flag matches {property.Name}");
    }
}
