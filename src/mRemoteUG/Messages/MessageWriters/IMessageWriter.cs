#nullable enable
namespace mRemoteUG.Messages.MessageWriters
{
    public interface IMessageWriter
    {
        void Write(IMessage message);
    }
}