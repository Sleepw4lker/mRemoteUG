using System.Globalization;

namespace mRemoteUG.Config.DataProviders;

/// <summary>
/// Finds the backups FileBackupCreator wrote of a connection file: the files next to it named
/// by the BackupFileNameFormat setting, each with the time it was taken.
/// </summary>
internal static class FileBackupLookup
{
    // A timestamp read from a backup name counts only if it falls in this window. Names written
    // before #99 used the current culture's calendar and still parse as Gregorian dates, but wrong
    // ones: th-TH wrote the Buddhist year (2569 in 2026), which lands centuries in the future, and
    // ar-SA and fa-IR wrote the Hijri and Persian years (1448, 1405), which land centuries in the
    // past. No backup of this application predates 2000, and neither of those calendars reaches the
    // year 2000 before the 26th century. The day of slack past now covers a clock or time zone
    // change, since the name holds local time.
    private static readonly DateTime s_earliestPlausibleBackup = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan s_futureTolerance = TimeSpan.FromDays(1);

    /// <summary>True when the options say no backups are taken, either way they can say it.</summary>
    public static bool AreSwitchedOff =>
        !mRemoteUG.Settings.Default.BackupFileEnabled || mRemoteUG.Settings.Default.BackupFileKeepCount == 0;

    /// <summary>
    /// Every backup of <paramref name="filePath"/> on disk, newest first; backups taken at the
    /// same time come in descending ordinal order of their names. Empty when the path has no
    /// folder. Lists backups even when they are switched off.
    /// </summary>
    public static IReadOnlyList<FileBackup> NewestFirst(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string? directoryName = Path.GetDirectoryName(filePath);

        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(directoryName))
        {
            return [];
        }

        string backupFileNameFormat = mRemoteUG.Settings.Default.BackupFileNameFormat;
        string searchPattern = string.Format(CultureInfo.InvariantCulture, backupFileNameFormat, fileName, "*");
        string[] files = Directory.GetFiles(directoryName, searchPattern);

        var nameTimestamp = BackupNameTimestamp.From(backupFileNameFormat, fileName);
        DateTime latestPlausibleBackup = DateTime.UtcNow + s_futureTolerance;

        return files
            .Select(file => new FileBackup(file, BackupTimeUtc(file, nameTimestamp, latestPlausibleBackup)))
            .OrderByDescending(backup => backup.TakenUtc)
            .ThenByDescending(backup => backup.Path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The newest backup of <paramref name="filePath"/>, or null when there is none or backups
    /// are switched off: a backup left from before they were switched off may be far older than
    /// the file.
    /// </summary>
    public static FileBackup? Newest(string filePath)
    {
        if (AreSwitchedOff)
        {
            return null;
        }

        IReadOnlyList<FileBackup> backups = NewestFirst(filePath);
        return backups.Count == 0 ? null : backups[0];
    }

    // The time the name records, if it is plausible; otherwise the file's last-write time,
    // which File.Copy carries over from the connection file the backup was taken of.
    private static DateTime BackupTimeUtc(
        string file,
        BackupNameTimestamp nameTimestamp,
        DateTime latestPlausibleBackup)
    {
        if (nameTimestamp.TryParse(Path.GetFileName(file), out DateTime local))
        {
            DateTime utc = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
            if (utc >= s_earliestPlausibleBackup && utc <= latestPlausibleBackup)
            {
                return utc;
            }
        }

        return File.GetLastWriteTimeUtc(file);
    }

    // Reads the timestamp back out of a backup name written with the user's BackupFileNameFormat.
    // Rather than parse the composite format string by hand, it formats it once with a formatter
    // that records the date's format specifier and writes a marker where the date goes, which
    // leaves the literal text before and after the date.
    private sealed class BackupNameTimestamp
    {
        private readonly string _prefix;
        private readonly string _suffix;
        private readonly string? _dateFormat;
        private readonly bool _usable;

        private BackupNameTimestamp(string prefix, string suffix, string? dateFormat, bool usable)
        {
            _prefix = prefix;
            _suffix = suffix;
            _dateFormat = dateFormat;
            _usable = usable;
        }

        public static BackupNameTimestamp From(string backupFileNameFormat, string fileName)
        {
            var recorder = new DateFormatRecorder();
            string formatted = string.Format(recorder, backupFileNameFormat, fileName, DateTime.MinValue);
            string[] parts = formatted.Split(DateFormatRecorder.Marker);
            if (parts.Length != 2 || recorder.Calls != 1)
            {
                // No date in the format, or more than one: every backup goes by last-write time.
                return new BackupNameTimestamp(string.Empty, string.Empty, null, usable: false);
            }

            return new BackupNameTimestamp(parts[0], parts[1], recorder.DateFormat, usable: true);
        }

        public bool TryParse(string backupFileName, out DateTime timestamp)
        {
            timestamp = default;
            if (!_usable
                || backupFileName.Length < _prefix.Length + _suffix.Length
                || !backupFileName.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase)
                || !backupFileName.EndsWith(_suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string date = backupFileName.Substring(
                _prefix.Length,
                backupFileName.Length - _prefix.Length - _suffix.Length);
            return string.IsNullOrEmpty(_dateFormat)
                ? DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp)
                : DateTime.TryParseExact(
                    date,
                    _dateFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out timestamp);
        }
    }

    private sealed class DateFormatRecorder : IFormatProvider, ICustomFormatter
    {
        // NUL cannot occur in a Windows file name, so it cannot collide with the format's text.
        public const char Marker = '\0';

        public string? DateFormat { get; private set; }

        public int Calls { get; private set; }

        public object? GetFormat(Type? formatType)
        {
            return formatType == typeof(ICustomFormatter) ? this : null;
        }

        public string Format(string? format, object? arg, IFormatProvider? formatProvider)
        {
            if (arg is DateTime)
            {
                DateFormat = format;
                Calls++;
                return Marker.ToString();
            }

            return arg is IFormattable formattable
                ? formattable.ToString(format, CultureInfo.InvariantCulture)
                : arg?.ToString() ?? string.Empty;
        }
    }
}
