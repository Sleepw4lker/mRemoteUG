using System.Diagnostics;

namespace mRemoteUG.Messages.MessageWriters;

public class DebugConsoleMessageWriter : IMessageWriter
{
    public void Write(IMessage message)
    {
        string textToPrint = $"{message.Class}: {message.Text}";
        Debug.Print(textToPrint);
    }
}
