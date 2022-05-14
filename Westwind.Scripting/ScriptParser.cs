using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Westwind.Scripting
{


    /// <summary>
    /// A very simple C# script parser that parses the provided script
    /// as a text string with embedded expressions and code blocks.
    ///
    /// Literal text:
    ///
    /// Parsed as plain text into the script output.
    /// 
    /// Expressions:
    ///
    /// {{ DateTime.Now.ToString("d") }}
    ///
    /// Code Blocks:
    ///
    /// {{% for(int x; x<10; x++  { }}
    ///     {{ x }}. Hello World
    /// {{% } }}
    /// 
    /// </summary>
    public class ScriptParser
    {

        /// <summary>
        /// Passes in a block of 'script' code into a string using
        /// code that uses a text writer to output. You can feed the
        /// output from this method in `ExecuteCode()` or similar to
        /// parse the script into an output string that includes the
        /// processed text.
        /// </summary>
        /// <param name="scriptText"></param>
        /// <param name="startDelim">code and expression start delimiter</param>
        /// <param name="endDelim">code and expression end delimiter</param>
        /// <param name="codeIndicator">code block indicator that combines the start delim plus this character (ie. default of `%` combines to `{{%`)</param>
        /// <returns></returns>
        public static string ParseScriptToCode(string scriptText, string startDelim = "{{", string endDelim = "}}",
            string codeIndicator = "%")
        {
            var atStart = scriptText.IndexOf(startDelim);
            if (atStart == -1)
                return scriptText; // nothing to expand return as is

            var literal = new StringBuilder();
            using (var code = new StringWriter())
            {
                var atEnd = -1;
                string expression = null;

                string initialCode = @"
var writer = new StringWriter();

";
                code.Write(initialCode);

                while (atStart > -1)
                {
                    atEnd = scriptText.IndexOf(endDelim);
                    if (atEnd == -1)
                    {
                        literal.Append(scriptText); // no end tag - take rest
                        break;
                    }

                    // take text up to the tag
                    literal.Append(scriptText.Substring(0, atStart));
                    expression = scriptText.Substring(atStart + startDelim.Length, atEnd - atStart - endDelim.Length);

                    // first we have to write out any left over literal
                    if (literal.Length > 0)
                    {
                        // output the code
                        code.WriteLine(
                            $"writer.Write({EncodeStringLiteral(literal.ToString(), true)});");
                        literal.Clear();
                    }

                    if (expression.StartsWith(codeIndicator))
                    {
                        // this should just be raw code - write out as is
                        expression = expression.Substring(1);
                        code.WriteLine(expression); // as is
                        // process Command (new line
                    }
                    else
                    {
                        code.WriteLine($"writer.Write( {expression} );");
                    }

                    // text that is left 
                    scriptText = scriptText.Substring(atEnd + endDelim.Length);

                    // look for the next bit
                    atStart = scriptText.IndexOf("{{");
                    if (atStart < 0)
                    {
                        // write out remaining literal text
                        code.WriteLine(
                            $"writer.Write({EncodeStringLiteral(scriptText, true)});");
                    }
                }

                code.WriteLine("return writer.ToString();");

                return code.ToString();
            }
        }


        /// <summary>
        /// Executes a script that supports {{ expression }} and {{% code block }} syntax
        /// and returns a string result. This version allows for `async` code inside of
        /// the template.
        ///
        /// You can optionally pass in a pre-configured `CSharpScriptExecution` instance
        /// which allows setting references/namespaces and can capture error information.
        ///
        /// Function returns `null` on error and `scriptEngine.Error` is set to `true`
        /// along with the error message and the generated code.
        /// </summary>
        /// <param name="script">The template to execute that contains C# script</param>
        /// <param name="model">A model that can be accessed in the template as `Model`. Pass null if you don't need to access values.</param>
        /// <param name="scriptEngine">Optional CSharpScriptEngine so you can customize configuration and capture result errors</param>
        /// <param name="startDelim">Optional start delimiter for script tags</param>
        /// <param name="endDelim">Optional end delimiter for script tags</param>
        /// <param name="codeIndicator">Optional Code block indicator that indicates raw code to create in the template (ie. `%` which uses `{{% }}`)</param>
        /// <returns>expanded template or null. On null check `scriptEngine.Error` and `scriptEngine.ErrorMessage`</returns>
        public static async Task<string> ExecuteScriptAsync(string script, object model,
            CSharpScriptExecution scriptEngine = null, string startDelim = "{{", string endDelim = "}}",
            string codeIndicator = "%")
        {
            var code = ParseScriptToCode(script, startDelim, endDelim, codeIndicator);
            if (code == null)
                return null;

            // expose the parameter as Model
            code = "dynamic Model = parameters[0];\n" + code;

            if (scriptEngine == null)
            {
                scriptEngine = new CSharpScriptExecution();
                scriptEngine.AddDefaultReferencesAndNamespaces();
            }

            string result = await scriptEngine.ExecuteCodeAsync(code, model) as string;

            return result;
        }

        /// <summary>
        /// Executes a script that supports {{ expression }} and {{% code block }} syntax
        /// and returns a string result.
        ///
        /// You can optionally pass in a pre-configured `CSharpScriptExecution` instance
        /// which allows setting references/namespaces and can capture error information.
        ///
        /// Function returns `null` on error and `scriptEngine.Error` is set to `true`
        /// along with the error message and the generated code.
        /// </summary>
        /// <param name="script">The template to execute that contains C# script</param>
        /// <param name="model">A model that can be accessed in the template as `Model`. Pass null if you don't need to access values.</param>
        /// <param name="scriptEngine">Optional CSharpScriptEngine so you can customize configuration and capture result errors</param>
        /// <param name="startDelim">Optional start delimiter for script tags</param>
        /// <param name="endDelim">Optional end delimiter for script tags</param>
        /// <param name="codeIndicator">Optional Code block indicator that indicates raw code to create in the template (ie. `%` which uses `{{% }}`)</param>
        /// <returns>expanded template or null. On null check `scriptEngine.Error` and `scriptEngine.ErrorMessage`</returns>
        public static string ExecuteScript(string script, object model, CSharpScriptExecution scriptEngine = null,
            string startDelim = "{{", string endDelim = "}}", string codeIndicator = "%")
        {
            var code = ParseScriptToCode(script, startDelim, endDelim, codeIndicator);
            if (code == null)
                return null;

            // expose the parameter as Model
            code = "dynamic Model = parameters[0];\n" + code;

            if (scriptEngine == null)
            {
                scriptEngine = new CSharpScriptExecution();
                scriptEngine.AddDefaultReferencesAndNamespaces();
            }

            string result = scriptEngine.ExecuteCode(code, model) as string;

            return result;
        }



        /// <summary>
        /// Encodes a string to be represented as a c style string literal. The format
        /// is essentially a JSON string that is returned in double quotes.
        /// 
        /// The string returned includes outer quotes by default: 
        /// "Hello \"Rick\"!\r\nRock on"
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string EncodeStringLiteral(string plainString, bool addQuotes = true)
        {
            if (plainString == null)
                return "null";

            StringBuilder sb = new StringBuilder();
            if (addQuotes)
                sb.Append("\"");

            foreach (char c in plainString)
            {
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
                        int i = (int) c;
                        if (i < 32)
                        {
                            sb.AppendFormat("\\u{0:X04}", i);
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            if (addQuotes)
                sb.Append("\"");

            return sb.ToString();
        }
    }
}

