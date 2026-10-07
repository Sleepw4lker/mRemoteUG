#nullable enable
using System;


namespace mRemoteUG.Security
{
    [Serializable]
    public class EncryptionException : Exception
    {
        public EncryptionException(string message) : base(message)
        {
        }

        public EncryptionException(string message, Exception exception) : base(message, exception)
        {
        }
    }
}
