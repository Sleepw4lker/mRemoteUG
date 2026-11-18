using mRemoteUG.Config.Connections;
using mRemoteUG.Connection;
using mRemoteUG.Security;
using mRemoteUG.Tests.TestHelpers;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Connections;

public class XmlConnectionsSaverTests
{
    // Rebuilt for every test in Setup: each test gets a file in a directory of its own.
    private string _connectionsFile = null!;
    private string _connectionsDirectory = null!;

    [SetUp]
    public void Setup()
    {
        _connectionsFile = FileTestHelpers.NewTempFilePath(".xml");
        // NewTempFilePath returns a file inside a directory it has just created.
        _connectionsDirectory = Path.GetDirectoryName(_connectionsFile)!;
    }

    [TearDown]
    public void Teardown()
    {
        FileTestHelpers.DeleteDirectory(_connectionsDirectory);
    }

    [Test]
    public void ASaveThatCannotEncryptThrowsAndLeavesTheFileAsItWas()
    {
        byte[] original = "<the last good connections file/>"u8.ToArray();
        File.WriteAllBytes(_connectionsFile, original);
        var saver = new XmlConnectionsSaver(_connectionsFile, new SaveFilter());

        // The encryption refuses a whitespace-only password, so the document cannot be built.
        Assert.That(() => saver.Save(ModelWithRootPassword("   ")), Throws.ArgumentException);

        Assert.That(File.ReadAllBytes(_connectionsFile), Is.EqualTo(original));
    }

    /// <summary>A connection tree whose root is protected with <paramref name="password"/>.</summary>
    internal static ConnectionTreeModel ModelWithRootPassword(string password)
    {
        var root = new RootNodeInfo(RootNodeType.Connection) { PasswordString = password };
        root.AddChild(new ConnectionInfo { Name = "server" });
        var model = new ConnectionTreeModel();
        model.AddRootNode(root);
        return model;
    }
}
