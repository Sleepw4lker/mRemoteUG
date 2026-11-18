namespace mRemoteUG.Config.DataProviders;

/// <summary>A backup of a connection file, and when it was taken.</summary>
internal sealed record FileBackup(string Path, DateTime TakenUtc);
