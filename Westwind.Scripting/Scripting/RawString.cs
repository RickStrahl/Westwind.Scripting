
using System.IO;
using System;

namespace Westwind.Scripting
{

    /// <summary>
    /// Marker interface
    /// </summary>
    public interface IRawString
    {
        string Value { get; set; }
    }

    public class RawString : IRawString
    {
        /// <summary>
        /// The raw string. You can also use ToString()
        /// to retrieve this.
        /// </summary>
        public string Value { get; set; }

        public RawString(string value)
        {
            Value = value;
        }

        public RawString(object value)
        {
            if (value == null)
                Value = null;
            else
                Value = value.ToString();
        }


        public override string ToString()
        {            
            return Value;
        }


        /// <summary>
        /// Returns a raw string (same as new RawString() but
        /// easier to use in code  {{ RawString.Raw() }}
        ///
        /// Functions can return IRawString to 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>

        public static IRawString Raw(string value)
        {
            return new RawString(value);
        }
    }
}
