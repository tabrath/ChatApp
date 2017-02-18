using System;

namespace ChatApp
{
    public class MessageSentEventArgs : MessageEventArgs
    {
        public MessageSentEventArgs(string message, DateTime? time = null)
            : base(message, time)
        {
        }
    }
}