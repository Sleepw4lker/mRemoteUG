using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using mRemoteUG.App;
using mRemoteUG.UI.Forms.OptionsPages;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Forms.OptionsPages;

/// <summary>
/// The logging half of Options -> Notifications, which decides both what is logged and where.
/// </summary>
/// <remarks>
/// The private Load/SaveLoggingSettings are called directly rather than the public
/// LoadSettings/SaveSettings, because the public ones call Settings.Default.Save() and a test
/// has no business writing the user's configuration file.
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _page after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class NotificationsPageTests
{
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private NotificationsPage _page = null!;
    private CheckBox _useDefaultLocation = null!;
    private TextBox _logPath = null!;

    // Snapshots taken by [SetUp] before each test and put back by [TearDown], so null! rather
    // than an initializer here; see ADR-0022 on when that is the right answer.
    private string _originalLogFilePath = null!;
    private bool _originalLogToApplicationDirectory;
    private string _originalLoggerPath = null!;

    [SetUp]
    public void Setup()
    {
        _originalLogFilePath = Settings.Default.LogFilePath;
        _originalLogToApplicationDirectory = Settings.Default.LogToApplicationDirectory;
        _originalLoggerPath = Logger.Instance.LogPath;

        _page = new NotificationsPage();
        _useDefaultLocation = (CheckBox)_page.Controls.Find("chkLogToCurrentDir", true)[0];
        _logPath = (TextBox)_page.Controls.Find("textBoxLogPath", true)[0];
    }

    [TearDown]
    public void TearDown()
    {
        _page.Dispose();
        Settings.Default.LogFilePath = _originalLogFilePath;
        Settings.Default.LogToApplicationDirectory = _originalLogToApplicationDirectory;
        Logger.Instance.SetLogPath(_originalLoggerPath);
    }

    private static string CustomPath => Path.Combine(Path.GetTempPath(), "mRemoteUG-custom.log");

    private void Load() => Invoke("LoadLoggingSettings");
    private void Save() => Invoke("SaveLoggingSettings");

    private void Invoke(string methodName)
    {
        MethodInfo method = typeof(NotificationsPage).GetMethod(methodName,
                                                         BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? throw new InvalidOperationException($"{methodName} is gone - update this test.");
        method.Invoke(_page, null);
    }

    [Test]
    public void LoadingShowsTheCustomPathWhenTheDefaultLocationIsNotInUse()
    {
        Settings.Default.LogToApplicationDirectory = false;
        Settings.Default.LogFilePath = CustomPath;

        Load();

        Assert.That(_useDefaultLocation.Checked, Is.False);
        Assert.That(_logPath.Text, Is.EqualTo(CustomPath));
    }

    [Test]
    public void LoadingShowsTheDefaultPathWhenTheDefaultLocationIsInUse()
    {
        Settings.Default.LogToApplicationDirectory = true;
        Settings.Default.LogFilePath = CustomPath;

        Load();

        Assert.That(_useDefaultLocation.Checked, Is.True);
        Assert.That(_logPath.Text, Is.EqualTo(Logger.DefaultLogPath));
    }

    /// <summary>
    /// Both transitions used to overwrite the box with the default path, so unticking silently
    /// threw away the path the user had chosen.
    /// </summary>
    [Test]
    public void TickingTheDefaultLocationAndUntickingItAgainRestoresTheCustomPath()
    {
        Settings.Default.LogToApplicationDirectory = false;
        Settings.Default.LogFilePath = CustomPath;
        Load();

        _useDefaultLocation.Checked = true;
        Assert.That(_logPath.Text, Is.EqualTo(Logger.DefaultLogPath));

        _useDefaultLocation.Checked = false;
        Assert.That(_logPath.Text, Is.EqualTo(CustomPath));
    }

    [Test]
    public void SavingWhileTheDefaultLocationIsInUseKeepsTheCustomPath()
    {
        Settings.Default.LogToApplicationDirectory = false;
        Settings.Default.LogFilePath = CustomPath;
        Load();

        _useDefaultLocation.Checked = true;
        Save();

        Assert.That(Settings.Default.LogToApplicationDirectory, Is.True);
        Assert.That(Settings.Default.LogFilePath, Is.EqualTo(CustomPath),
                    "The custom path must survive, or unticking the box later leaves nothing to go back to.");
    }

    /// <summary>
    /// Saving used to apply the path box unconditionally, which pointed the running log
    /// somewhere the next startup would not have chosen.
    /// </summary>
    [Test]
    public void SavingAppliesTheSamePathTheNextStartupWouldChoose()
    {
        Settings.Default.LogToApplicationDirectory = false;
        Settings.Default.LogFilePath = CustomPath;
        Load();

        _useDefaultLocation.Checked = true;
        Save();

        Assert.That(Logger.Instance.LogPath, Is.EqualTo(Logger.DefaultLogPath));
        Assert.That(Logger.Instance.LogPath, Is.EqualTo(Logger.EffectiveLogPath));
    }

    [Test]
    public void SavingACustomPathAppliesIt()
    {
        Settings.Default.LogToApplicationDirectory = true;
        Settings.Default.LogFilePath = string.Empty;
        Load();

        _useDefaultLocation.Checked = false;
        _logPath.Text = CustomPath;
        Save();

        Assert.That(Settings.Default.LogToApplicationDirectory, Is.False);
        Assert.That(Settings.Default.LogFilePath, Is.EqualTo(CustomPath));
        Assert.That(Logger.Instance.LogPath, Is.EqualTo(CustomPath));
    }

    [Test]
    public void LoadingDoesNotOverwriteTheStoredPath()
    {
        Settings.Default.LogToApplicationDirectory = true;
        Settings.Default.LogFilePath = CustomPath;

        Load();

        Assert.That(Settings.Default.LogFilePath, Is.EqualTo(CustomPath));
    }
}
