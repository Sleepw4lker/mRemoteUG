using System.Collections.Specialized;

using mRemoteUG.Connection;
using mRemoteUG.Tree.Root;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.Config.Putty;

public abstract class AbstractPuttySessionsProvider
{
    public virtual RootPuttySessionsNodeInfo RootInfo { get; } = new RootPuttySessionsNodeInfo();
    protected virtual List<PuttySessionInfo> Sessions
    {
        get { return RootInfo.Children.OfType<PuttySessionInfo>().ToList(); }
    }

    #region Public Methods
    public abstract string[] GetSessionNames(bool raw = false);
    /// <returns>
    /// The session, or null when there is none by that name or its PuTTY protocol is not hosted here.
    /// </returns>
    public abstract PuttySessionInfo? GetSession(string sessionName);

    public virtual IEnumerable<PuttySessionInfo> GetSessions()
    {
        string[] sessionNamesFromProvider = GetSessionNames(true);
        foreach (string sessionName in GetSessionNamesToAdd(sessionNamesFromProvider))
        {
            PuttySessionInfo? sessionInfo = GetSession(sessionName);
            AddSession(sessionInfo);
        }
        foreach (PuttySessionInfo session in GetSessionToRemove(sessionNamesFromProvider))
        {
            RemoveSession(session);
        }
        RootInfo.SortRecursive();
        return Sessions;
    }

    private IEnumerable<string> GetSessionNamesToAdd(IEnumerable<string> sessionNamesFromProvider)
    {
        IEnumerable<string> currentlyKnownSessionNames = Sessions.Select(session => session.Name);
        IEnumerable<string> sessionNamesToAdd = sessionNamesFromProvider.Except(currentlyKnownSessionNames);
        return sessionNamesToAdd;
    }

    private IEnumerable<PuttySessionInfo> GetSessionToRemove(IEnumerable<string> sessionNamesFromProvider)
    {
        IEnumerable<string> currentlyKnownSessionNames = Sessions.Select(session => session.Name);
        IEnumerable<string> normalizedSessionNames = sessionNamesFromProvider
            .Select(name => PuttySessionName.Decode(name));
        IEnumerable<string> sessionNamesToRemove = currentlyKnownSessionNames.Except(normalizedSessionNames);
        return Sessions.Where(session => sessionNamesToRemove.Contains(session.Name));
    }

    protected virtual void AddSession(PuttySessionInfo? sessionInfo)
    {
        if (sessionInfo == null
            || string.IsNullOrEmpty(sessionInfo.Name)
            || Sessions.Any(child => child.Name == sessionInfo.Name))
        {
            return;
        }

        RootInfo.AddChild(sessionInfo);
        RaisePuttySessionCollectionChangedEvent(
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, sessionInfo));
    }

    protected virtual void RemoveSession(PuttySessionInfo sessionInfo)
    {
        if (!Sessions.Contains(sessionInfo))
        {
            return;
        }

        RootInfo.RemoveChild(sessionInfo);
        RaisePuttySessionCollectionChangedEvent(
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, sessionInfo));
    }

    public virtual void StartWatcher() { }

    public virtual void StopWatcher() { }
    #endregion

    public event EventHandler<PuttySessionChangedEventArgs>? PuttySessionChanged;
    protected virtual void RaiseSessionChangedEvent(PuttySessionChangedEventArgs args)
    {
        PuttySessionChanged?.Invoke(this, args);
    }

    public event NotifyCollectionChangedEventHandler? PuttySessionsCollectionChanged;
    protected void RaisePuttySessionCollectionChangedEvent(NotifyCollectionChangedEventArgs args)
    {
        PuttySessionsCollectionChanged?.Invoke(this, args);
    }
}
