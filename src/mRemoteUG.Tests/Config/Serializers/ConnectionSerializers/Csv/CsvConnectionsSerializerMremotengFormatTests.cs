using mRemoteUG.Config.Serializers.Csv;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Security;
using mRemoteUG.Tests.TestHelpers;
using mRemoteUG.Tree;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Serializers.ConnectionSerializers.Csv;

public class CsvConnectionsSerializerMremotengFormatTests
{
    private const string ConnectionName = "myconnection";
    private const string Username = "myuser";
    private const string Domain = "mydomain";
    private const string Password = "mypass123";
    private const string GatewayUsername = "mygatewayuser";
    private const string GatewayPassword = "mygatewaypass456";
    private const string GatewayDomain = "mygatewaydomain";

    [Test]
    public void SerializesNodeId()
    {
        var serializer = new CsvConnectionsSerializerMremotengFormat(new SaveFilter());
        ConnectionInfo connectionInfo = BuildConnectionInfo();
        string csv = serializer.Serialize(connectionInfo);
        Assert.That(csv, Does.Match(connectionInfo.ConstantID));
    }

    [Test]
    public void DoesntSerializeTheRootNode()
    {
        var serializer = new CsvConnectionsSerializerMremotengFormat(new SaveFilter());
        ConnectionTreeModel treeModel = ConnectionTreeModelBuilder.Build();
        string csv = serializer.Serialize(treeModel);
        Assert.That(csv, Does.Not.Match($"{treeModel.RootNodes[0].ConstantID};.*;{TreeNodeType.Root}"));
    }

    [TestCase(Username)]
    [TestCase(Domain)]
    [TestCase(Password)]
    [TestCase("InheritColors")]
    public void CreatesCsv(string valueThatShouldExist)
    {
        var serializer = new CsvConnectionsSerializerMremotengFormat(new SaveFilter());
        ConnectionInfo connectionInfo = BuildConnectionInfo();
        string csv = serializer.Serialize(connectionInfo);
        Assert.That(csv, Does.Match(valueThatShouldExist));
    }

    [TestCase(Username)]
    [TestCase(Domain)]
    [TestCase(Password)]
    [TestCase("InheritColors")]
    public void SerializerRespectsSaveFilterSettings(string valueThatShouldntExist)
    {
        var saveFilter = new SaveFilter(true);
        var serializer = new CsvConnectionsSerializerMremotengFormat(saveFilter);
        ConnectionInfo connectionInfo = BuildConnectionInfo();
        string csv = serializer.Serialize(connectionInfo);
        Assert.That(csv, Does.Not.Match(valueThatShouldntExist));
    }

    [Test]
    public void CanSerializeEmptyConnectionInfo()
    {
        var serializer = new CsvConnectionsSerializerMremotengFormat(new SaveFilter());
        var connectionInfo = new ConnectionInfo();
        string csv = serializer.Serialize(connectionInfo);
        Assert.That(csv, Is.Not.Empty);
    }

    [Test]
    public void CantPassNullToConstructor()
    {
        // null! here and below: passing null is the point, to reach the argument guard.
        Assert.Throws<ArgumentNullException>(() => new CsvConnectionsSerializerMremotengFormat(null!));
    }

    [Test]
    public void CantPassNullToSerializeConnectionInfo()
    {
        var serializer = new CsvConnectionsSerializerMremotengFormat(new SaveFilter());
        Assert.Throws<ArgumentNullException>(() => serializer.Serialize((ConnectionInfo)null!));
    }

    [Test]
    public void CantPassNullToSerializeConnectionTreeModel()
    {
        var serializer = new CsvConnectionsSerializerMremotengFormat(new SaveFilter());
        Assert.Throws<ArgumentNullException>(() => serializer.Serialize((ConnectionTreeModel)null!));
    }

    [Test]
    public void FoldersAreSerialized()
    {
        var serializer = new CsvConnectionsSerializerMremotengFormat(new SaveFilter());
        ContainerInfo container = BuildContainer();
        string csv = serializer.Serialize(container);
        Assert.That(csv, Does.Match(container.Name));
        Assert.That(csv, Does.Match(container.Username));
        Assert.That(csv, Does.Match(container.Domain));
        Assert.That(csv, Does.Match(container.Password));
        Assert.That(csv, Does.Contain(TreeNodeType.Container.ToString()));
    }

    [Test]
    public void SerializationIncludesRawInheritedValuesIfObjectInheritsFromParentOutsideOfSerializationScope()
    {
        var serializer = new CsvConnectionsSerializerMremotengFormat(new SaveFilter());
        ConnectionTreeModel treeModel = ConnectionTreeModelBuilder.Build();
        ConnectionInfo serializationTarget = treeModel.GetRecursiveChildList().First(info => info.Name == "folder3");
        string csv = serializer.Serialize(serializationTarget);
        string lineWithFolder3 = csv
            .Split(new[] { Environment.NewLine }, StringSplitOptions.None)
            .First(s => s.Contains(serializationTarget.Name));
        Assert.That(lineWithFolder3, Does.Contain(serializationTarget.Username));
        Assert.That(lineWithFolder3, Does.Contain(serializationTarget.Domain));
        Assert.That(lineWithFolder3, Does.Contain(serializationTarget.Password));
    }

    [Test]
    public void GatewayCredentialsStayOutOfTheCsvWhenTheSaveFilterExcludesCredentials()
    {
        // The reproduction from #104: with every credential flag off, the gateway secrets used to
        // be written anyway, in clear text.
        var con = new ConnectionInfo
        {
            Name = "c1",
            Hostname = "h1",
            Password = "connsecret",
            RDGatewayUsername = "gwuser",
            RDGatewayPassword = "gwsecret",
            RDGatewayDomain = "gwdom"
        };
        var filter = new SaveFilter { SavePassword = false, SaveUsername = false, SaveDomain = false };

        string csv = new CsvConnectionsSerializerMremotengFormat(filter).Serialize(con);

        Assert.That(csv, Does.Not.Contain("connsecret"));
        Assert.That(csv, Does.Not.Contain("gwuser"));
        Assert.That(csv, Does.Not.Contain("gwsecret"));
        Assert.That(csv, Does.Not.Contain("gwdom"));
    }

    [Test]
    public void CredentialColumnsFollowTheSaveFilter(
        [Values] bool saveUsername,
        [Values] bool savePassword,
        [Values] bool saveDomain)
    {
        var filter = new SaveFilter
        {
            SaveUsername = saveUsername,
            SavePassword = savePassword,
            SaveDomain = saveDomain
        };
        ConnectionInfo connectionInfo = BuildConnectionInfo();
        connectionInfo.RDGatewayUsername = GatewayUsername;
        connectionInfo.RDGatewayPassword = GatewayPassword;
        connectionInfo.RDGatewayDomain = GatewayDomain;

        string csv = new CsvConnectionsSerializerMremotengFormat(filter).Serialize(connectionInfo);

        string[] lines = csv.Split(Environment.NewLine);
        Assert.That(lines, Has.Length.EqualTo(2));
        string[] header = SplitCsvLine(lines[0]);
        string[] row = SplitCsvLine(lines[1]);
        Assert.That(row, Has.Length.EqualTo(header.Length), "header and row have different field counts");
        AssertColumn(header, row, "Username", saveUsername, Username);
        AssertColumn(header, row, "Password", savePassword, Password);
        AssertColumn(header, row, "Domain", saveDomain, Domain);
        AssertColumn(header, row, "RDGatewayUsername", saveUsername, GatewayUsername);
        AssertColumn(header, row, "RDGatewayPassword", savePassword, GatewayPassword);
        AssertColumn(header, row, "RDGatewayDomain", saveDomain, GatewayDomain);
        AssertColumn(header, row, "InheritRDGatewayUsername", true, bool.FalseString);
        AssertColumn(header, row, "InheritRDGatewayPassword", true, bool.FalseString);
        AssertColumn(header, row, "InheritRDGatewayDomain", true, bool.FalseString);
    }

    private const string HeaderStart = "Name;Id;Parent;NodeType;Description;Icon;Panel;";

    private const string HeaderConnectionColumns =
        "Hostname;Protocol;PuttySession;Port;ConnectToConsole;UseCredSsp;RDPAuthenticationLevel;"
        + "LoadBalanceInfo;Colors;Resolution;AutomaticResize;DisplayWallpaper;DisplayThemes;"
        + "EnableFontSmoothing;EnableDesktopComposition;CacheBitmaps;RedirectDiskDrives;RedirectPorts;"
        + "RedirectPrinters;RedirectSmartCards;RedirectClipboard;RedirectSound;RedirectKeys;MacAddress;"
        + "UserField;RDGatewayUsageMethod;RDGatewayHostname;RDGatewayUseConnectionCredentials;";

    private const string HeaderInheritanceColumns =
        "InheritCacheBitmaps;InheritColors;InheritDescription;InheritDisplayThemes;"
        + "InheritDisplayWallpaper;InheritEnableFontSmoothing;InheritEnableDesktopComposition;"
        + "InheritDomain;InheritIcon;InheritPanel;InheritPassword;InheritPort;InheritProtocol;"
        + "InheritPuttySession;InheritRedirectDiskDrives;InheritRedirectKeys;InheritRedirectPorts;"
        + "InheritRedirectPrinters;InheritRedirectSmartCards;InheritRedirectClipboard;InheritRedirectSound;"
        + "InheritResolution;InheritAutomaticResize;InheritUseConsoleSession;InheritUseCredSsp;"
        + "InheritUsername;InheritRDPAuthenticationLevel;InheritLoadBalanceInfo;InheritMacAddress;"
        + "InheritUserField;InheritRDGatewayUsageMethod;InheritRDGatewayHostname;"
        + "InheritRDGatewayUseConnectionCredentials;InheritRDGatewayUsername;InheritRDGatewayPassword;"
        + "InheritRDGatewayDomain;InheritRDPAlertIdleTimeout;InheritRDPMinutesToIdleTimeout;"
        + "InheritSoundQuality";

    [Test]
    public void TheHeaderListsEveryColumnInOrderWhenNothingIsFiltered()
    {
        string csv = new CsvConnectionsSerializerMremotengFormat(new SaveFilter()).Serialize(BuildConnectionInfo());

        Assert.That(csv.Split(Environment.NewLine)[0],
                    Is.EqualTo(HeaderStart + "Username;Password;Domain;" + HeaderConnectionColumns
                               + "RDGatewayUsername;RDGatewayPassword;RDGatewayDomain;"
                               + HeaderInheritanceColumns));
    }

    [Test]
    public void TheHeaderLeavesOutFilteredColumns()
    {
        string csv = new CsvConnectionsSerializerMremotengFormat(new SaveFilter(true))
            .Serialize(BuildConnectionInfo());

        Assert.That(csv.Split(Environment.NewLine)[0], Is.EqualTo(HeaderStart + HeaderConnectionColumns));
    }

    [Test]
    public void EachFlagAddsOnlyItsOwnColumns(
        [Values] bool saveUsername,
        [Values] bool savePassword,
        [Values] bool saveDomain)
    {
        var filter = new SaveFilter(true)
        {
            SaveUsername = saveUsername,
            SavePassword = savePassword,
            SaveDomain = saveDomain
        };

        string csv = new CsvConnectionsSerializerMremotengFormat(filter).Serialize(BuildConnectionInfo());

        string expected = HeaderStart
                          + (saveUsername ? "Username;" : "")
                          + (savePassword ? "Password;" : "")
                          + (saveDomain ? "Domain;" : "")
                          + HeaderConnectionColumns
                          + (saveUsername ? "RDGatewayUsername;" : "")
                          + (savePassword ? "RDGatewayPassword;" : "")
                          + (saveDomain ? "RDGatewayDomain;" : "");
        Assert.That(csv.Split(Environment.NewLine)[0], Is.EqualTo(expected));
    }

    [Test]
    public void AConnectionInAFolderIsSerializedOnItsOwnLineWithTheFoldersIdAsParent()
    {
        ContainerInfo folder = BuildContainer();
        var child = new ConnectionInfo { Name = ConnectionName };
        folder.AddChild(child);

        string csv = new CsvConnectionsSerializerMremotengFormat(new SaveFilter()).Serialize(folder);

        string[] lines = csv.Split(Environment.NewLine);
        Assert.That(lines, Has.Length.EqualTo(3), "header, connection row, folder row");
        string[] header = SplitCsvLine(lines[0]);
        string[] childRow = SplitCsvLine(lines.Single(line => RowOf(line, ConnectionName)));
        string[] folderRow = SplitCsvLine(lines.Single(line => RowOf(line, folder.Name)));
        Assert.Multiple(() =>
        {
            Assert.That(childRow, Has.Length.EqualTo(header.Length));
            Assert.That(folderRow, Has.Length.EqualTo(header.Length));
            Assert.That(childRow[Array.IndexOf(header, "Id")], Is.EqualTo(child.ConstantID));
            Assert.That(childRow[Array.IndexOf(header, "Parent")], Is.EqualTo(folder.ConstantID));
            Assert.That(childRow[Array.IndexOf(header, "NodeType")], Is.EqualTo(nameof(TreeNodeType.Connection)));
            Assert.That(folderRow[Array.IndexOf(header, "NodeType")], Is.EqualTo(nameof(TreeNodeType.Container)));
        });
    }

    [Test]
    public void ANodeWithoutAParentHasAnEmptyParentField()
    {
        string csv = new CsvConnectionsSerializerMremotengFormat(new SaveFilter()).Serialize(BuildConnectionInfo());

        string[] lines = csv.Split(Environment.NewLine);
        string[] header = SplitCsvLine(lines[0]);
        string[] row = SplitCsvLine(lines[1]);
        Assert.That(row[Array.IndexOf(header, "Parent")], Is.Empty);
    }

    [Test]
    public void EveryRowIsOnItsOwnLineAndMatchesTheHeader(
        [Values] bool saveInheritance)
    {
        ContainerInfo folder = BuildContainer();
        folder.AddChild(new ConnectionInfo { Name = "first" });
        folder.AddChild(new ConnectionInfo { Name = "second" });
        var filter = new SaveFilter { SaveInheritance = saveInheritance };

        string csv = new CsvConnectionsSerializerMremotengFormat(filter).Serialize(folder);

        string[] lines = csv.Split(Environment.NewLine);
        Assert.That(lines, Has.Length.EqualTo(4));
        int fields = SplitCsvLine(lines[0]).Length;
        Assert.That(lines.Skip(1).Select(line => SplitCsvLine(line).Length), Is.All.EqualTo(fields));
    }

    [Test]
    public void InheritanceColumnsAreWrittenWithTheirValuesWhenSaved()
    {
        string csv = new CsvConnectionsSerializerMremotengFormat(new SaveFilter()).Serialize(BuildConnectionInfo());

        string[] lines = csv.Split(Environment.NewLine);
        string[] header = SplitCsvLine(lines[0]);
        string[] row = SplitCsvLine(lines[1]);
        Assert.Multiple(() =>
        {
            Assert.That(row[Array.IndexOf(header, "InheritColors")], Is.EqualTo(bool.TrueString));
            Assert.That(row[Array.IndexOf(header, "InheritDescription")], Is.EqualTo(bool.FalseString));
            Assert.That(row[Array.IndexOf(header, "InheritCacheBitmaps")], Is.EqualTo(bool.FalseString));
            Assert.That(row[Array.IndexOf(header, "InheritSoundQuality")], Is.EqualTo(bool.FalseString));
        });
    }

    [Test]
    public void InheritanceColumnsAreAbsentFromHeaderAndRowsWhenNotSaved()
    {
        var filter = new SaveFilter { SaveInheritance = false };

        string csv = new CsvConnectionsSerializerMremotengFormat(filter).Serialize(BuildConnectionInfo());

        string[] lines = csv.Split(Environment.NewLine);
        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Does.Not.Contain("Inherit"));
            Assert.That(SplitCsvLine(lines[1]), Has.Length.EqualTo(SplitCsvLine(lines[0]).Length));
        });
    }

    [Test]
    public void SemicolonsInAValueAreDroppedSoTheyCannotShiftTheColumns()
    {
        var con = new ConnectionInfo { Name = "a;b", Description = ";x;;y;" };

        string csv = new CsvConnectionsSerializerMremotengFormat(new SaveFilter()).Serialize(con);

        string[] lines = csv.Split(Environment.NewLine);
        string[] header = SplitCsvLine(lines[0]);
        string[] row = SplitCsvLine(lines[1]);
        Assert.Multiple(() =>
        {
            Assert.That(row, Has.Length.EqualTo(header.Length));
            Assert.That(row[Array.IndexOf(header, "Name")], Is.EqualTo("ab"));
            Assert.That(row[Array.IndexOf(header, "Description")], Is.EqualTo("xy"));
        });
    }

    [Test]
    public void ATreeWithoutARootNodeCannotBeSerialized()
    {
        var serializer = new CsvConnectionsSerializerMremotengFormat(new SaveFilter());

        Assert.Throws<InvalidOperationException>(() => serializer.Serialize(new ConnectionTreeModel()));
    }

    // FormatForCsv strips ';' from every value and terminates each with one, so a line splits
    // cleanly on ';'. The header ends without a terminator when the inheritance columns are
    // written, the rows always end with one; drop that single trailing terminator from both.
    private static string[] SplitCsvLine(string line)
    {
        string trimmed = line.EndsWith(';') ? line[..^1] : line;
        return trimmed.Split(';');
    }

    private static bool RowOf(string line, string name)
    {
        return line.StartsWith(name + ";", StringComparison.Ordinal);
    }

    private static void AssertColumn(
        string[] header, string[] row, string column, bool expectPresent, string expectedValue)
    {
        if (!expectPresent)
        {
            Assert.That(header, Does.Not.Contain(column), $"column {column} should be left out");
            Assert.That(row, Does.Not.Contain(expectedValue), $"value of {column} should be left out");
            return;
        }

        Assert.That(header.Count(name => name == column), Is.EqualTo(1), $"column {column} should appear once");
        Assert.That(row[Array.IndexOf(header, column)], Is.EqualTo(expectedValue), $"value of {column}");
    }

    private static ConnectionInfo BuildConnectionInfo()
    {
        return new ConnectionInfo
        {
            Name = ConnectionName,
            Username = Username,
            Domain = Domain,
            Password = Password,
            Inheritance = { Colors = true }
        };
    }

    private static ContainerInfo BuildContainer()
    {
        return new ContainerInfo
        {
            Name = "MyFolder",
            Username = "BlahBlah1",
            Domain = "aklkskkksh8",
            Password = "qweraslkdjf87"
        };
    }
}
