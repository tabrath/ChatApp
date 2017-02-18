using System;

namespace ChatApp
{
    public class Prompt
    {
        public string Ask(string prefix, Func<string, bool> check) => Ask(prefix, s => s, check);

        public T Ask<T>(string prefix, Func<string, T> convert, Func<T, bool> check, T defaultValue = default(T))
        {
            var result = defaultValue;
            do
            {
                Console.Write($"{prefix}: ");
                try
                {
                    result = convert(Console.ReadLine()?.TrimEnd() ?? string.Empty);
                }
                catch
                {
                    return defaultValue;
                }
            } while (!check(result));
            return result;
        }
    }
}