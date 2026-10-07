namespace mRemoteUG.App
{
    /// <summary>
    /// Whether the application is on its way out.
    /// </summary>
    /// <remarks>
    /// Deliberately a type of its own with nothing else in it. The obvious place for this is
    /// <see cref="UI.Forms.FrmMain"/>, but touching any static member of that class runs its type
    /// initializer, which builds the entire main window - and that needs an STA thread. Session
    /// teardown asks this question on every close, including from unit tests where there is no
    /// main window at all, so asking must not be able to create one.
    /// </remarks>
    public static class ApplicationLifecycle
    {
        /// <summary>
        /// True once the main window has begun closing. Nothing is worth blocking the UI thread
        /// for after this point: the process is about to exit and Windows reclaims what is left.
        /// </summary>
        public static bool IsShuttingDown { get; set; }
    }
}
