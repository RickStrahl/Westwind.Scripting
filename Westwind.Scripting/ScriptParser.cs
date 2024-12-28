using System;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

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
    /// {{% for(int x; x&lt;10; x++  { }}
    ///     {{ x }}. Hello World
    /// {{% } }}
    ///
    /// Uses the `.ScriptEngine` property for execution and provides
    /// error information there.
    /// </summary>
    public class ScriptParser 
    {
        /// <summary>
        /// Script Engine used if none is passed in
        /// </summary>
        public CSharpScriptExecution ScriptEngine
        {
            get
            {
                if (_scriptEngine == null)
                    _scriptEngine = CreateScriptEngine();

                return _scriptEngine;
            }
            set => _scriptEngine = value;
        }
        private CSharpScriptExecution _scriptEngine;

        /// <summary>
        /// Determines whether the was a compile time or runtime error
        /// </summary>
        public bool Error => ScriptEngine?.Error ?? false;

        /// <summary>
        /// Error Message if an error occurred
        /// </summary>
        public string ErrorMessage => ScriptEngine?.ErrorMessage;

        /// <summary>
        /// Type of error that occurred during compilation or execution of the template
        /// </summary>
        public ExecutionErrorTypes ErrorType => ScriptEngine?.ErrorType ?? ExecutionErrorTypes.None;


        /// <summary>
        /// Generated code that is compiled
        /// </summary>
        public string GeneratedClassCode => ScriptEngine?.GeneratedClassCode;

        /// <summary>
        /// Generated code with line numbers that is compiled. You can use this
        /// to match error messages to code lines.
        /// </summary>
        public string GeneratedClassCodeWithLineNumbers => ScriptEngine?.GeneratedClassCodeWithLineNumbers;

        /// <summary>
        /// Delimiters used for script parsing
        /// </summary>
        public ScriptingDelimiters ScriptingDelimiters { get; set; } = ScriptingDelimiters.Default;


        #region Script Execution

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
        /// <param name="basepath">Optional basePath/root for the script and related partials so ~/ or / can be resolved</param>
        /// <returns>expanded template or null. On null check `scriptEngine.Error` and `scriptEngine.ErrorMessage`</returns>
        public string ExecuteScript(string script, object model,
            CSharpScriptExecution scriptEngine = null,
            string basePath = null)
        {
            if (string.IsNullOrEmpty(script) || !script.Contains("{{"))
                return script;

            var code = ParseScriptToCode(script);
            if (code == null)
                return null;

            // expose the parameter as Model
            code = "dynamic Model = parameters[0];\n" +
                   "ScriptHelper Script = new ScriptHelper() { BasePath = \"" + basePath + "\"  };\n" +
                   code;

            if (scriptEngine != null)
                ScriptEngine = scriptEngine;

            return ScriptEngine.ExecuteCode(code, model) as string;
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
        /// <returns>expanded template or null. On null check `scriptEngine.Error` and `scriptEngine.ErrorMessage`</returns>
        /// 
        public string ExecuteScript<TModelType>(string script, TModelType model,
            CSharpScriptExecution scriptEngine = null,        
            string basePath = null)
        {
            if (string.IsNullOrEmpty(script) || !script.Contains("{{"))
                return script;

            var code = ParseScriptToCode(script);
            if (code == null)
                return null;

            code = "ScriptHelper Script = new ScriptHelper() { BasePath = \"" + basePath + "\"  };\n" +
                   code;

            if (scriptEngine != null)
                ScriptEngine = scriptEngine;

            return ScriptEngine.ExecuteCode<string, TModelType>(code, model) as string;
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
        /// <param name="model">A model that can be accessed in the template as `Model`. Model is exposed as `dynamic`
        /// which allows passing any value without requiring type dependencies at compile time.
        /// 
        /// Pass null if you don't need to access values.</param>
        /// <param name="scriptEngine">Optional CSharpScriptEngine so you can customize configuration and capture result errors</param>
        /// <param name="startDelim">Optional start delimiter for script tags</param>
        /// <param name="endDelim">Optional end delimiter for script tags</param>
        /// <param name="codeIndicator">Optional Code block indicator that indicates raw code to create in the template (ie. `%` which uses `{{% }}`)</param>
        /// <returns>expanded template or null. On null check `scriptEngine.Error` and `scriptEngine.ErrorMessage`</returns>
        public async Task<string> ExecuteScriptAsync(string script,
            object model = null,
            CSharpScriptExecution scriptEngine = null,
            string basePath = null)
        {
            if (string.IsNullOrEmpty(script) || !script.Contains("{{"))
                return script;

            var code = ParseScriptToCode(script);
            if (code == null)
                return null;

            // expose the parameter as Model
            code = "dynamic Model = parameters[0];\n" +
                   "ScriptHelper Script = new ScriptHelper() { BasePath = \"" + basePath + "\"  };\n" +
                   code;

            if (scriptEngine != null)
                ScriptEngine = scriptEngine;

            string result = await ScriptEngine.ExecuteCodeAsync(code, model) as string;

            return result;
        }

     
        public async Task<string> ExecuteScriptAsync<TModelType>(string script,
            TModelType model = default,
            CSharpScriptExecution scriptEngine = null,
            string basePath = null)
        {
            if (string.IsNullOrEmpty(script) || !script.Contains("{{"))
                return script;

            var code = ParseScriptToCode(script);
            if (code == null)
                return null;

            // Model is passed in the signature so no model here
            code = "ScriptHelper Script = new ScriptHelper() { BasePath = \"" + basePath + "\"  };\n" +
                   code;

            if (scriptEngine != null)
                ScriptEngine = scriptEngine;

            var type = typeof(TModelType);

            
            string result = await ScriptEngine.ExecuteCodeAsync<string, TModelType>(code, model) as string;

            return result;
        }


        /// <summary>
        /// Passes in a block of 'script' code into a string using
        /// code that uses a text writer to output. You can feed the
        /// output from this method in `ExecuteCode()` or similar to
        /// parse the script into an output string that includes the
        /// processed text.
        /// </summary>
        /// <param name="scriptText"></param>
        /// <returns></returns>
        public string ParseScriptToCode(string scriptText)
        {
            if (string.IsNullOrEmpty(scriptText))
                return scriptText;

            var atStart = scriptText.IndexOf(ScriptingDelimiters.StartDelim,StringComparison.OrdinalIgnoreCase);

            // no script in string - just return - this should be handled higher up
            // and is in ExecuteXXXX methods.
            if (atStart < 0)
                return "return " + EncodeStringLiteral(scriptText, true) + ";";

            var literal = new StringBuilder();
            using (var code = new StringWriter())
            {
                var atEnd = -1;
                string expression = null;

                string initialCode = @"
var writer = new StringWriter();

";
                code.Write(initialCode);

                bool containsDelimEscape = initialCode.Contains("\\{\\{") || initialCode.Contains("\\}\\}");

                while (atStart > -1)
                {
                    atEnd = scriptText.IndexOf(ScriptingDelimiters.EndDelim);
                    if (atEnd == -1)
                    {
                        literal.Append(scriptText); // no end tag - take rest
                        break;
                    }

                    // take text up to the tag
                    literal.Append(scriptText.Substring(0, atStart));
                    expression = scriptText.Substring(atStart + ScriptingDelimiters.StartDelim.Length, atEnd - atStart - ScriptingDelimiters.EndDelim.Length);

                    // first we have to write out any left over literal
                    if (literal.Length > 0)
                    {                        
                        string literalText = containsDelimEscape ?
                            literal.ToString().Replace("\\{\\{", "{{").Replace("\\}\\}", "}}") :
                            literal.ToString();

                        // output the code
                        code.WriteLine(
                            $"writer.Write({EncodeStringLiteral(literalText, true)});");
                        literal.Clear();
                    }

                    if (expression.StartsWith(ScriptingDelimiters.CodeBlockIndicator))
                    {
                        // this should just be raw code - write out as is
                        expression = expression.Substring(1);
                        code.WriteLine(expression); // as is - it's plain code
                        // process Command (new line
                    }
                    else if (expression.StartsWith(ScriptingDelimiters.HtmlEncodingIndicator))
                    {
                        expression = expression.Substring(1).Trim();
                        code.WriteLine($"writer.Write( ScriptParser.HtmlEncode({expression}) );");
                    }
                    else if (expression.StartsWith(ScriptingDelimiters.RawTextEncodingIndicator))
                    {
                        expression = expression.Substring(1).Trim();
                        code.WriteLine($"writer.Write( {expression} );");
                    }
                    else
                    {
                        if (ScriptingDelimiters.HtmlEncodeExpressionsByDefault)                                                 
                            code.WriteLine($"writer.Write( ScriptParser.HtmlEncode({expression}) );");                                                    
                        else
                            code.WriteLine($"writer.Write( {expression} );");
                    }

                    // text that is left 
                    scriptText = scriptText.Substring(atEnd + ScriptingDelimiters.EndDelim.Length);

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


       


        #endregion

        #region Script Engine

        /// <summary>
        /// Creates an instance of a script engine with default configuration settings
        /// set and the abililty to quickly specify addition references and namespaces.
        ///
        /// You can pass this to ExecuteScript()/ExecuteScriptAsync()
        /// </summary>
        /// <param name="references">optional list of string assembly file names</param>
        /// <param name="namespaces">optional list of name spaces</param>
        /// <param name="referenceTypes">optional list of reference types</param>
        /// <returns></returns>
        public CSharpScriptExecution CreateScriptEngine(
            string[] references = null,
            string[] namespaces = null,
            Type[] referenceTypes = null)
        {
            var exec = new CSharpScriptExecution() {SaveGeneratedCode = true};
            exec.AddDefaultReferencesAndNamespaces();

            if (references != null && references.Length > 0)
                exec.AddAssemblies(references);
            if (referenceTypes != null && referenceTypes.Length > 0)
            {
                for (int i = 0; i < referenceTypes.Length; i++)
                    exec.AddAssembly(referenceTypes[i]);
            }

            if (namespaces != null)
                exec.AddNamespaces(namespaces);

            return exec;
        }

        /// <summary>
        /// Adds an assembly to the list of references for compilation
        /// using a dll filename
        /// </summary>
        /// <param name="assemblyFile">Assembly filenames</param>
        public void AddAssembly(string assemblyFile) => ScriptEngine.AddAssembly(assemblyFile);


        /// <summary>
        /// Adds an assembly to the list of references for compilation
        /// using a type that is loaded and contained in the assembly
        /// </summary>
        /// <param name="typeInAssembly">type loaded and contained in the target assembly</param>
        public void AddAssembly(Type typeInAssembly) => ScriptEngine.AddAssembly(typeInAssembly);

        /// <summary>
        /// Adds several assembly to the list of references for compilation
        /// using a dll filenames.
        /// </summary>
        /// <param name="assemblies">Assembly file names</param>
        public void AddAssemblies(params string[] assemblies) => ScriptEngine.AddAssemblies(assemblies);

        /// <summary>
        /// list of meta references to assemblies. Can be used with `Basic.References
        /// </summary>
        /// <param name="metaAssemblies"></param>
        public void AddAssemblies(params PortableExecutableReference[] metaAssemblies) => ScriptEngine.AddAssemblies(metaAssemblies);

        /// <summary>
        /// Add a namespace for compilation of the template
        /// </summary>
        /// <param name="nameSpace"></param>
        public void AddNamespace(string nameSpace) => ScriptEngine.AddNamespace(nameSpace);

        /// <summary>
        /// Add a list of namespaces for compilation of the template
        /// </summary>
        /// <param name="nameSpaces"></param>
        public void AddNamespaces(params string[] nameSpaces) => ScriptEngine.AddNamespaces(nameSpaces);

        #endregion


        /// <summary>
        /// Encodes a string to be represented as a C# style string literal. 
        ///
        /// Example output:
        /// "Hello \"Rick\"!\r\nRock on"
        /// </summary>
        /// <param name="plainString">string to encode</param>
        /// <param name="addQuotes">if true adds quotes around the encoded text</param>
        /// <returns></returns>
        public static string EncodeStringLiteral(string plainString, bool addQuotes = true)
        {
            if (plainString == null)
                return "null";

            var sb = new StringBuilder();
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


        /// <summary>
        /// Encodes a value using Html Encoding by first converting
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string HtmlEncode(object value)
        {
            if (value == null)
                return null;

            return System.Net.WebUtility.HtmlEncode(value.ToString());
        }
    }


    /// <summary>
    /// Class that encapsulates the delimiters used for script parsing
    /// </summary>
    public class ScriptingDelimiters
    {
        public ScriptingDelimiters()
        {
        }

        /// <summary>
        /// Start delimiter for expressions and script tags
        /// </summary>
        public string StartDelim { get; set; } = "{{";

        /// <summary>
        /// End delimiter for expressions and script tags
        /// </summary>
        public string EndDelim { get; set; } = "}}";

        /// <summary>
        /// Indicator for code blocks inside of StartDelim (ie. {{% code }} for code blocks)
        /// </summary>
        public string CodeBlockIndicator { get; set; } = "%";

        /// <summary>
        /// If true all expressions except the Raw Html indicator are Html Encoded
        /// </summary>
        public bool HtmlEncodeExpressionsByDefault { get; set; }

        /// <summary>
        /// Indicator for expressions to be explicitly HtmlEncoded
        /// </summary>
        public string HtmlEncodingIndicator { get; set; } = ":";

        /// <summary>
        /// Indicator for expressions to be explicitly NOT encoded and just returned as is regardless of HtmlEncodeByDefault
        /// </summary>
        public string RawTextEncodingIndicator { get; set; } = "@";
    

        /// <summary>
        /// A default instance of the delimiters
        /// </summary>
        public static ScriptingDelimiters Default { get; } = new ScriptingDelimiters();
    }
}
