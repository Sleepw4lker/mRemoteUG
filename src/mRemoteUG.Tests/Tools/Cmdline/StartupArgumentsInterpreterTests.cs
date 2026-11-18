using mRemoteUG.App.Info;
using mRemoteUG.Messages;
using mRemoteUG.Tests.TestHelpers;
using mRemoteUG.Tools.Cmdline;

using NUnit.Framework;

namespace mRemoteUG.Tests.Tools.Cmdline;

/// <summary>
/// Every test starts from known settings and puts the user's back afterwards: the interpreter
/// writes straight to <c>Settings.Default</c>.
/// </summary>
[TestFixture]
public class StartupArgumentsInterpreterTests
{
    private static readonly string[] s_resetposArguments = { "/resetpos" };
    private static readonly string[] s_resetArguments = { "/reset" };
    private static readonly string[] s_rpnlNorcArguments = { "/rpnl", "/norc" };
    private static readonly string[] s_noreconnectArguments = { "/NoReconnect" };
    private static readonly string[] s_resetLogMessages =
    {
        "Parsing cmdline arguments",
        "Cmdline arg: Resetting window positions.",
        "Cmdline arg: Resetting panels",
        "Cmdline arg: Resetting toolbar position",
        "Cmdline arg: Disabling reconnection to previously connected hosts",
        "Cmdline arg: loading connections from a custom path"
    };
    private static readonly string[] s_consArguments = { "/cons" };

    private static readonly Point s_markerLocation = new Point(7, 9);
    private static readonly Size s_markerSize = new Size(321, 123);

    // Rebuilt per test in Setup, with the settings and working directory each test may change.
    private MessageCollector _collector = null!;
    private StartupArgumentsInterpreter _interpreter = null!;
    private readonly List<string> _filesToDelete = new List<string>();
    private string? _directoryToDelete;
    private string _workingDirectory = null!;
    private string _emptyDirectory = null!;

    private bool _kiosk;
    private Point _location;
    private Size _size;
    private FormWindowState _state;
    private bool _resetPanels;
    private bool _resetToolbars;
    private bool _noReconnect;
    private bool _loadFromCustomLocation;
    private string _customPath = null!;

    [SetUp]
    public void Setup()
    {
        _kiosk = Settings.Default.MainFormKiosk;
        _location = Settings.Default.MainFormLocation;
        _size = Settings.Default.MainFormSize;
        _state = Settings.Default.MainFormState;
        _resetPanels = Settings.Default.ResetPanels;
        _resetToolbars = Settings.Default.ResetToolbars;
        _noReconnect = Settings.Default.NoReconnect;
        _loadFromCustomLocation = Settings.Default.LoadConsFromCustomLocation;
        _customPath = Settings.Default.CustomConsPath;

        Settings.Default.MainFormKiosk = true;
        Settings.Default.MainFormLocation = s_markerLocation;
        Settings.Default.MainFormSize = s_markerSize;
        Settings.Default.MainFormState = FormWindowState.Maximized;
        Settings.Default.ResetPanels = false;
        Settings.Default.ResetToolbars = false;
        Settings.Default.NoReconnect = false;
        Settings.Default.LoadConsFromCustomLocation = false;
        Settings.Default.CustomConsPath = "marker";

        // The first lookup is relative to the working directory, which is the application directory
        // under the test host. Starting somewhere empty keeps the later lookups apart.
        _workingDirectory = Environment.CurrentDirectory;
        _emptyDirectory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_emptyDirectory);
        Environment.CurrentDirectory = _emptyDirectory;

        _collector = new MessageCollector();
        _interpreter = new StartupArgumentsInterpreter(_collector);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.CurrentDirectory = _workingDirectory;
        Directory.Delete(_emptyDirectory, true);
        Settings.Default.MainFormKiosk = _kiosk;
        Settings.Default.MainFormLocation = _location;
        Settings.Default.MainFormSize = _size;
        Settings.Default.MainFormState = _state;
        Settings.Default.ResetPanels = _resetPanels;
        Settings.Default.ResetToolbars = _resetToolbars;
        Settings.Default.NoReconnect = _noReconnect;
        Settings.Default.LoadConsFromCustomLocation = _loadFromCustomLocation;
        Settings.Default.CustomConsPath = _customPath;

        foreach (string file in _filesToDelete)
        {
            FileTestHelpers.DeleteTestFile(file);
        }

        if (_directoryToDelete != null && Directory.Exists(_directoryToDelete))
        {
            Directory.Delete(_directoryToDelete, true);
        }
    }

    [Test]
    public void ANullMessageCollectorIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new StartupArgumentsInterpreter(null!));
    }

    [Test]
    public void NoArgumentsChangeNothing()
    {
        _interpreter.ParseArguments(Array.Empty<string>());

        AssertWindowUntouched();
        AssertResetsAndReconnectUntouched();
        AssertConnectionsPathUntouched();
    }

    [Test]
    public void ParsingIsLoggedAsADebugMessage()
    {
        _interpreter.ParseArguments(Array.Empty<string>());

        Assert.That(
            _collector.Messages.Select(m => m.Class),
            Is.EqualTo(new[] { MessageClass.DebugMsg }));
        Assert.That(_collector.Messages.Single().Text, Is.EqualTo("Parsing cmdline arguments"));
    }

    [TestCase("/resetpos")]
    [TestCase("/rp")]
    [TestCase("-rp")]
    [TestCase("--resetpos")]
    public void ResetPositionResetsOnlyTheWindow(string argument)
    {
        Assume.That(Screen.PrimaryScreen, Is.Not.Null);

        _interpreter.ParseArguments(new[] { argument });

        AssertWindowReset();
        AssertResetsAndReconnectUntouched();
        AssertConnectionsPathUntouched();
    }

    [Test]
    public void ResetPositionCentresA900By600WindowInThePrimaryWorkingArea()
    {
        Assume.That(Screen.PrimaryScreen, Is.Not.Null);
        Rectangle area = Screen.PrimaryScreen!.WorkingArea; // The Assume above.

        _interpreter.ParseArguments(s_resetposArguments);

        Assert.That(Settings.Default.MainFormSize, Is.EqualTo(new Size(900, 600)));
        Assert.That(
            Settings.Default.MainFormLocation,
            Is.EqualTo(new Point(area.Width / 2 - 450, area.Height / 2 - 300)));
        Assert.That(Settings.Default.MainFormState, Is.EqualTo(FormWindowState.Normal));
        Assert.That(Settings.Default.MainFormKiosk, Is.False);
    }

    [TestCase("/resetpanels")]
    [TestCase("/rpnl")]
    public void ResetPanelsResetsOnlyThePanels(string argument)
    {
        _interpreter.ParseArguments(new[] { argument });

        Assert.That(Settings.Default.ResetPanels, Is.True);
        Assert.That(Settings.Default.ResetToolbars, Is.False);
        Assert.That(Settings.Default.NoReconnect, Is.False);
        AssertWindowUntouched();
        AssertConnectionsPathUntouched();
    }

    [TestCase("/resettoolbar")]
    [TestCase("/rtbr")]
    public void ResetToolbarResetsOnlyTheToolbars(string argument)
    {
        _interpreter.ParseArguments(new[] { argument });

        Assert.That(Settings.Default.ResetToolbars, Is.True);
        Assert.That(Settings.Default.ResetPanels, Is.False);
        Assert.That(Settings.Default.NoReconnect, Is.False);
        AssertWindowUntouched();
        AssertConnectionsPathUntouched();
    }

    [Test]
    public void ResetResetsTheWindowThePanelsAndTheToolbars()
    {
        Assume.That(Screen.PrimaryScreen, Is.Not.Null);

        _interpreter.ParseArguments(s_resetArguments);

        AssertWindowReset();
        Assert.That(Settings.Default.ResetPanels, Is.True);
        Assert.That(Settings.Default.ResetToolbars, Is.True);
        Assert.That(Settings.Default.NoReconnect, Is.False);
        AssertConnectionsPathUntouched();
    }

    [TestCase("/noreconnect")]
    [TestCase("/norc")]
    public void NoReconnectDisablesOnlyReconnecting(string argument)
    {
        _interpreter.ParseArguments(new[] { argument });

        Assert.That(Settings.Default.NoReconnect, Is.True);
        Assert.That(Settings.Default.ResetPanels, Is.False);
        Assert.That(Settings.Default.ResetToolbars, Is.False);
        AssertWindowUntouched();
        AssertConnectionsPathUntouched();
    }

    [Test]
    public void SeveralSwitchesCombine()
    {
        _interpreter.ParseArguments(s_rpnlNorcArguments);

        Assert.That(Settings.Default.ResetPanels, Is.True);
        Assert.That(Settings.Default.NoReconnect, Is.True);
        Assert.That(Settings.Default.ResetToolbars, Is.False);
    }

    [Test]
    public void ASwitchIsFoundWhateverItsCase()
    {
        _interpreter.ParseArguments(s_noreconnectArguments);

        Assert.That(Settings.Default.NoReconnect, Is.True);
    }

    [Test]
    public void EachSwitchIsLoggedAsADebugMessage()
    {
        Assume.That(Screen.PrimaryScreen, Is.Not.Null);

        _interpreter.ParseArguments(new[] { "/reset", "/norc", "/c", "no-such-file-" + Guid.NewGuid().ToString("N") });

        string[] texts = _collector.Messages.Select(m => m.Text).ToArray();
        Assert.That(
            texts,
            Is.EqualTo(s_resetLogMessages));
        Assert.That(_collector.Messages.Select(m => m.Class), Has.All.EqualTo(MessageClass.DebugMsg));
    }

    [TestCase("/c")]
    [TestCase("/cons")]
    [TestCase("-cons")]
    public void AnExistingAbsoluteFileIsLoadedFromThere(string switchName)
    {
        string file = CreateTempFile(Path.GetTempPath());

        _interpreter.ParseArguments(new[] { switchName + "=" + file });

        Assert.That(Settings.Default.LoadConsFromCustomLocation, Is.True);
        Assert.That(Settings.Default.CustomConsPath, Is.EqualTo(file));
        AssertWindowUntouched();
        AssertResetsAndReconnectUntouched();
    }

    [Test]
    public void AFileInTheWorkingDirectoryIsKeptUnderTheNameItWasGiven()
    {
        string file = CreateTempFile(_emptyDirectory);
        string name = Path.GetFileName(file);

        _interpreter.ParseArguments(new[] { "/c", name });

        Assert.That(Settings.Default.LoadConsFromCustomLocation, Is.True);
        Assert.That(Settings.Default.CustomConsPath, Is.EqualTo(name));
    }

    [Test]
    public void AFileNextToTheApplicationIsFoundByItsRelativeName()
    {
        Assume.That(GeneralAppInfo.HomePath, Is.Not.Null);
        string file = CreateTempFile(GeneralAppInfo.HomePath);
        string name = Path.GetFileName(file);

        _interpreter.ParseArguments(new[] { "/c", name });

        Assert.That(Settings.Default.LoadConsFromCustomLocation, Is.True);
        Assert.That(Settings.Default.CustomConsPath, Is.EqualTo(Path.Combine(GeneralAppInfo.HomePath, name)));
    }

    [Test]
    public void AFileInTheDefaultConnectionsDirectoryIsFoundByItsRelativeName()
    {
        string directory = ConnectionsFileInfo.DefaultConnectionsPath;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _directoryToDelete = directory;
        }

        string file = CreateTempFile(directory);
        string name = Path.GetFileName(file);

        _interpreter.ParseArguments(new[] { "/cons", name });

        Assert.That(Settings.Default.LoadConsFromCustomLocation, Is.True);
        Assert.That(Settings.Default.CustomConsPath, Is.EqualTo(Path.Combine(directory, name)));
    }

    [Test]
    public void TheApplicationDirectoryComesBeforeTheDefaultConnectionsDirectory()
    {
        Assume.That(GeneralAppInfo.HomePath, Is.Not.Null);
        string directory = ConnectionsFileInfo.DefaultConnectionsPath;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _directoryToDelete = directory;
        }

        string name = "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N") + ".xml";
        string inHome = Path.Combine(GeneralAppInfo.HomePath, name);
        string inDefault = Path.Combine(directory, name);
        File.WriteAllText(inHome, "");
        File.WriteAllText(inDefault, "");
        _filesToDelete.Add(inHome);
        _filesToDelete.Add(inDefault);

        _interpreter.ParseArguments(new[] { "/c", name });

        Assert.That(Settings.Default.CustomConsPath, Is.EqualTo(inHome));
    }

    [Test]
    public void AMissingFileChangesNothing()
    {
        string missing = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N") + ".xml");

        _interpreter.ParseArguments(new[] { "/c=" + missing });
        _interpreter.ParseArguments(new[] { "/c", Path.GetFileName(missing) });

        AssertConnectionsPathUntouched();
    }

    [Test]
    public void TheLongerSwitchIsOverriddenByTheShorterOne()
    {
        string file = CreateTempFile(Path.GetTempPath());
        string missing = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N") + ".xml");

        _interpreter.ParseArguments(new[] { "/cons=" + missing, "/c=" + file });

        Assert.That(Settings.Default.CustomConsPath, Is.EqualTo(file));
    }

    [Test]
    public void ABareConsSwitchIsLookedUpAsAFileNamedTrue()
    {
        _interpreter.ParseArguments(s_consArguments);

        AssertConnectionsPathUntouched();
    }

    [Test]
    public void AGarbageArgumentIsReportedAndNotThrown()
    {
        using var recorder = new MessageRecorder();

        Assert.DoesNotThrow(() => _interpreter.ParseArguments(new string[] { null!, "???" }));

        AssertWindowUntouched();
        AssertResetsAndReconnectUntouched();
        AssertConnectionsPathUntouched();
        Assert.That(recorder.Matching(MessageClass.ErrorMsg, "Creating new Args failed"), Has.Count.EqualTo(1));
    }

    private string CreateTempFile(string directory)
    {
        string file = Path.Combine(directory, "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N") + ".xml");
        File.WriteAllText(file, "");
        _filesToDelete.Add(file);
        return file;
    }

    private static void AssertWindowReset()
    {
        Assert.That(Settings.Default.MainFormKiosk, Is.False);
        Assert.That(Settings.Default.MainFormState, Is.EqualTo(FormWindowState.Normal));
        Assert.That(Settings.Default.MainFormSize, Is.EqualTo(new Size(900, 600)));
        Assert.That(Settings.Default.MainFormLocation, Is.Not.EqualTo(s_markerLocation));
    }

    private static void AssertWindowUntouched()
    {
        Assert.That(Settings.Default.MainFormKiosk, Is.True);
        Assert.That(Settings.Default.MainFormState, Is.EqualTo(FormWindowState.Maximized));
        Assert.That(Settings.Default.MainFormSize, Is.EqualTo(s_markerSize));
        Assert.That(Settings.Default.MainFormLocation, Is.EqualTo(s_markerLocation));
    }

    private static void AssertResetsAndReconnectUntouched()
    {
        Assert.That(Settings.Default.ResetPanels, Is.False);
        Assert.That(Settings.Default.ResetToolbars, Is.False);
        Assert.That(Settings.Default.NoReconnect, Is.False);
    }

    private static void AssertConnectionsPathUntouched()
    {
        Assert.That(Settings.Default.LoadConsFromCustomLocation, Is.False);
        Assert.That(Settings.Default.CustomConsPath, Is.EqualTo("marker"));
    }
}
