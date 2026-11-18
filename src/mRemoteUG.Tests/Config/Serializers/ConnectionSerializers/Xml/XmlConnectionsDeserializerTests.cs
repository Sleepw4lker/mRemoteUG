using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Xml;

using mRemoteUG.App;
using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Security.SymmetricEncryption;
using mRemoteUG.Tests.TestHelpers;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

using NUnit.Framework;
// Aliased rather than imported: the product assembly has a Resources class in namespace
// mRemoteUG, and a member of an enclosing namespace is found before a file-level using, so
// inside mRemoteUG.Tests the bare name Resources binds to that one instead of this one.
using Fixtures = mRemoteUG.Tests.Properties.Resources;

namespace mRemoteUG.Tests.Config.Serializers.ConnectionSerializers.Xml;

public class XmlConnectionsDeserializerTests
{
    private const string EmptyFileHeading = "Connection file is empty";

    private static readonly string[] s_keptArguments = { "kept" };
    private static readonly string[] s_conArguments = { "con" };
    private static readonly string[] s_commentedFileNodes = { "first", "folder", "inside" };

    // Rebuilt by every test that uses them, through Setup.
    private XmlConnectionsDeserializer _xmlConnectionsDeserializer = null!;
    private ConnectionTreeModel _connectionTreeModel = null!;

    public void Setup(string confCons, string password)
    {
        _xmlConnectionsDeserializer = new XmlConnectionsDeserializer(() => password.ConvertToSecureString());
        _connectionTreeModel = _xmlConnectionsDeserializer.DeserializeLoaded(confCons);
    }

    [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData),
                    nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
    public void DeserializingCreatesRootNode(Datagram testData)
    {
        Setup(testData.ConfCons, testData.Password);
        Assert.That(_connectionTreeModel.RootNodes, Is.Not.Empty);
    }

    [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData),
                    nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
    public void RootNodeHasThreeChildren(Datagram testData)
    {
        Setup(testData.ConfCons, testData.Password);
        ContainerInfo connectionRoot = _connectionTreeModel.RootNodes[0];
        Assert.That(connectionRoot.Children.Count, Is.EqualTo(3));
    }

    [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData),
                    nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
    public void RootContainsFolder1(Datagram testData)
    {
        Setup(testData.ConfCons, testData.Password);
        ContainerInfo connectionRoot = _connectionTreeModel.RootNodes[0];
        Assert.That(ContainsNodeNamed("Folder1", connectionRoot.Children), Is.True);
    }

    [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData),
                    nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
    public void Folder1ContainsThreeConnections(Datagram testData)
    {
        // Folder1's three connections include one using SSH2, which this build
        // supports again, so all three load.
        Setup(testData.ConfCons, testData.Password);
        ContainerInfo connectionRoot = _connectionTreeModel.RootNodes[0];
        ContainerInfo folder1 = GetFolderNamed("Folder1", connectionRoot.Children);
        int? folder1ConnectionCount = folder1?.Children.Count(node => !(node is ContainerInfo));
        Assert.That(folder1ConnectionCount, Is.EqualTo(3));
    }

    [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData),
                    nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
    public void Folder2ContainsThreeNodes(Datagram testData)
    {
        Setup(testData.ConfCons, testData.Password);
        ContainerInfo connectionRoot = _connectionTreeModel.RootNodes[0];
        ContainerInfo folder2 = GetFolderNamed("Folder2", connectionRoot.Children);
        int? folder1Count = folder2?.Children.Count;
        Assert.That(folder1Count, Is.EqualTo(3));
    }

    [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData),
                    nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
    public void Folder21HasTwoNodes(Datagram testData)
    {
        Setup(testData.ConfCons, testData.Password);
        ContainerInfo connectionRoot = _connectionTreeModel.RootNodes[0];
        ContainerInfo folder2 = GetFolderNamed("Folder2", connectionRoot.Children);
        ContainerInfo folder21 = GetFolderNamed("Folder2.1", folder2.Children);
        Assert.That(folder21.Children.Count, Is.EqualTo(2));
    }

    [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData),
                    nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
    public void Folder211HasOneConnection(Datagram testData)
    {
        Setup(testData.ConfCons, testData.Password);
        ContainerInfo connectionRoot = _connectionTreeModel.RootNodes[0];
        ContainerInfo folder2 = GetFolderNamed("Folder2", connectionRoot.Children);
        ContainerInfo folder21 = GetFolderNamed("Folder2.1", folder2.Children);
        ContainerInfo folder211 = GetFolderNamed("Folder2.1.1", folder21.Children);
        int connectionCount = folder211.Children.Count(node => !(node is ContainerInfo));
        Assert.That(connectionCount, Is.EqualTo(1));
    }

    [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData),
                    nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
    public void Folder22InheritsUsername(Datagram testData)
    {
        Setup(testData.ConfCons, testData.Password);
        ContainerInfo connectionRoot = _connectionTreeModel.RootNodes[0];
        ContainerInfo folder2 = GetFolderNamed("Folder2", connectionRoot.Children);
        ContainerInfo folder22 = GetFolderNamed("Folder2.2", folder2.Children);
        Assert.That(folder22.Inheritance.Username, Is.True);
    }

    /// <summary>Every inheritance flag, once set and once cleared in the file.</summary>
    /// <remarks>
    /// Both values, because a flag the deserializer never reads keeps whatever
    /// DefaultConnectionInheritance gave the new record, and that singleton is process-wide and
    /// changed by other fixtures. Only one of the two cases can match it.
    /// <para>
    /// The connection sits in a folder: the root node switches inheritance off for its direct
    /// children, so a flag read onto one of those would not be visible afterwards.
    /// </para>
    /// </remarks>
    private static IEnumerable<TestCaseData> InheritanceFlagValues()
    {
        foreach (PropertyInfo flag in new ConnectionInfoInheritance(null, true).GetProperties())
        {
            yield return new TestCaseData(flag.Name, true);
            yield return new TestCaseData(flag.Name, false);
        }
    }

    [TestCaseSource(nameof(InheritanceFlagValues))]
    public void AnInheritanceFlagIsReadFromTheFile(string inheritanceFlag, bool value)
    {
        var folder = new ContainerInfo { Name = "folder" };
        folder.AddChild(new ConnectionInfo { Name = "con" });
        XmlDocument file = SerializedFile("Connections", folder);
        ConnectionNode(file, "con").SetAttribute("Inherit" + inheritanceFlag, value.ToString().ToLowerInvariant());

        ConnectionTreeModel model = new XmlConnectionsDeserializer().DeserializeLoaded(file.OuterXml);
        ConnectionInfo loaded = LoadedConnection(model, "con");

        Assert.That(typeof(ConnectionInfoInheritance).GetProperty(inheritanceFlag)!.GetValue(loaded.Inheritance),
                    Is.EqualTo(value));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ConnectedIsReadAsPleaseConnect(bool connected)
    {
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "con" });
        ConnectionNode(file, "con").SetAttribute("Connected", connected.ToString().ToLowerInvariant());

        ConnectionTreeModel model = new XmlConnectionsDeserializer().DeserializeLoaded(file.OuterXml);
        ConnectionInfo loaded = LoadedConnection(model, "con");

        Assert.That(loaded.PleaseConnect, Is.EqualTo(connected));
    }

    [Test]
    public void TheRootNodeNameIsRead()
    {
        XmlDocument file = SerializedFile("My saved hosts", new ConnectionInfo { Name = "con" });

        ConnectionTreeModel model = new XmlConnectionsDeserializer().DeserializeLoaded(file.OuterXml);

        Assert.That(model.RootNodes[0].Name, Is.EqualTo("My saved hosts"));
    }

    [Test]
    public void ACommentBetweenConnectionsIsNotReadAsAConnection()
    {
        var folder = new ContainerInfo { Name = "folder" };
        folder.AddChild(new ConnectionInfo { Name = "inside" });
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "first" }, folder);
        XmlElement root = file.DocumentElement!;
        root.InsertBefore(file.CreateComment(" edited by hand "), root.FirstChild);
        XmlElement folderNode = ConnectionNode(file, "folder");
        folderNode.InsertBefore(file.CreateComment(" inside a folder "), folderNode.FirstChild);

        ConnectionTreeModel model = new XmlConnectionsDeserializer().DeserializeLoaded(file.OuterXml);

        Assert.That(model.GetRecursiveChildList().Select(node => node.Name),
                    Is.EquivalentTo(s_commentedFileNodes));
    }

    [Test]
    public void AConnectionWithAnUnsupportedProtocolIsSkippedAndNamedInOneWarning()
    {
        XmlDocument file = SerializedFile("Connections",
                                          new ConnectionInfo { Name = "kept" },
                                          new ConnectionInfo { Name = "dropped" });
        ConnectionNode(file, "dropped").SetAttribute("Protocol", "VNC");

        using var recorder = new MessageRecorder();
        ConnectionTreeModel model = new XmlConnectionsDeserializer().DeserializeLoaded(file.OuterXml);

        Assert.Multiple(() =>
        {
            Assert.That(model.RootNodes[0].Children.Select(node => node.Name), Is.EqualTo(s_keptArguments));
            IReadOnlyList<IMessage> warnings = recorder.Matching(MessageClass.WarningMsg, "dropped (VNC)");
            Assert.That(warnings.Select(warning => warning.Text).ToList(),
                        Has.Count.EqualTo(1).And.All.StartsWith("1 connection(s)"));
        });
    }

    [Test]
    public void LoadingTwiceWithOneDeserializerReportsEachSkippedConnectionOncePerLoad()
    {
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "dropped" });
        ConnectionNode(file, "dropped").SetAttribute("Protocol", "VNC");
        var deserializer = new XmlConnectionsDeserializer();

        using var recorder = new MessageRecorder();
        deserializer.Deserialize(file.OuterXml);
        deserializer.Deserialize(file.OuterXml);

        IReadOnlyList<IMessage> warnings = recorder.Matching(MessageClass.WarningMsg, "dropped (VNC)");
        Assert.That(warnings.Select(warning => warning.Text).ToList(),
                    Has.Count.EqualTo(2).And.All.StartsWith("1 connection(s)"));
    }

    [Test]
    public void AFileWithOnlySupportedProtocolsRaisesNoSkippedConnectionsWarning()
    {
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "con" });

        using var recorder = new MessageRecorder();
        new XmlConnectionsDeserializer().Deserialize(file.OuterXml);

        Assert.That(recorder.Matching(MessageClass.WarningMsg, "not supported by this build"), Is.Empty);
    }

    /// <summary>
    /// An attribute missing from the file is logged once per load, however many nodes lack it.
    /// </summary>
    /// <remarks>
    /// The one-node case is the one that tells "report the first time" from "report every time
    /// but the first". Loading twice with the same instance shows the record is reset per load.
    /// </remarks>
    [TestCase(1)]
    [TestCase(3)]
    public void AMissingAttributeIsLoggedOncePerLoad(int connectionsWithoutTheAttribute)
    {
        ConnectionInfo[] connections = Enumerable.Range(0, connectionsWithoutTheAttribute)
                                                 .Select(i => new ConnectionInfo { Name = "con" + i })
                                                 .ToArray();
        XmlDocument file = SerializedFile("Connections", connections);
        foreach (ConnectionInfo connection in connections)
        {
            ConnectionNode(file, connection.Name).RemoveAttribute("Descr");
        }

        var deserializer = new XmlConnectionsDeserializer();

        using var recorder = new MessageRecorder();
        deserializer.Deserialize(file.OuterXml);
        int afterFirstLoad = recorder.Matching(MessageClass.InformationMsg, "no 'Descr' attribute").Count;
        deserializer.Deserialize(file.OuterXml);

        IReadOnlyList<IMessage> reports = recorder.Matching(MessageClass.InformationMsg, "no 'Descr' attribute");
        Assert.Multiple(() =>
        {
            Assert.That(afterFirstLoad, Is.EqualTo(1));
            Assert.That(reports, Has.Count.EqualTo(2));
            Assert.That(reports.Select(report => report.OnlyLog), Is.All.True);
        });
    }

    /// <summary>
    /// A connection that fails to load is reported with the file it came from, not with an
    /// empty name.
    /// </summary>
    [Test]
    public void AConnectionThatFailsToParseIsReportedWithTheConnectionFilePath()
    {
        const string path = @"C:\Users\someone\confCons.xml";
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "broken" });
        ConnectionNode(file, "broken").SetAttribute("Password", "not an encrypted value");
        var deserializer = new XmlConnectionsDeserializer(connectionFileName: path);

        using var recorder = new MessageRecorder();
        deserializer.Deserialize(file.OuterXml);

        IReadOnlyList<IMessage> failures = recorder.Matching(MessageClass.ErrorMsg, "\"broken\"");
        Assert.That(failures.Select(message => message.Text),
                    Has.Exactly(1).Contains($"from \"{path}\""));
    }

    /// <summary>
    /// Neither a load nor an import marks the connections file loaded here: only
    /// ConnectionsService.LoadConnections knows whether the result becomes the model in use.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void DeserializingNeverMarksTheConnectionsFileLoaded(bool import)
    {
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "con" });
        bool wasLoaded = Runtime.ConnectionsService.IsConnectionsFileLoaded;
        try
        {
            Runtime.ConnectionsService.IsConnectionsFileLoaded = false;

            var deserializer = new XmlConnectionsDeserializer();
            if (import)
            {
                deserializer.Deserialize(file.OuterXml, true);
            }
            else
            {
                deserializer.Deserialize(file.OuterXml);
            }

            Assert.That(Runtime.ConnectionsService.IsConnectionsFileLoaded, Is.False);
        }
        finally
        {
            Runtime.ConnectionsService.IsConnectionsFileLoaded = wasLoaded;
        }
    }

    /// <summary>
    /// A load that fails leaves the loaded mark as it was, and says why in the log.
    /// </summary>
    /// <remarks>
    /// It used to clear the mark. But a failed load or import installs nothing: with a file
    /// open, the old model stays in use, and with the mark cleared every save of it was skipped
    /// without a word until another file was loaded. ConnectionsService.LoadConnections, the
    /// only place that installs a model, is the only place that sets the mark.
    /// </remarks>
    [TestCase(true)]
    [TestCase(false)]
    public void AFailedLoadLeavesTheLoadedMarkUnchangedAndReportsTheFailure(bool loaded)
    {
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "con" });
        file.DocumentElement!.SetAttribute("EncryptionEngine", "Twofish");
        bool wasLoaded = Runtime.ConnectionsService.IsConnectionsFileLoaded;
        try
        {
            Runtime.ConnectionsService.IsConnectionsFileLoaded = loaded;

            using var recorder = new MessageRecorder();
            Assert.Throws<UnsupportedEncryptionException>(
                () => new XmlConnectionsDeserializer().Deserialize(file.OuterXml));

            Assert.Multiple(() =>
            {
                Assert.That(Runtime.ConnectionsService.IsConnectionsFileLoaded, Is.EqualTo(loaded));
                Assert.That(recorder.Matching(MessageClass.ErrorMsg, Language.strLoadFromXmlFailed)
                                    .Select(message => message.Exception),
                            Has.One.InstanceOf<UnsupportedEncryptionException>());
            });
        }
        finally
        {
            Runtime.ConnectionsService.IsConnectionsFileLoaded = wasLoaded;
        }
    }

    [TestCase("AES", "GCM")]
    [TestCase("aes", "gcm")]
    public void AnAesGcmFileLoadsWithoutAnEncryptionWarning(string engine, string mode)
    {
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "con" });
        file.DocumentElement!.SetAttribute("EncryptionEngine", engine);
        file.DocumentElement.SetAttribute("BlockCipherMode", mode);

        using var recorder = new MessageRecorder();
        ConnectionTreeModel model = new XmlConnectionsDeserializer().DeserializeLoaded(file.OuterXml);

        Assert.Multiple(() =>
        {
            Assert.That(model.RootNodes[0].Children.Select(node => node.Name), Is.EqualTo(s_conArguments));
            Assert.That(recorder.Matching(MessageClass.WarningMsg, "Only AES+GCM is supported"), Is.Empty);
        });
    }

    /// <summary>
    /// Any other declared engine or mode is named in a warning before the load fails.
    /// </summary>
    /// <remarks>
    /// The lower-case cases show the names are parsed ignoring case: parsed case-sensitively,
    /// "twofish" would fall back to AES and the file would be read as if it were AES.
    /// </remarks>
    [TestCase("Twofish", "GCM", "Twofish+GCM")]
    [TestCase("twofish", "gcm", "Twofish+GCM")]
    [TestCase("AES", "CCM", "AES+CCM")]
    [TestCase("aes", "eax", "AES+EAX")]
    [TestCase("Serpent", "EAX", "Serpent+EAX")]
    public void AnUnsupportedEncryptionDeclarationIsNamedBeforeTheLoadFails(
        string engine, string mode, string expectedDeclaration)
    {
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "con" });
        file.DocumentElement!.SetAttribute("EncryptionEngine", engine);
        file.DocumentElement.SetAttribute("BlockCipherMode", mode);

        using var recorder = new MessageRecorder();
        Assert.Multiple(() =>
        {
            Assert.Throws<UnsupportedEncryptionException>(
                () => new XmlConnectionsDeserializer().Deserialize(file.OuterXml));
            Assert.That(recorder.Matching(MessageClass.WarningMsg, $"declares '{expectedDeclaration}'"),
                        Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AFileWithoutFullFileEncryptionIsReadAsNotEncrypted()
    {
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "con" });
        file.DocumentElement!.RemoveAttribute("FullFileEncryption");

        ConnectionTreeModel model = new XmlConnectionsDeserializer().DeserializeLoaded(file.OuterXml);

        Assert.That(model.RootNodes[0].Children.Select(node => node.Name), Is.EqualTo(s_conArguments));
    }

    /// <summary>A file written where the decimal separator is a comma is still version 2.9.</summary>
    [Test]
    public void AConfVersionWithACommaLoads()
    {
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "con" });
        file.DocumentElement!.SetAttribute("ConfVersion", "2,9");

        ConnectionTreeModel model = new XmlConnectionsDeserializer().DeserializeLoaded(file.OuterXml);

        Assert.That(model.RootNodes[0].Children.Select(node => node.Name), Is.EqualTo(s_conArguments));
    }

    /// <summary>
    /// A file at a version this build cannot read is refused with one dialog naming why, and
    /// the load fails rather than reading it as if it were 2.9. 2.8 is the old 128-bit nonce;
    /// a file without a version is read as version 0. The dialog's details and the exception
    /// name the file and its version.
    /// </summary>
    [TestCase("2.8", 2.8, "Connection file format no longer supported")]
    [TestCase("3.0", 3.0, "Incompatible connection file format")]
    [TestCase(null, 0.0, "Connection file format no longer supported")]
    public void AFileAtAnUnsupportedVersionIsRefused(string? confVersion, double readAs, string expectedHeading)
    {
        const string path = @"C:\Users\someone\confCons.xml";
        XmlDocument file = FileWithConfVersion(confVersion);
        string version = readAs.ToString(CultureInfo.CurrentCulture);

        List<(string Heading, string Explanation, string Details)> refusals = RecordingRefusals(() =>
        {
            var deserializer = new XmlConnectionsDeserializer(connectionFileName: path);
            Assert.That(() => deserializer.Deserialize(file.OuterXml),
                        Throws.TypeOf<InvalidDataException>().With.Message.Contains(version));
        });

        Assert.Multiple(() =>
        {
            Assert.That(refusals.Select(refusal => refusal.Heading), Is.EqualTo(new[] { expectedHeading }));
            Assert.That(refusals.Select(refusal => refusal.Details),
                        Has.All.Contains(path).And.All.Contains(version));
        });
    }

    [Test]
    public void AFileWithoutAConfVersionIsReportedAsAnOldFile()
    {
        XmlDocument file = FileWithConfVersion(null);

        using var recorder = new MessageRecorder();
        RecordingRefusals(() => Assert.Catch(() => new XmlConnectionsDeserializer().Deserialize(file.OuterXml)));

        Assert.That(recorder.Matching(MessageClass.WarningMsg, Language.strOldConffile), Has.Count.EqualTo(1));
    }

    /// <summary>
    /// A file holding nothing but whitespace and a byte-order mark is refused as empty, not as
    /// an old format whose passwords are lost, and the load fails as for the other refusals.
    /// </summary>
    [TestCase("")]
    [TestCase(" \r\n\t ")]
    [TestCase("\uFEFF")]
    [TestCase("\uFEFF\r\n  ")]
    public void AnEmptyFileIsRefusedAsEmpty(string content)
    {
        const string path = @"C:\Users\someone\confCons.xml";

        List<(string Heading, string Explanation, string Details)> refusals = RecordingRefusals(() =>
        {
            var deserializer = new XmlConnectionsDeserializer(connectionFileName: path);
            Assert.That(() => deserializer.Deserialize(content), Throws.TypeOf<InvalidDataException>());
        });

        Assert.Multiple(() =>
        {
            Assert.That(refusals.Select(refusal => refusal.Heading), Is.EqualTo(new[] { EmptyFileHeading }));
            Assert.That(refusals.Select(refusal => refusal.Explanation),
                        Has.All.Contains(path).And.All.Contains("is empty"));
            Assert.That(refusals.Select(refusal => refusal.Details), Has.All.Contains(path));
        });
    }

    /// <summary>The empty-file refusal names the newest backup, its folder and when it was taken.</summary>
    [Test]
    public void AnEmptyFileIsRefusedNamingItsNewestBackup()
    {
        DateTime newestTaken = new(2026, 11, 9, 8, 30, 0, DateTimeKind.Local);
        (string explanation, string directory, string file) = EmptyFileRefusal(true, 10, directory =>
        {
            File.WriteAllText(Path.Combine(directory, "confCons.xml.20261108-0830000000.backup"), "");
            File.WriteAllText(Path.Combine(directory, "confCons.xml.20261109-0830000000.backup"), "");
        });

        Assert.Multiple(() =>
        {
            Assert.That(explanation, Does.Contain(file));
            Assert.That(explanation, Does.Contain(directory));
            Assert.That(explanation, Does.Contain("confCons.xml.20261109-0830000000.backup"));
            Assert.That(explanation, Does.Not.Contain("confCons.xml.20261108-0830000000.backup"));
            Assert.That(explanation, Does.Contain(newestTaken.ToString("G", CultureInfo.CurrentCulture)));
        });
    }

    [Test]
    public void AnEmptyFileWithoutBackupsSaysSo()
    {
        (string explanation, string directory, _) = EmptyFileRefusal(true, 10, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(explanation, Does.Contain("No backup"));
            Assert.That(explanation, Does.Contain(directory));
        });
    }

    [TestCase(false, 10)]
    [TestCase(true, 0)]
    public void AnEmptyFileWithBackupsSwitchedOffSaysSo(bool enabled, int keepCount)
    {
        (string explanation, _, _) = EmptyFileRefusal(enabled, keepCount, directory =>
            File.WriteAllText(Path.Combine(directory, "confCons.xml.20261109-0830000000.backup"), ""));

        Assert.Multiple(() =>
        {
            Assert.That(explanation, Does.Contain("switched off"));
            Assert.That(explanation, Does.Not.Contain("20261109"));
        });
    }

    /// <summary>
    /// Loads an empty confCons.xml from a directory of its own, prepared by
    /// <paramref name="prepare"/>, with the given backup settings and the default backup name
    /// format, and returns the refusal's explanation, the directory and the file.
    /// </summary>
    private static (string Explanation, string Directory, string File) EmptyFileRefusal(
        bool backupsEnabled, int keepCount, Action<string> prepare)
    {
        string directory = FileTestHelpers.GetTestSpecificTempDirectory();
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "confCons.xml");
        string originalFormat = Settings.Default.BackupFileNameFormat;
        bool originalEnabled = Settings.Default.BackupFileEnabled;
        int originalKeepCount = Settings.Default.BackupFileKeepCount;
        try
        {
            Settings.Default.BackupFileNameFormat = "{0}.{1:yyyyMMdd-HHmmssffff}.backup";
            Settings.Default.BackupFileEnabled = backupsEnabled;
            Settings.Default.BackupFileKeepCount = keepCount;
            File.WriteAllText(file, "");
            prepare(directory);

            List<(string Heading, string Explanation, string Details)> refusals = RecordingRefusals(() =>
                Assert.That(() => new XmlConnectionsDeserializer(connectionFileName: file).Deserialize(""),
                            Throws.TypeOf<InvalidDataException>()));

            Assert.That(refusals.Select(refusal => refusal.Heading), Is.EqualTo(new[] { EmptyFileHeading }));
            return (refusals[0].Explanation, directory, file);
        }
        finally
        {
            Settings.Default.BackupFileNameFormat = originalFormat;
            Settings.Default.BackupFileEnabled = originalEnabled;
            Settings.Default.BackupFileKeepCount = originalKeepCount;
            FileTestHelpers.DeleteDirectory(directory);
        }
    }

    /// <summary>A file this build wrote, with its ConfVersion set, or removed when null.</summary>
    private static XmlDocument FileWithConfVersion(string? confVersion)
    {
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "con" });
        if (confVersion == null)
        {
            file.DocumentElement!.RemoveAttribute("ConfVersion");
        }
        else
        {
            file.DocumentElement!.SetAttribute("ConfVersion", confVersion);
        }

        return file;
    }

    /// <summary>
    /// Runs <paramref name="load"/> with the refusal dialog replaced, so nothing is shown, and
    /// returns what would have been. Restores the dialog and the loaded mark afterwards.
    /// </summary>
    private static List<(string Heading, string Explanation, string Details)> RecordingRefusals(Action load)
    {
        Action<string, string, string> originalShowRefusal = XmlConnectionsDeserializer.ShowRefusal;
        bool wasLoaded = Runtime.ConnectionsService.IsConnectionsFileLoaded;
        var refusals = new List<(string Heading, string Explanation, string Details)>();
        try
        {
            XmlConnectionsDeserializer.ShowRefusal = (heading, explanation, details) =>
                refusals.Add((heading, explanation, details));
            load();
            return refusals;
        }
        finally
        {
            XmlConnectionsDeserializer.ShowRefusal = originalShowRefusal;
            Runtime.ConnectionsService.IsConnectionsFileLoaded = wasLoaded;
        }
    }

    /// <summary>
    /// A connection without a saved password is written with empty Password and
    /// RDGatewayPassword attributes, which load as empty rather than being decrypted.
    /// </summary>
    [Test]
    public void EmptyPasswordAttributesLoadAsEmpty()
    {
        XmlDocument file = SerializedFile("Connections", new ConnectionInfo { Name = "con" });
        ConnectionNode(file, "con").SetAttribute("Password", "");
        ConnectionNode(file, "con").SetAttribute("RDGatewayPassword", "");

        ConnectionTreeModel model = new XmlConnectionsDeserializer().DeserializeLoaded(file.OuterXml);

        ConnectionInfo loaded = LoadedConnection(model, "con");
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Password, Is.Empty);
            Assert.That(loaded.RDGatewayPassword, Is.Empty);
        });
    }

    /// <summary>
    /// A file without KdfIterations is read with 1000 iterations, which for this fixture is
    /// also what it was written with, so the passwords decrypt.
    /// </summary>
    [Test]
    public void AFileWithoutKdfIterationsLoadsAndDecryptsWithTheDefault()
    {
        XmlDocument file = FixtureWithKdfIterations(Fixtures.confCons_v2_9, null);

        var deserializer = new XmlConnectionsDeserializer(() => "mR3m".ConvertToSecureString());
        ConnectionTreeModel model = deserializer.DeserializeLoaded(file.OuterXml);

        Assert.That(model.RootNodes[0].Children.Count, Is.EqualTo(3));
    }

    [TestCase("1000")]
    [TestCase(" 1000 ")]
    public void AFileDeclaringTheMinimumLoads(string declared)
    {
        XmlDocument file = FixtureWithKdfIterations(Fixtures.confCons_v2_9, declared);

        var deserializer = new XmlConnectionsDeserializer(() => "mR3m".ConvertToSecureString());
        ConnectionTreeModel model = deserializer.DeserializeLoaded(file.OuterXml);

        Assert.That(model.RootNodes[0].Children.Count, Is.EqualTo(3));
    }

    [TestCase("999")]
    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("abc")]
    [TestCase("")]
    [TestCase("1000.5")]
    [TestCase("1000001")]
    [TestCase("2000000000")]
    [TestCase("99999999999")]
    public void AFileDeclaringAnIterationCountOutOfRangeFailsTheLoadWithAClearMessage(string declared)
    {
        XmlDocument file = FixtureWithKdfIterations(Fixtures.confCons_v2_9, declared);
        using var recorder = new MessageRecorder();
        var deserializer = new XmlConnectionsDeserializer(() => "mR3m".ConvertToSecureString());

        Exception? thrown = Assert.Catch(() => deserializer.Deserialize(file.OuterXml));

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.InstanceOf<EncryptionException>());
            Assert.That(thrown, Is.Not.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(thrown!.Message, Does.Contain("KdfIterations"));
            Assert.That(thrown.Message, Does.Contain($"\"{declared}\""));
            Assert.That(thrown.Message, Does.Contain("1000 to 1000000"));
            Assert.That(recorder.Matching(MessageClass.ErrorMsg, Language.strLoadFromXmlFailed)
                                .Select(message => message.Exception),
                        Has.One.SameAs(thrown));
        });
    }

    /// <summary>The fixture's root element with KdfIterations set, or removed when null.</summary>
    private static XmlDocument FixtureWithKdfIterations(string fixture, string? iterations)
    {
        var file = new XmlDocument();
        file.LoadXml(fixture);
        if (iterations == null)
        {
            file.DocumentElement!.RemoveAttribute("KdfIterations");
        }
        else
        {
            file.DocumentElement!.SetAttribute("KdfIterations", iterations);
        }

        return file;
    }

    /// <summary>A file this build wrote, holding <paramref name="connections"/> under its root.</summary>
    private static XmlDocument SerializedFile(string rootName, params ConnectionInfo[] connections)
    {
        var rootNode = new RootNodeInfo(RootNodeType.Connection) { Name = rootName };
        foreach (ConnectionInfo connection in connections)
        {
            connection.Inheritance.TurnOffInheritanceCompletely();
            rootNode.AddChild(connection);
        }

        var model = new ConnectionTreeModel();
        model.AddRootNode(rootNode);
        ICryptographyProvider cryptographyProvider = new AeadCryptographyProvider();
        var nodeSerializer = new XmlConnectionNodeSerializer(
            cryptographyProvider, rootNode.PasswordString.ConvertToSecureString(), new SaveFilter());

        var file = new XmlDocument();
        file.LoadXml(new XmlConnectionsSerializer(cryptographyProvider, nodeSerializer).Serialize(model));
        return file;
    }

    private static XmlElement ConnectionNode(XmlDocument file, string name)
    {
        return (XmlElement)file.SelectSingleNode($"//Node[@Name='{name}']")!;
    }

    private static ConnectionInfo LoadedConnection(ConnectionTreeModel model, string name)
    {
        return model.GetRecursiveChildList().Single(node => node.Name == name);
    }

    private static bool ContainsNodeNamed(string name, IEnumerable<ConnectionInfo> list)
    {
        return list.Any(node => node.Name == name);
    }

    private static ContainerInfo GetFolderNamed(string name, IEnumerable<ConnectionInfo> list)
    {
        return list.OfType<ContainerInfo>().First(node => node.Name == name);
    }
}

public class XmlConnectionsDeserializerFixtureData
{
    public static IEnumerable FixtureParams
    {
        get
        {
            yield return new TestCaseData(new Datagram("confCons v2.9", Fixtures.confCons_v2_9, "mR3m"));
            yield return new TestCaseData(new Datagram("confCons v2.9 5k Iterations",
                                                       Fixtures.confCons_v2_9_5k_iterations, "mR3m"));
            yield return new TestCaseData(new Datagram("confCons v2.9 fullencryption",
                                                       Fixtures.confCons_v2_9_fullencryption, "mR3m"));
            yield return new TestCaseData(new Datagram("confCons v2.9 custompassword",
                                                       Fixtures.confCons_v2_9_passwordis_Password, "Password"));
            yield return new TestCaseData(new Datagram("confCons v2.9 custompassword,fullencryption",
                                                       Fixtures.confCons_v2_9_passwordis_Password_fullencryption,
                                                       "Password"));
        }
    }
}

public class Datagram
{
    private readonly string _testName;

    public string ConfCons { get; set; }
    public string Password { get; set; }

    public Datagram(string testName, string confCons, string password)
    {
        _testName = testName;
        ConfCons = confCons;
        Password = password;
    }

    public override string ToString()
    {
        return _testName;
    }
}
