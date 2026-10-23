using System;
using System.Text;

namespace AviConverter
{
    internal static class CommandLine
    {
        /// <summary>Quotes one argument for a Windows command line.</summary>
        public static string Quote(string value)
        {
            if (value == null) { return "\"\""; }
            if (value.Length == 0) { return "\"\""; }
            if (value.IndexOfAny(new char[] { ' ', '\t', '\n', '\v', '"' }) < 0) { return value; }

            StringBuilder sb = new StringBuilder();
            sb.Append('"');
            int backslashes = 0;

            foreach (char c in value)
            {
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                }
                else
                {
                    if (backslashes > 0)
                    {
                        sb.Append('\\', backslashes);
                        backslashes = 0;
                    }
                    sb.Append(c);
                }
            }

            if (backslashes > 0) { sb.Append('\\', backslashes * 2); }
            sb.Append('"');
            return sb.ToString();
        }

        public static string Join(System.Collections.Generic.IEnumerable<string> args)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string a in args)
            {
                if (sb.Length > 0) { sb.Append(' '); }
                sb.Append(Quote(a));
            }
            return sb.ToString();
        }
    }
}
