using Microsoft.Win32;

using mRemoteUG.Config.Putty;

namespace mRemoteUG.Tests.TestHelpers;

/// <summary>
/// A throwaway copy of PuTTY's registry layout under <c>HKCU\Software\mRemoteUG.Tests\&lt;guid&gt;</c>,
/// so the PuTTY session tests never read or write the user's real
/// <c>HKCU\Software\SimonTatham\PuTTY</c> key.
/// </summary>
/// <remarks>
/// The sessions live at <c>&lt;guid&gt;\SimonTatham\PuTTY\Sessions</c>, so the provider's watcher
/// fallbacks (the Sessions key and its two parents) all stay inside the scratch key too.
/// Disposing deletes the whole scratch key, whatever the test left behind.
/// </remarks>
internal sealed class PuttyRegistryScratch : IDisposable
{
    private const string TestRootPath = @"Software\mRemoteUG.Tests";

    public PuttyRegistryScratch()
    {
        RootPath = $@"{TestRootPath}\{Guid.NewGuid():N}";
        SimonTathamKeyPath = $@"{RootPath}\SimonTatham";
        PuttyKeyPath = $@"{SimonTathamKeyPath}\PuTTY";
        SessionsKeyPath = $@"{PuttyKeyPath}\Sessions";
    }

    public string RootPath { get; }

    public string SimonTathamKeyPath { get; }

    public string PuttyKeyPath { get; }

    public string SessionsKeyPath { get; }

    /// <summary>A registry provider reading from this scratch key instead of PuTTY's.</summary>
    public PuttySessionsRegistryProvider CreateProvider()
    {
        return new PuttySessionsRegistryProvider { SessionsKeyPath = SessionsKeyPath };
    }

    public static void CreateKey(string path)
    {
        Registry.CurrentUser.CreateSubKey(path)?.Dispose();
    }

    public void CreateSessionsKey()
    {
        CreateKey(SessionsKeyPath);
    }

    /// <summary>
    /// Writes a session the way PuTTY does: one subkey per session, named as stored (so
    /// URL-encoded), strings as REG_SZ and numbers as REG_DWORD.
    /// </summary>
    public void AddSession(string storedName, params (string Name, object Value)[] values)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey($@"{SessionsKeyPath}\{storedName}");
        foreach ((string name, object value) in values)
        {
            key.SetValue(name, value);
        }
    }

    public void DeleteSession(string storedName)
    {
        Registry.CurrentUser.DeleteSubKeyTree($@"{SessionsKeyPath}\{storedName}");
    }

    public void Dispose()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(RootPath, throwOnMissingSubKey: false);
        }
        catch (ArgumentException)
        {
            // Nothing was created.
        }
    }
}
