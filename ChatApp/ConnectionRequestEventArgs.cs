using System;
using System.Net;

namespace ChatApp
{
    public class ConnectionRequestEventArgs : EventArgs
    {
        public string Nickname { get; }
        public EndPoint EndPoint { get; }
        public bool Accept { get; set; } = false;

        public ConnectionRequestEventArgs(string nickname, EndPoint endPoint)
        {
            Nickname = nickname;
            EndPoint = endPoint;
        }
    }
}