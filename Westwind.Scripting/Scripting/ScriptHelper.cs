using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Westwind.Scripting
{

    /// <summary>
    /// Script Helper that is injected into the script as a global `Script` variable
    ///
    /// To use:
    ///
    /// {{ Script.RenderPartial("./test.template") }} 
    /// </summary>
    public class ScriptHelper
    {

        /// <summary>
        /// This the base path that's used for ~/ or  / paths when using RenderTemplate
        ///
        /// This value is null by default and if not set the current working directory
        /// is used instead.
        /// </summary>
        public string BasePath { get; set; }


        public string Layout { get; set; }
        /// <summary>
        /// Optional Page Title - useful in HTML Pages that use Layout to
        /// pass the title to the Layout page
        /// </summary>
        public string Title { get; set; }

        public bool IsPreview { get; set;  }


        ScriptParser _parser = new ScriptParser();


        /// <summary>
        /// Renders a partial file into the template
        /// </summary>
        /// <param name="scriptPath">Path to script file to execute</param>
        /// <param name="model">optional model to pass in</param>
        /// <returns></returns>
        public string RenderPartial(string scriptPath, object model = null)
        {
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.Combine(BasePath, scriptPath);
                if (!File.Exists(scriptPath))
                    throw new InvalidEnumArgumentException("Page not found: " + scriptPath);
            }

            var script = File.ReadAllText(scriptPath);
            string result = _parser.ExecuteScript(script, model);
            if (_parser.Error)
            {
                result = $"{{! Template error ({scriptPath}):  " + _parser.ErrorMessage?.Trim() + " !}";
            }

            return result;
        }

        /// <summary>
        /// Renders a partial file into the template
        /// </summary>
        /// <param name="scriptPath">Path to script file to execute</param>
        /// <param name="model">optional model to pass in</param>
        /// <returns></returns>
        public async Task<string> RenderPartialAsync(string scriptPath, object model = null)
        {
            var script = await ReadFileAsync(scriptPath);
            string result = await _parser.ExecuteScriptAsync(script, model);
            if (_parser.Error)
            {
                result = "!! " + _parser.ErrorMessage + " !!";
            }

            return result;
        }


        /// <summary>
        /// Used in a Layout Page to indicate where the content should be rendered
        /// </summary>
        public void RenderContent()
        {  }


        public void RenderSection(string sectionName)
        { }

        public string Section(string sectionName)
        { return string.Empty; }

        public string EndSection(string sectionName)
        { return string.Empty;  }

        /// <summary>
        /// Renders a string of script to effectively allow recursive
        /// rendering of content into a fixed template
        /// </summary>
        /// <param name="scriptPath">text or script to render</param>
        /// <param name="model">optional model to pass in</param>
        /// <returns></returns>
        public string RenderScript(string script, object model = null)
        {
            //var parser = new ScriptParser()
            //{
            //    ScriptEngine = _parser.ScriptEngine,
            //};
            string result = _parser.ExecuteScript(script, model);
            return result;
        }

        /// <summary>
        /// Renders a string of script to effectively allow recursive
        /// rendering of content into a fixed template
        /// </summary>
        /// <param name="scriptPath">text or script to render</param>
        /// <param name="model">optional model to pass in</param>
        /// <returns></returns>
        public async Task<string> RenderScriptAsync(string script, object model = null)
        {
            return await _parser.ExecuteScriptAsync(script, model);
        }

        /// <summary>
        /// Reads the entire content of a file asynchronously
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        private async Task<string> ReadFileAsync(string filePath, Encoding encoding = null)
        {
            filePath = FixBasePath(filePath);

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fs))
            {
                return await reader.ReadToEndAsync();
            }
        }


        /// <summary>
        /// Reads the entire content of a file asynchronously
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        private string ReadFile(string filePath, Encoding encoding = null)
        {
            filePath = FixBasePath(filePath);
            return File.ReadAllText(filePath);
        }

        /// <summary>
        /// Returns a raw string that is Html Encoded even
        /// if encoding by default is enabled or an explicit
        /// {{: }} block is used.
        /// </summary>
        /// <returns></returns>
        public RawString Raw(string value) => new RawString(value);

        /// <summary>
        /// Returns a raw string that is Html Encoded even
        /// if encoding by default is enabled or an explicit
        /// {{: }} block is used.
        /// </summary>
        public RawString Raw(object value) => new RawString(value);


        private string FixBasePath(string filePath)
        {
            if (!string.IsNullOrEmpty(BasePath))
            {
                if (filePath.StartsWith("~") || filePath.StartsWith("/") || filePath.StartsWith("\\"))
                {
                    filePath = Path.Combine(BasePath, filePath.TrimStart('~', '/', '\\'));
                }
            }

            return filePath;
        }

        /// <summary>
        /// Encodes a value using Html Encoding by first converting
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public string HtmlEncode(object value) => ScriptParser.HtmlEncode(value);


        #region Reflection Helpers


        /// <summary>
        /// Retrieve a property value from an object dynamically. This is a simple version
        /// that uses Reflection calls directly. It doesn't support indexers.
        /// </summary>
        /// <param name="instance">Object to make the call on</param>
        /// <param name="property">Property to retrieve</param>
        /// <returns>Object - cast to proper type</returns>
        public object GetProperty(object instance, string property)
        {
            var bindings = BindingFlags.Public | BindingFlags.NonPublic |
                                      BindingFlags.Instance | BindingFlags.IgnoreCase;

            return instance.GetType().GetProperty(property, bindings).GetValue(instance, null);
        }

        /// <summary>
        /// Calls a method on an object dynamically. 
        /// 
        /// This version doesn't require specific parameter signatures to be passed. 
        /// Instead parameter types are inferred based on types passed. Note that if 
        /// you pass a null parameter, type inferrance cannot occur and if overloads
        /// exist the call may fail. if so use the more detailed overload of this method.
        /// </summary> 
        /// <param name="instance">Instance of object to call method on</param>
        /// <param name="method">The method to call as a stringToTypedValue</param>
        /// <param name="parameterTypes">Specify each of the types for each parameter passed. 
        /// You can also pass null, but you may get errors for ambiguous methods signatures
        /// when null parameters are passed</param>
        /// <param name="parms">any variable number of parameters.</param>        
        /// <returns>object</returns>
        public object CallMethod(object instance, string method, params object[] parms)
        {
            // Pick up parameter types so we can match the method properly
            Type[] parameterTypes = null;
            if (parms != null)
            {
                parameterTypes = new Type[parms.Length];
                for (int x = 0; x < parms.Length; x++)
                {
                    // if we have null parameters we can't determine parameter types - exit
                    if (parms[x] == null)
                    {
                        parameterTypes = null;  // clear out - don't use types        
                        break;
                    }
                    parameterTypes[x] = parms[x].GetType();
                }
            }
            return CallMethod(instance, method, parameterTypes, parms);
        }


        /// <summary>
        /// Calls a method on an object dynamically. This version requires explicit
        /// specification of the parameter type signatures.
        /// </summary>
        /// <param name="instance">Instance of object to call method on</param>
        /// <param name="method">The method to call as a stringToTypedValue</param>
        /// <param name="parameterTypes">Specify each of the types for each parameter passed. 
        /// You can also pass null, but you may get errors for ambiguous methods signatures
        /// when null parameters are passed</param>
        /// <param name="parms">any variable number of parameters.</param>        
        /// <returns>object</returns>
        internal static object CallMethod(object instance, string method, Type[] parameterTypes, params object[] parms)
        {
            var bindings = BindingFlags.Public | BindingFlags.NonPublic |
                           BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase;

            if (parameterTypes == null && parms.Length > 0)
                // Call without explicit parameter types - might cause problems with overloads    
                // occurs when null parameters were passed and we couldn't figure out the parm type
                return instance.GetType().GetMethod(method, bindings | BindingFlags.InvokeMethod).Invoke(instance, parms);

            // Call with parameter types - works only if no null values were passed
            return instance.GetType().GetMethod(method, bindings | BindingFlags.InvokeMethod, null, parameterTypes, null).Invoke(instance, parms);
        }

        #endregion



    }
    
    /// <summary>
    /// String Writer Abstraction
    /// </summary>
    public class ScriptWriter : IDisposable
    {
        public ScriptWriter()
        {
            Writer = new StringWriter();
        }
        public ScriptWriter(StringBuilder sb)
        {
            Writer = new StringWriter(sb);
        }


        public StringWriter Writer { get; }

        public void Write(string text) => Writer.Write(text);

        public void Write(object text) => Writer.Write(text?.ToString());

        public void WriteHtmlEncoded(string text) => Writer.Write( ScriptParser.HtmlEncode(text) );
        public void WriteHtmlEncoded(IRawString text) => Writer.Write(text.ToString());  // write without encoding
        public void WriteHtmlEncoded(object text) => Writer.Write( ScriptParser.HtmlEncode(text?.ToString()) );        

        public void WriteLine(string text) => Writer.WriteLine(text);

        public void WriteLine(object text) => Writer.WriteLine(text?.ToString());

        public void Flush() => Writer.Flush();

        public void Clear() => Writer.GetStringBuilder().Clear();

        public override string ToString()
        {
            return Writer.ToString();
        }

        public void Dispose() => Writer?.Dispose();
    }
}


