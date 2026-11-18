using System.Globalization;
using System.Text;

using mRemoteUG.Config.DataProviders;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.DataProviders;

public class FileBackupLookupTests
{
    private const string DefaultFormat = "{0}.{1:yyyyMMdd-HHmmssffff}.backup";

    private const string FileName = "confCons.xml";

    private static readonly CompositeFormat s_defaultFormat = CompositeFormat.Parse(DefaultFormat);

    // Rebuilt for every test in Setup: each test gets a directory of its own and the backup
    // settings it expects, which Teardown puts back.
    private string _directory = null!;
    private string _connectionFile = null!;
    private DateTime _now;
    private string _originalFormat = null!;
    private bool _originalEnabled;
    private int _originalKeepCount;

    [SetUp]
    public void Setup()
    {
        _directory = FileTestHelpers.GetTestSpecificTempDirectory();
        Directory.CreateDirectory(_directory);
        _connectionFile = Path.Combine(_directory, FileName);
        _now = DateTime.Now;
        _originalFormat = Settings.Default.BackupFileNameFormat;
        _originalEnabled = Settings.Default.BackupFileEnabled;
        _originalKeepCount = Settings.Default.BackupFileKeepCount;
        Settings.Default.BackupFileNameFormat = DefaultFormat;
        Settings.Default.BackupFileEnabled = true;
        Settings.Default.BackupFileKeepCount = 10;
    }

    [TearDown]
    public void Teardown()
    {
        Settings.Default.BackupFileNameFormat = _originalFormat;
        Settings.Default.BackupFileEnabled = _originalEnabled;
        Settings.Default.BackupFileKeepCount = _originalKeepCount;
        FileTestHelpers.DeleteDirectory(_directory);
    }

    /// <summary>
    /// The newest goes by the time in the name, not by last-write time, which here runs the
    /// other way, and comes with that time.
    /// </summary>
    [Test]
    public void NamesTheNewestOfSeveralBackups()
    {
        Backup(_now.AddDays(-3), lastWrite: _now);
        Backup(_now.AddDays(-2), lastWrite: _now.AddDays(-10));
        DateTime newestTaken = TruncatedToTheName(_now.AddDays(-1));
        string newest = Backup(newestTaken, lastWrite: _now.AddDays(-20));

        FileBackup? found = FileBackupLookup.Newest(_connectionFile);

        Assert.That(found, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(found!.Path, Is.EqualTo(newest));
            Assert.That(found.TakenUtc, Is.EqualTo(newestTaken.ToUniversalTime()));
        });
    }

    [Test]
    public void ListsEveryBackupNewestFirst()
    {
        string oldest = Backup(_now.AddDays(-3), lastWrite: _now);
        string middle = Backup(_now.AddDays(-2), lastWrite: _now);
        string newest = Backup(_now.AddDays(-1), lastWrite: _now);
        File.WriteAllText(_connectionFile, "");
        File.WriteAllText(Path.Combine(_directory, "other.xml.20260101-0000000000.backup"), "");

        IReadOnlyList<FileBackup> found = FileBackupLookup.NewestFirst(_connectionFile);

        Assert.That(found.Select(backup => backup.Path), Is.EqualTo(new[] { newest, middle, oldest }));
    }

    [Test]
    public void ReportsNoneWhenThereAreNone()
    {
        File.WriteAllText(_connectionFile, "");

        Assert.Multiple(() =>
        {
            Assert.That(FileBackupLookup.Newest(_connectionFile), Is.Null);
            Assert.That(FileBackupLookup.NewestFirst(_connectionFile), Is.Empty);
        });
    }

    [Test]
    public void ReportsNoneForABareFileName()
    {
        Assert.That(FileBackupLookup.NewestFirst(FileName), Is.Empty);
    }

    /// <summary>
    /// With backups switched off, either way the options offer, the backups still on disk are
    /// not offered as the newest: they may be far older than the file.
    /// </summary>
    [TestCase(false, 10)]
    [TestCase(true, 0)]
    public void RespectsBackupsBeingSwitchedOff(bool enabled, int keepCount)
    {
        Backup(_now.AddDays(-1), lastWrite: _now);
        Settings.Default.BackupFileEnabled = enabled;
        Settings.Default.BackupFileKeepCount = keepCount;

        Assert.Multiple(() =>
        {
            Assert.That(FileBackupLookup.AreSwitchedOff, Is.True);
            Assert.That(FileBackupLookup.Newest(_connectionFile), Is.Null);
        });
    }

    [Test]
    public void AreOnWhenEnabledWithAKeepCount()
    {
        Assert.That(FileBackupLookup.AreSwitchedOff, Is.False);
    }

    private string Backup(DateTime taken, DateTime lastWrite)
    {
        string path = Path.Combine(
            _directory,
            string.Format(CultureInfo.InvariantCulture, s_defaultFormat, FileName, taken));
        File.WriteAllText(path, "");
        File.SetLastWriteTimeUtc(path, lastWrite.ToUniversalTime());
        return path;
    }

    /// <summary>The time as the default name records it, to the ten-thousandth of a second.</summary>
    private static DateTime TruncatedToTheName(DateTime time)
    {
        return new DateTime(time.Ticks - (time.Ticks % (TimeSpan.TicksPerMillisecond / 10)), time.Kind);
    }
}
