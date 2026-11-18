using System.Globalization;

using mRemoteUG.Config.DataProviders;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.DataProviders;

public class FileBackupPrunerTests
{
    private const string DefaultFormat = "{0}.{1:yyyyMMdd-HHmmssffff}.backup";

    private const string FileName = "confCons.xml";

    // Rebuilt for every test in Setup: each test gets a directory of its own.
    private string _directory = null!;
    private string _connectionFile = null!;
    private DateTime _now;

    [SetUp]
    public void Setup()
    {
        _directory = FileTestHelpers.GetTestSpecificTempDirectory();
        Directory.CreateDirectory(_directory);
        _connectionFile = Path.Combine(_directory, FileName);
        _now = DateTime.Now;
    }

    [TearDown]
    public void Teardown()
    {
        FileTestHelpers.DeleteDirectory(_directory);
    }

    [Test]
    public void KeepsTheNewestByTheTimeInTheName()
    {
        // Last-write times run opposite to the names, so only reading the name gets this right.
        string oldest = Backup(DefaultFormat, CultureInfo.InvariantCulture, _now.AddDays(-3), lastWrite: _now);
        string older = Backup(DefaultFormat, CultureInfo.InvariantCulture, _now.AddDays(-2),
                              lastWrite: _now.AddDays(-10));
        string newer = Backup(DefaultFormat, CultureInfo.InvariantCulture, _now.AddDays(-1),
                              lastWrite: _now.AddDays(-20));

        FileBackupPruner.PruneBackupFiles(_connectionFile, 2);

        Assert.That(RemainingBackups(), Is.EquivalentTo(new[] { older, newer }));
        Assert.That(File.Exists(oldest), Is.False);
    }

    // Before #99 the backup name took the current culture's calendar: th-TH wrote 2569, ar-SA
    // 1448 and fa-IR 1405 for 2026. Those names parse as Gregorian dates centuries away, so the
    // pruner ages them by last-write time instead. Run under the old culture, which must not
    // change how names are read either.
    [TestCase("th-TH")]
    [TestCase("ar-SA")]
    [TestCase("fa-IR")]
    public void OldCultureNamedBackupsNeitherCrowdOutNewOnesNorStayForever(string cultureName)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var oldCulture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = oldCulture;
            string oldest = Backup(DefaultFormat, oldCulture, _now.AddDays(-30), lastWrite: _now.AddDays(-31));
            string middle = Backup(DefaultFormat, oldCulture, _now.AddDays(-20), lastWrite: _now.AddDays(-21));
            string newestOld = Backup(DefaultFormat, oldCulture, _now.AddDays(-10), lastWrite: _now.AddDays(-11));
            Assert.That(Path.GetFileName(newestOld),
                        Does.Not.StartWith(FileName + "." + _now.Year.ToString(CultureInfo.InvariantCulture)),
                        "the premise: this culture writes a non-Gregorian year");

            string firstNew = Backup(DefaultFormat, CultureInfo.InvariantCulture, _now.AddHours(-2),
                                     lastWrite: _now.AddDays(-12));
            string secondNew = Backup(DefaultFormat, CultureInfo.InvariantCulture, _now.AddHours(-1),
                                      lastWrite: _now.AddHours(-2));

            FileBackupPruner.PruneBackupFiles(_connectionFile, 3);
            Assert.That(RemainingBackups(), Is.EquivalentTo(new[] { newestOld, firstNew, secondNew }),
                        "the two new backups and the newest old one survive; the two oldest go");

            string thirdNew = Backup(DefaultFormat, CultureInfo.InvariantCulture, _now, lastWrite: _now.AddHours(-1));

            FileBackupPruner.PruneBackupFiles(_connectionFile, 3);
            Assert.That(RemainingBackups(), Is.EquivalentTo(new[] { firstNew, secondNew, thirdNew }),
                        "once there are enough new backups, the last old one goes too");
            Assert.That(new[] { oldest, middle, newestOld }.Any(File.Exists), Is.False);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Test]
    public void ACustomFormatIsReadToo()
    {
        // Day first, so sorting the names would keep the wrong ones.
        const string customFormat = "{0}-backup-{1:dd-MM-yyyy_HH-mm-ss}.bak";
        string originalFormat = Settings.Default.BackupFileNameFormat;
        try
        {
            Settings.Default.BackupFileNameFormat = customFormat;
            string oldest = Backup(customFormat, CultureInfo.InvariantCulture, new DateTime(2026, 1, 31, 12, 0, 0),
                                   lastWrite: _now);
            string older = Backup(customFormat, CultureInfo.InvariantCulture, new DateTime(2026, 2, 1, 12, 0, 0),
                                  lastWrite: _now.AddDays(-10));
            string newer = Backup(customFormat, CultureInfo.InvariantCulture, new DateTime(2026, 2, 2, 12, 0, 0),
                                  lastWrite: _now.AddDays(-20));

            FileBackupPruner.PruneBackupFiles(_connectionFile, 2);

            Assert.That(RemainingBackups(), Is.EquivalentTo(new[] { older, newer }));
            Assert.That(File.Exists(oldest), Is.False);
        }
        finally
        {
            Settings.Default.BackupFileNameFormat = originalFormat;
        }
    }

    [Test]
    public void ANameThatDoesNotParseGoesByLastWriteTime()
    {
        string unreadable = Path.Combine(_directory, FileName + ".copied-by-hand.backup");
        File.WriteAllText(unreadable, "");
        File.SetLastWriteTimeUtc(unreadable, _now.AddDays(-5).ToUniversalTime());
        string older = Backup(DefaultFormat, CultureInfo.InvariantCulture, _now.AddDays(-7), lastWrite: _now);
        string newer = Backup(DefaultFormat, CultureInfo.InvariantCulture, _now.AddDays(-1), lastWrite: _now);

        FileBackupPruner.PruneBackupFiles(_connectionFile, 2);

        Assert.That(RemainingBackups(), Is.EquivalentTo(new[] { unreadable, newer }));
        Assert.That(File.Exists(older), Is.False);
    }

    [Test]
    public void NothingIsDeletedWithinTheKeepCount()
    {
        string first = Backup(DefaultFormat, CultureInfo.InvariantCulture, _now.AddDays(-2), lastWrite: _now);
        string second = Backup(DefaultFormat, CultureInfo.InvariantCulture, _now.AddDays(-1), lastWrite: _now);

        FileBackupPruner.PruneBackupFiles(_connectionFile, 2);

        Assert.That(RemainingBackups(), Is.EquivalentTo(new[] { first, second }));
    }

    /// <summary>A path without a directory has no backups to look for.</summary>
    [Test]
    public void ABareFileNameIsIgnored()
    {
        Assert.DoesNotThrow(() => FileBackupPruner.PruneBackupFiles(FileName, 0));
    }

    /// <summary>Two backups taken at the same time keep the ordinally greater name.</summary>
    [Test]
    public void ATieInTimeKeepsTheGreaterName()
    {
        DateTime lastWrite = _now.AddDays(-1).ToUniversalTime();
        string lesser = Path.Combine(_directory, FileName + ".a-copied-by-hand.backup");
        string greater = Path.Combine(_directory, FileName + ".b-copied-by-hand.backup");
        foreach (string backup in new[] { lesser, greater })
        {
            File.WriteAllText(backup, "");
            File.SetLastWriteTimeUtc(backup, lastWrite);
        }

        FileBackupPruner.PruneBackupFiles(_connectionFile, 1);

        Assert.That(RemainingBackups(), Is.EqualTo(new[] { greater }));
    }

    /// <summary>
    /// A name recording exactly the start of 2000 is still read, so the window includes its
    /// lower bound: the backup goes by that time rather than by its newer last-write time.
    /// </summary>
    [Test]
    public void ANameAtTheStartOfTheWindowIsRead()
    {
        DateTime windowStart = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();
        string atWindowStart = Backup(DefaultFormat, CultureInfo.InvariantCulture, windowStart, lastWrite: _now);
        string dayAfter = Backup(DefaultFormat, CultureInfo.InvariantCulture, windowStart.AddDays(1),
                                 lastWrite: _now.AddDays(-1));

        FileBackupPruner.PruneBackupFiles(_connectionFile, 1);

        Assert.That(RemainingBackups(), Is.EqualTo(new[] { dayAfter }));
        Assert.That(File.Exists(atWindowStart), Is.False);
    }

    private string Backup(string format, CultureInfo culture, DateTime taken, DateTime lastWrite)
    {
        string path = Path.Combine(_directory, string.Format(culture, format, FileName, taken));
        File.WriteAllText(path, "");
        File.SetLastWriteTimeUtc(path, lastWrite.ToUniversalTime());
        return path;
    }

    private string[] RemainingBackups()
    {
        return Directory.GetFiles(_directory).Where(file => file != _connectionFile).ToArray();
    }
}
