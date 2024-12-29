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

    }
}
