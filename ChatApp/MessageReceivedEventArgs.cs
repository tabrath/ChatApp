using System;

namespace ChatApp
{
    public class MessageReceivedEventArgs : MessageEventArgs
    {
        public string Nickname { get; }

        public MessageReceivedEventArgs(string nickname, string message, DateTime? time = null)
            : base(message, time)
        {
            Nickname = nickname;
        }
    }
}