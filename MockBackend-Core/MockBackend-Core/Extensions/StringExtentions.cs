using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MockBackend_Core.Extensions
{
    public static class StringExtentions
    {
        public static byte[] AsBytes(this string input, Encoding? encoding = null)
        {
            encoding ??= Encoding.ASCII;
            return encoding.GetBytes(input);
        }

        public static string AsString(this byte[] input, int? length = null, Encoding? encoding = null)
        {
            encoding ??= Encoding.ASCII;
            return encoding.GetString(input, 0, length ?? input.Length);
        }
    }
}
