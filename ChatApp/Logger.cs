using System;

namespace ChatApp
{
    public class Logger
    {
        public void Info(string message) => Console.WriteLine($"[Info] {message}");
        public void Warning(string message) => Console.WriteLine($"[Warning] {message}");
        public void Error(string message) => Console.WriteLine($"[Error] {message}");
    }
}