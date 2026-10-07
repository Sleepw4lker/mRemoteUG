#nullable enable
using System;

namespace mRemoteUG.Messages
{
	public class Message : IMessage
	{
	    public MessageClass Class { get; set; }
	    public string Text { get; set; }
	    public DateTime Date { get; set; }
	    public bool OnlyLog { get; set; }
	    public Exception? Exception { get; set; }

        public Message(MessageClass messageClass, string messageText, bool onlyLog = false,
                       Exception? exception = null)
        {
            Class = messageClass;
            Text = messageText;
            Date = DateTime.Now;
            OnlyLog = onlyLog;
            Exception = exception;
        }
	}
}