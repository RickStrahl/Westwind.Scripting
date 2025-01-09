
using System.IO;
using System;

namespace Westwind.Scripting
{

    /// <summary>
    /// Marker interface
    /// </summary>
    public interface IRawString
    {
        string ToString();
    }

    /// <summary>
    /// String container that indicates that this string
    /// should never be Html Encoded.
    /// </summary>
    public class RawString : IRawString
    {
        /// <summary>
        /// The raw string value that's been assigned.
        /// Alternately retrieve with .ToString()
        /// </summary>
        private string Value { get; set; }

        public static RawString Empty => new RawString(string.Empty);

        public RawString()
        { }

        public RawString(string value)
        {
            Value = value;
        }

        public RawString(object value)
        {
            if (value is not null)
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
        public static RawString Raw(string value) => new RawString(value);

        /// <summary>
        /// Returns a raw string (same as new RawString() but
        /// easier to use in code  {{ RawString.Raw() }}
        ///
        /// Functions can return IRawString to 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static RawString Raw(object value) => new RawString(value);
    }
}
