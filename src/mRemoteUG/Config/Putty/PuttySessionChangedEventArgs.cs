using mRemoteUG.Connection;


namespace mRemoteUG.Config.Putty;

public class PuttySessionChangedEventArgs : EventArgs
{
    /// <summary>The session that changed, or null when the provider cannot tell which one did.</summary>
    public PuttySessionInfo? Session { get; set; }

    public PuttySessionChangedEventArgs(PuttySessionInfo? sessionChanged = null)
    {
        Session = sessionChanged;
    }
}
