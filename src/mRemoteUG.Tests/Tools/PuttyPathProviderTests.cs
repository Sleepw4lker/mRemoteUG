using System.Diagnostics;

using mRemoteUG.Tests.TestHelpers;
using mRemoteUG.Tools;

using NUnit.Framework;

namespace mRemoteUG.Tests.Tools;

public class PuttyPathProviderTests
{
    private const string PathVariable = "PATH";

    // Rebuilt per test in Setup: what the test may change, and a fresh temp file.
    private string _originalCustomPuttyPath = null!;
    private string? _originalPathVariable;
    private string _existingFile = null!;
    private string? _puttyDirectory;

    [SetUp]
    public void Setup()
    {
        _originalCustomPuttyPath = mRemoteUG.Settings.Default.CustomPuttyPath;
        _originalPathVariable = Environment.GetEnvironmentVariable(PathVariable);
        _existingFile = Path.GetTempFileName();
        PuttyPathProvider.InvalidateDiscoveryCache();
    }

    [TearDown]
    public void Teardown()
    {
        mRemoteUG.Settings.Default.CustomPuttyPath = _originalCustomPuttyPath;
        Environment.SetEnvironmentVariable(PathVariable, _originalPathVariable);
        PuttyPathProvider.InvalidateDiscoveryCache();
        if (File.Exists(_existingFile))
        {
            File.Delete(_existingFile);
        }

        if (_puttyDirectory != null)
        {
            try { Directory.Delete(_puttyDirectory, true); } catch (IOException) { }
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void AnUnsetPathIsNotConfigured(string configured)
    {
        mRemoteUG.Settings.Default.CustomPuttyPath = configured;

        Assert.That(PuttyPathProvider.ConfiguredPath, Is.Empty);
    }

    [Test]
    public void TheConfiguredPathIsTrimmed()
    {
        mRemoteUG.Settings.Default.CustomPuttyPath = $"  {_existingFile}  ";

        Assert.That(PuttyPathProvider.ConfiguredPath, Is.EqualTo(_existingFile));
    }

    [Test]
    public void AnExistingConfiguredFileIsUsable()
    {
        mRemoteUG.Settings.Default.CustomPuttyPath = _existingFile;

        Assert.Multiple(() =>
        {
            Assert.That(PuttyPathProvider.IsUsable, Is.True);
            Assert.That(PuttyPathProvider.GetUnusableReason(), Is.Null);
            Assert.That(PuttyPathProvider.ResolvedPath, Is.EqualTo(_existingFile));
        });
    }

    [Test]
    public void AMissingConfiguredFileReportsWhyItCannotBeUsed()
    {
        string missing = Path.Combine(Path.GetTempPath(), "definitely-not-here-putty.exe");
        mRemoteUG.Settings.Default.CustomPuttyPath = missing;

        Assert.Multiple(() =>
        {
            Assert.That(PuttyPathProvider.IsUsable, Is.False);
            Assert.That(PuttyPathProvider.GetUnusableReason(), Does.Contain(missing));
        });
    }

    [Test]
    public void AMalformedPathIsRejectedRatherThanThrowing()
    {
        // Path.GetDirectoryName throws on invalid characters; callers must not see that.
        mRemoteUG.Settings.Default.CustomPuttyPath = "C:\\|not|a|path";

        Assert.Multiple(() =>
        {
            Assert.That(PuttyPathProvider.IsUsable, Is.False);
            Assert.That(PuttyPathProvider.ResolvedDirectory, Is.Null);
        });
    }

    [Test]
    public void ResolvedDirectoryIsTheFolderHoldingTheExecutable()
    {
        mRemoteUG.Settings.Default.CustomPuttyPath = _existingFile;

        Assert.That(PuttyPathProvider.ResolvedDirectory, Is.EqualTo(Path.GetDirectoryName(_existingFile)));
    }

    [Test]
    public void ResolvedDirectoryIsNullWhenNoPuttyIsAvailable()
    {
        mRemoteUG.Settings.Default.CustomPuttyPath = "";

        // Discovery may legitimately find a PuTTY on this machine; only assert the
        // contract that a directory is returned exactly when a path was resolved.
        string resolved = PuttyPathProvider.ResolvedPath;
        if (resolved.Length == 0)
        {
            Assert.That(PuttyPathProvider.ResolvedDirectory, Is.Null);
        }
        else
        {
            Assert.That(PuttyPathProvider.ResolvedDirectory, Is.Not.Null);
        }
    }

    [Test]
    public void WithoutAConfiguredPathAPuttyOnPathIsUsed()
    {
        AssumeNoPuttyBesideTheApplication();
        mRemoteUG.Settings.Default.CustomPuttyPath = "";
        string putty = PutPuttyOnPath();

        Assert.Multiple(() =>
        {
            Assert.That(PuttyPathProvider.ResolvedPath, Is.EqualTo(putty));
            Assert.That(PuttyPathProvider.IsUsable, Is.True);
            Assert.That(PuttyPathProvider.GetUnusableReason(), Is.Null);
        });
    }

    [Test]
    public void WithNothingConfiguredAndNothingFoundTheReasonIsThatNothingIsConfigured()
    {
        AssumeNoPuttyBesideTheApplication();
        mRemoteUG.Settings.Default.CustomPuttyPath = "";
        Environment.SetEnvironmentVariable(PathVariable, null);

        Assert.Multiple(() =>
        {
            Assert.That(PuttyPathProvider.ResolvedPath, Is.Empty);
            Assert.That(PuttyPathProvider.GetUnusableReason(), Is.EqualTo(Language.strPuttyPathNotConfigured));
        });
    }

    /// <summary>
    /// Discovery costs two File.Exists calls per PATH entry, so it runs once until the options
    /// page asks for it again.
    /// </summary>
    [Test]
    public void DiscoveryIsNotRepeatedUntilItsCacheIsInvalidated()
    {
        AssumeNoPuttyBesideTheApplication();
        mRemoteUG.Settings.Default.CustomPuttyPath = "";
        string putty = PutPuttyOnPath();
        Assert.That(PuttyPathProvider.ResolvedPath, Is.EqualTo(putty));

        Environment.SetEnvironmentVariable(PathVariable, null);
        string cached = PuttyPathProvider.ResolvedPath;
        PuttyPathProvider.InvalidateDiscoveryCache();

        Assert.Multiple(() =>
        {
            Assert.That(cached, Is.EqualTo(putty), "Discovery ran again without being asked to.");
            Assert.That(PuttyPathProvider.ResolvedPath, Is.Empty, "Invalidating the cache did not probe again.");
        });
    }

    /// <summary>
    /// A launcher shim starts the real PuTTY in another process and exits, so connecting through
    /// one can never find a window, and the PuTTY it started stays behind, logged in, after the
    /// connect fails (#250). It is refused before anything is started.
    /// </summary>
    [Test]
    public void AConfiguredLauncherShimIsRefusedAndNamed()
    {
        string shim = PutShimInFreshDirectory();
        mRemoteUG.Settings.Default.CustomPuttyPath = shim;

        Assert.Multiple(() =>
        {
            Assert.That(PuttyPathProvider.IsLauncherShim(shim), Is.True);
            Assert.That(PuttyPathProvider.GetUnusableReason(), Does.Contain(shim));
        });
    }

    [Test]
    public void ALauncherShimFoundOnPathIsRefusedAndNamed()
    {
        AssumeNoPuttyBesideTheApplication();
        mRemoteUG.Settings.Default.CustomPuttyPath = "";
        string shim = PutShimInFreshDirectory();
        Environment.SetEnvironmentVariable(PathVariable, _puttyDirectory);
        PuttyPathProvider.InvalidateDiscoveryCache();

        Assert.Multiple(() =>
        {
            Assert.That(PuttyPathProvider.ResolvedPath, Is.EqualTo(shim));
            Assert.That(PuttyPathProvider.GetUnusableReason(), Does.Contain(shim));
        });
    }

    /// <summary>The check reads the version resource; an ordinary executable must still pass it.</summary>
    [Test]
    public void AnExecutableThatIsNotAShimIsNotTakenForOne()
    {
        _puttyDirectory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_puttyDirectory);
        string putty = Path.Combine(_puttyDirectory, "putty.exe");
        LauncherShimStub.Write(putty, "SSH, Telnet, Rlogin, and SUPDUP client");
        mRemoteUG.Settings.Default.CustomPuttyPath = putty;

        Assert.Multiple(() =>
        {
            Assert.That(PuttyPathProvider.IsLauncherShim(putty), Is.False);
            Assert.That(PuttyPathProvider.GetUnusableReason(), Is.Null);
        });
    }

    [Test]
    public void AFileWithoutAVersionResourceIsNotAShim()
    {
        Assert.That(PuttyPathProvider.IsLauncherShim(_existingFile), Is.False);
    }

    [Test]
    public void ABareFileNameHasNoDirectory()
    {
        mRemoteUG.Settings.Default.CustomPuttyPath = "putty.exe";

        Assert.That(PuttyPathProvider.ResolvedDirectory, Is.Null);
    }

    /// <summary>
    /// An invalid character counts wherever it is, the very first position included, and in the
    /// file name as well as in the path.
    /// </summary>
    [TestCase(@"|C:\Tools\putty.exe")]
    [TestCase(@"C:\Tools\?putty.exe")]
    public void AnInvalidCharacterAtTheStartMakesThePathMalformed(string configured)
    {
        mRemoteUG.Settings.Default.CustomPuttyPath = configured;

        Assert.That(PuttyPathProvider.ResolvedDirectory, Is.Null);
    }

    /// <summary>
    /// A PATH holding only a fresh directory with an empty putty.exe in it, so discovery has
    /// exactly one place to find PuTTY outside the application's own directory.
    /// </summary>
    private string PutPuttyOnPath()
    {
        _puttyDirectory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_puttyDirectory);
        string putty = Path.Combine(_puttyDirectory, "putty.exe");
        File.WriteAllBytes(putty, Array.Empty<byte>());
        Environment.SetEnvironmentVariable(PathVariable, _puttyDirectory);
        PuttyPathProvider.InvalidateDiscoveryCache();
        return putty;
    }

    /// <summary>A putty.exe in a fresh directory that identifies itself as a Chocolatey shim.</summary>
    private string PutShimInFreshDirectory()
    {
        _puttyDirectory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_puttyDirectory);
        string shim = Path.Combine(_puttyDirectory, "putty.exe");
        LauncherShimStub.Write(shim, LauncherShimStub.ChocolateyShimDescription);
        // Guards the fixture itself: without this, a stub that failed to identify as a shim
        // would make the refusal tests fail for the wrong reason.
        Assert.That(FileVersionInfo.GetVersionInfo(shim).FileDescription,
                    Is.EqualTo(LauncherShimStub.ChocolateyShimDescription));
        return shim;
    }

    /// <summary>Discovery looks beside the application before PATH; these tests need it empty.</summary>
    private static void AssumeNoPuttyBesideTheApplication()
    {
        string home = mRemoteUG.App.Info.GeneralAppInfo.HomePath ?? string.Empty;
        Assume.That(File.Exists(Path.Combine(home, "putty.exe")) || File.Exists(Path.Combine(home, "PuTTYNG.exe")),
                    Is.False, "A PuTTY sits beside the test host.");
    }
}
