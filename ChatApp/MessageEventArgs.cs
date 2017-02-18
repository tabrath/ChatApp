using System;

namespace ChatApp
{
    public abstract class MessageEventArgs : EventArgs
    {
        public string Message { get; }
        public DateTime Time { get; }

        protected MessageEventArgs(string message, DateTime? time = null)
        {
            Message = message;
            Time = time ?? DateTime.Now;
        }
    }
}