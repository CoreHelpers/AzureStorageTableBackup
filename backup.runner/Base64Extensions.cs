using System;
using System.Text;

namespace backup.runner
{
    public static class Base64Extensions
    {
        public static string ToBase64(this string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(bytes);
        }

        public static string FromBase64(this string input)
        {
            var bytes = Convert.FromBase64String(input);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}