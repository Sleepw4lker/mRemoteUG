using System;
using mRemoteUG.Connection;


namespace mRemoteUG.Config.Putty
{
    public class PuttySessionChangedEventArgs : EventArgs
    {
        public PuttySessionInfo Session { get; set; }

        public PuttySessionChangedEventArgs(PuttySessionInfo sessionChanged = null)
        {
            Session = sessionChanged;
        }
    }
}