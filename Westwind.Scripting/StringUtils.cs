using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Westwind.Scripting
{

    internal class StringUtils
    {
        /// <summary>
        /// Returns the number or right characters specified
        /// </summary>
        /// <param name="full">full string to work with</param>
        /// <param name="rightCharCount">number of right characters to return</param>
        /// <returns></returns>
        public static string Right(string full, int rightCharCount)
        {
            if (string.IsNullOrEmpty(full) || full.Length < rightCharCount || full.Length - rightCharCount < 0)
                return full;

            return full.Substring(full.Length - rightCharCount);
        }

        /// <summary>
        /// Extracts a string from between a pair of delimiters. Only the first 
        /// instance is found.
        /// </summary>
        /// <param name="source">Input String to work on</param>
        /// <param name="beginDelim">Beginning delimiter</param>
        /// <param name="endDelim">ending delimiter</param>
        /// <param name="caseSensitive">Determines whether the search for delimiters is case sensitive</param>        
        /// <param name="allowMissingEndDelimiter"></param>
        /// <param name="returnDelimiters"></param>
        /// <returns>Extracted string or string.Empty on no match</returns>
        public static string ExtractString(string source,
            string beginDelim,
            string endDelim,
            bool caseSensitive = false,
            bool allowMissingEndDelimiter = false,
            bool returnDelimiters = false)
        {
            int at1, at2;

            if (string.IsNullOrEmpty(source))
                return string.Empty;

            if (caseSensitive)
            {
                at1 = source.IndexOf(beginDelim, StringComparison.CurrentCulture);
                if (at1 == -1)
                    return string.Empty;

                at2 = source.IndexOf(endDelim, at1 + beginDelim.Length, StringComparison.CurrentCulture);
            }
            else
            {
                //string Lower = source.ToLower();
                at1 = source.IndexOf(beginDelim, 0, source.Length, StringComparison.OrdinalIgnoreCase);
                if (at1 == -1)
                    return string.Empty;

                at2 = source.IndexOf(endDelim, at1 + beginDelim.Length, StringComparison.OrdinalIgnoreCase);
            }

            if (allowMissingEndDelimiter && at2 < 0)
            {
                if (!returnDelimiters)
                    return source.Substring(at1 + beginDelim.Length);

                return source.Substring(at1);
            }

            if (at1 > -1 && at2 > 1)
            {
                if (!returnDelimiters)
                    return source.Substring(at1 + beginDelim.Length, at2 - at1 - beginDelim.Length);

                return source.Substring(at1, at2 - at1 + endDelim.Length);
            }

            return string.Empty;
        }

        /// <summary>
        /// Tries to create a phrase string from CamelCase text
        /// into Proper Case text.  Will place spaces before capitalized
        /// letters.
        /// 
        /// Note that this method may not work for round tripping 
        /// ToCamelCase calls, since ToCamelCase strips more characters
        /// than just spaces.
        /// </summary>
        /// <param name="camelCase">Camel Case Text: firstName -> First Name</param>
        /// <returns></returns>
        public static string FromCamelCase(string camelCase)
        {
            if (string.IsNullOrEmpty(camelCase))
                return camelCase;

            StringBuilder sb = new StringBuilder(camelCase.Length + 10);
            bool first = true;
            char lastChar = '\0';

            foreach (char ch in camelCase)
            {
                if (!first &&
                    lastChar != ' ' && !char.IsSymbol(lastChar) && !char.IsPunctuation(lastChar) &&
                    ((char.IsUpper(ch) && !char.IsUpper(lastChar)) ||
                     char.IsDigit(ch) && !char.IsDigit(lastChar)))
                    sb.Append(' ');

                sb.Append(ch);
                first = false;
                lastChar = ch;
            }

            return sb.ToString(); ;
        }

        /// <summary>
        /// A helper to generate a JSON string from a string value
        /// 
        /// Use this to avoid bringing in a full JSON Serializer for
        /// scenarios of string serialization.
        /// </summary>
        /// <param name="text"></param>
        /// <returns>JSON encoded string ("text"), empty ("") or "null".</returns>
        public static string ToJsonString(string text)
        {
            if (text is null)
                return "null";

            var sb = new StringBuilder(text.Length);
            sb.Append("\"");
            var ct = text.Length;

            for (int x = 0; x < ct; x++)
            {
                var c = text[x];

                switch (c)
                {
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        uint i = c;
                        if (i < 32)  // || i > 255
                            sb.Append($"\\u{i:x4}");
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append("\"");

            return sb.ToString();
        }
    }
}
