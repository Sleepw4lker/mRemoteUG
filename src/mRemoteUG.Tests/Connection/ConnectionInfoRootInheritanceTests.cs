using System.Reflection;

using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tests.TestHelpers;
using mRemoteUG.Tree.Root;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection;

/// <summary>
/// The connections root is nothing to inherit from, even with a flag switched on.
/// </summary>
/// <remarks>
/// RootNodeInfo declares <c>new bool Password</c> over the inherited string one, so looking the
/// property up by name on the root throws AmbiguousMatchException. That was logged as "Error
/// retrieving inherited property" on every read of Password. Adding a node to the root switches
/// its flags off, so the flag is set after the node is in the root, which is the state a
/// caller that assigns Inheritance after the fact ends up in.
/// </remarks>
[TestFixture]
public class ConnectionInfoRootInheritanceTests
{
    private const string ErrorText = "Error retrieving inherited property";

    [TestCaseSource(nameof(InheritableProperties))]
    public void AConnectionDirectlyUnderTheRootReadsItsOwnValueAndLogsNothing(PropertyInfo property)
    {
        AssertReadsOwnValueQuietly(new ConnectionInfo(), property);
    }

    [TestCaseSource(nameof(InheritableProperties))]
    public void AFolderDirectlyUnderTheRootReadsItsOwnValueAndLogsNothing(PropertyInfo property)
    {
        AssertReadsOwnValueQuietly(new ContainerInfo(), property);
    }

    [TestCaseSource(nameof(InheritableProperties))]
    public void ANodeInANormalFolderStillInheritsFromTheFolder(PropertyInfo property)
    {
        var root = new RootNodeInfo(RootNodeType.Connection);
        var folder = new ContainerInfo();
        root.AddChild(folder);
        var child = new ConnectionInfo();
        folder.AddChild(child);
        child.Inheritance.TurnOffInheritanceCompletely();

        object? childValue = property.GetValue(child);
        object folderValue = DifferentPropertyValue.For(property, childValue);
        property.SetValue(folder, folderValue);
        FlagFor(property).SetValue(child.Inheritance, true);

        Assert.That(property.GetValue(child), Is.EqualTo(folderValue));
    }

    private static void AssertReadsOwnValueQuietly(ConnectionInfo node, PropertyInfo property)
    {
        var root = new RootNodeInfo(RootNodeType.Connection);
        root.AddChild(node);
        node.Inheritance.TurnOffInheritanceCompletely();

        object? ownValue = property.GetValue(node);
        object rootValue = DifferentPropertyValue.For(property, ownValue);
        TrySet(root, property, rootValue);

        FlagFor(property).SetValue(node.Inheritance, true);
        int before = ErrorCount();

        object? read = property.GetValue(node);

        Assert.That(read, Is.EqualTo(ownValue), "the root's value was inherited");
        Assert.That(ErrorCount(), Is.EqualTo(before), "an inheritance error was logged");
    }

    private static void TrySet(object target, PropertyInfo property, object value)
    {
        try
        {
            property.SetValue(target, value);
        }
        catch (Exception ex) when (ex is TargetInvocationException or ArgumentException)
        {
            // The root may not take a value for this property; the check that follows still holds.
        }
    }

    private static int ErrorCount() =>
        Runtime.MessageCollector.Messages.Count(m => m.Text != null && m.Text.Contains(ErrorText));

    private static PropertyInfo FlagFor(PropertyInfo property)
    {
        return typeof(ConnectionInfoInheritance).GetProperty(property.Name)
               ?? throw new AssertionException($"no inheritance flag matches {property.Name}");
    }

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
}
