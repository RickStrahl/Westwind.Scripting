using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// <param name="startDelim">Optional start delimiter for script tags</param>
        /// <param name="endDelim">Optional end delimiter for script tags</param>
        /// <param name="codeIndicator">Optional Code block indicator that indicates raw code to create in the template (ie. `%` which uses `{{% }}`)</param>
        /// <returns>expanded template or null. On null check `scriptEngine.Error` and `scriptEngine.ErrorMessage`</returns>
        public string ExecuteScript(string script, object model,
            CSharpScriptExecution scriptEngine = null,
            string startDelim = "{{", string endDelim = "}}", string codeIndicator = "%")
        {

            if (string.IsNullOrEmpty(script) || !script.Contains("{{"))
                return script;

            var code = ParseScriptToCode(script, startDelim, endDelim, codeIndicator);
            if (code == null)
                return null;

            // expose the parameter as Model
            code = "dynamic Model = parameters[0];\n" + code;

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
        /// <param name="startDelim">Optional start delimiter for script tags</param>
        /// <param name="endDelim">Optional end delimiter for script tags</param>
        /// <param name="codeIndicator">Optional Code block indicator that indicates raw code to create in the template (ie. `%` which uses `{{% }}`)</param>
        /// <returns>expanded template or null. On null check `scriptEngine.Error` and `scriptEngine.ErrorMessage`</returns>
        public string ExecuteScript<TModelType>(string script, TModelType model,
            CSharpScriptExecution scriptEngine = null,
            string startDelim = "{{", string endDelim = "}}", string codeIndicator = "%")
        {

            if (string.IsNullOrEmpty(script) || !script.Contains("{{"))
                return script;

            var code = ParseScriptToCode(script, startDelim, endDelim, codeIndicator);
            if (code == null)
                return null;

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
            string startDelim = "{{", string endDelim = "}}",
            string codeIndicator = "%")
        {
            if (string.IsNullOrEmpty(script) || !script.Contains("{{"))
                return script;

            var code = ParseScriptToCode(script, startDelim, endDelim, codeIndicator);
            if (code == null)
                return null;

            // expose the parameter as Model
            code = "dynamic Model = parameters[0];\n" + code;

            if (scriptEngine != null)
                ScriptEngine = scriptEngine;

            string result = await ScriptEngine.ExecuteCodeAsync(code, model) as string;

            return result;
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
        public async Task<string> ExecuteScriptAsync<TModelType>(string script,
            TModelType model = default,
            CSharpScriptExecution scriptEngine = null,
            string startDelim = "{{", string endDelim = "}}",
            string codeIndicator = "%")
        {
            if (string.IsNullOrEmpty(script) || !script.Contains("{{"))
                return script;

            var code = ParseScriptToCode(script, startDelim, endDelim, codeIndicator);
            if (code == null)
                return null;

            // expose the parameter as Model
            //code = "dynamic Model = parameters[0];\n" + code;

            if (scriptEngine != null)
                ScriptEngine = scriptEngine;
            
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
        /// <param name="startDelim">code and expression start delimiter</param>
        /// <param name="endDelim">code and expression end delimiter</param>
        /// <param name="codeIndicator">code block indicator that combines the start delim plus this character (ie. default of `%` combines to `{{%`)</param>
        /// <returns></returns>
        public  string ParseScriptToCode(string scriptText, string startDelim = "{{", string endDelim = "}}",
            string codeIndicator = "%")
        {
            var atStart = scriptText.IndexOf(startDelim);

            // no script in string - just return - this should be handled higher up
            // and is in ExecuteXXXX methods.
            if (atStart == -1)
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

    }
}

