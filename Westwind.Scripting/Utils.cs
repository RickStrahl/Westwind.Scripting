using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Westwind.Scripting
{
    internal static class Utils
    {        
        internal static string GenerateUniqueId()
        {
            const int stringSize = 8;

            string str = "abcdefghijkmnopqrstuvwxyz1234567890";
            var stringBuilder = new StringBuilder(stringSize);
            int num1 = 0;
            foreach (byte num2 in Guid.NewGuid().ToByteArray())
            {
                stringBuilder.Append(str[(int)num2 % str.Length]);
                ++num1;
                if (num1 >= stringSize)
                    break;
            }
            return stringBuilder.ToString();
        }

        internal static string GetTextWithLineNumbers(string text, string lineFormat = "{0}.  {1}")
        {
            if (string.IsNullOrEmpty(text))
                return text;
            var stringBuilder = new StringBuilder();
            string[] lines = GetLines(text);
            int totalWidth = 2;
            if (lines.Length > 9999)
                totalWidth = 5;
            else if (lines.Length > 999)
                totalWidth = 4;
            else if (lines.Length > 99)
                totalWidth = 3;
            else if (lines.Length < 10)
                totalWidth = 1;
            lineFormat += "\r\n";
            for (int index = 1; index <= lines.Length; ++index)
            {
                string str = index.ToString().PadLeft(totalWidth, ' ');
                stringBuilder.AppendFormat(lineFormat, (object)str, (object)lines[index - 1]);
            }
            return stringBuilder.ToString();
        }

        internal static string[] GetLines(string s, int maxLines = 0)
        {
            if (s == null)
                return (string[])null;
            s = s.Replace("\r\n", "\n");
            if (maxLines < 1)
                return s.Split('\n');
            return s.Split('\n').Take(maxLines).ToArray();
        }


        /// <summary>
        /// Normalizes a file path to the operating system default
        /// slashes.
        /// </summary>
        /// <param name="path"></param>
        public static string NormalizePath(string path)
        {
            //return Path.GetFullPath(path); // this always turns into a full OS path

            if (string.IsNullOrEmpty(path))
                return path;

            char slash = Path.DirectorySeparatorChar;
            path = path.Replace('/', slash).Replace('\\', slash);
            string doubleSlash = string.Concat(slash, slash);
            if (path.StartsWith(doubleSlash))
                return string.Concat(doubleSlash, path.TrimStart(slash).Replace(doubleSlash, slash.ToString()));
            else
                return path.Replace(doubleSlash, slash.ToString());
        }


       
    }
}
