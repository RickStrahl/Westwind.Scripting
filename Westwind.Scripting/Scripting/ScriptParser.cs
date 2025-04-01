using System;
using System.ComponentModel;
using System.IO;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;

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
        /// Allows you to inject additional code into the generated method
        /// that executes the script.
        /// </summary>
        public string AdditionalMethodHeaderCode { get; set; }

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

        public bool SaveGeneratedClassCode
        {
            get => ScriptEngine is { SaveGeneratedCode: true };
            set
            {
                if (ScriptEngine != null)
                    ScriptEngine.SaveGeneratedCode = value;
            }
        }

        /// <summary>
        /// Delimiters used for script parsing
        /// </summary>
        public ScriptingDelimiters ScriptingDelimiters { get; set; } = ScriptingDelimiters.Default;


        #region String Script Execution

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

            var scriptContext = new ScriptFileContext(script);
            var code = ParseScriptToCode(scriptContext);
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
        /// Extracts a variable like Scripts.Title = "Title of code"
        /// </summary>
        /// <param name="key"></param>
        /// <param name="scriptText"></param>
        /// <returns></returns>
        private string ExtractPageVariable(string key, string scriptText = null)
        {
            var matches = Regex.Match(scriptText, key + @"\s?=\s?""(.*?)""", RegexOptions.Multiline);
            if (!matches.Success || matches.Groups.Count < 2)
                return null;

            return matches.Groups[1].Value;
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

            var scriptContext = new ScriptFileContext(script);
            var code = ParseScriptToCode(scriptContext);
            if (code == null)
                return null;

            code = "ScriptHelper Script = new ScriptHelper() { BasePath = " + ScriptParser.EncodeStringLiteral( basePath ) + " };\n" +
                   AdditionalMethodHeaderCode + "\n" +
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

            var scriptContext = new ScriptFileContext(script);
            var code = ParseScriptToCode(scriptContext);
            if (code == null)
                return null;

            // expose the parameter as Model
            code = "dynamic Model = parameters[0];\n" +
                   "ScriptHelper Script = new ScriptHelper() { BasePath = " + ScriptParser.EncodeStringLiteral(basePath) + " };\n" +
                   AdditionalMethodHeaderCode + "\n" +
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

            var scriptContext = new ScriptFileContext(script);            
            var code = ParseScriptToCode(scriptContext);
            if (code == null)
                return null;

            // Model is passed in the signature so no model here
            code = "ScriptHelper Script = new ScriptHelper() { BasePath = " + ScriptParser.EncodeStringLiteral(basePath) + " };\n" +
                   code;

            if (scriptEngine != null)
                ScriptEngine = scriptEngine;

            var type = typeof(TModelType);


            string result = await ScriptEngine.ExecuteCodeAsync<string, TModelType>(code, model) as string;

            return result;
        }
        #endregion

        #region File Script Execution (Layouts, Sections, Partials)

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
        public string ExecuteScriptFile(string scriptFile, object model,
            CSharpScriptExecution scriptEngine = null,
            string basePath = null)
        {           
            var scriptContext = new ScriptFileContext(null)
            {
                ScriptFile = scriptFile,
                Model = model,
                BasePath = basePath
            };

            if (!FileScriptParsing(scriptContext))
                return null;

            var code = ParseScriptToCode(scriptContext);
            if (code == null)
                return null;

            // expose the parameter as Model
            code = "dynamic Model = parameters[0];\n" +
                   "ScriptHelper Script = new ScriptHelper() { BasePath = " + EncodeStringLiteral(scriptContext.BasePath) + "  };\n" +
                   "Script.Title = " + EncodeStringLiteral(scriptContext.Title) + ";\n" +
                   AdditionalMethodHeaderCode + "\n" +
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
        /// <param name="basepath">Optional basePath/root for the script and related partials so ~/ or / can be resolved</param>
        /// <returns>expanded template or null. On null check `scriptEngine.Error` and `scriptEngine.ErrorMessage`</returns>
        public string ExecuteScriptFile<TModelType>(string scriptFile, TModelType model,
            CSharpScriptExecution scriptEngine = null,
            string basePath = null)
        {
            var scriptContext = new ScriptFileContext(null)
            {
                ScriptFile = scriptFile,
                Model = model,
                BasePath = basePath
            };

            if (!FileScriptParsing(scriptContext))
                return null;

            var code = ParseScriptToCode(scriptContext);
            if (code == null)
                return null;

            // expose the parameter as Model
            code = "ScriptHelper Script = new ScriptHelper() { BasePath = " + EncodeStringLiteral(scriptContext.BasePath) + "  };\n" +
                   "Script.Title = " + EncodeStringLiteral(scriptContext.Title) + ";\n" +
                   AdditionalMethodHeaderCode + "\n" +
                   code;

            if (scriptEngine != null)
                ScriptEngine = scriptEngine;

            ScriptEngine.AddAssembly(typeof(TModelType));
            return ScriptEngine.ExecuteCode<string, TModelType>(code, model) as string;
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
        /// <param name="basepath">Optional basePath/root for the script and related partials so ~/ or / can be resolved</param>
        /// <returns>expanded template string or null on error. On null check `scriptEngine.Error` and `scriptEngine.ErrorMessage`</returns>
        public async Task<string> ExecuteScriptFileAsync(string scriptFile, object model,
            CSharpScriptExecution scriptEngine = null,
            string basePath = null)
        {

            var scriptContext = new ScriptFileContext(null)
            {
                ScriptFile = scriptFile,
                Model = model,
                BasePath = basePath
            };

            if (!FileScriptParsing(scriptContext))
                return null;

            var code = ParseScriptToCode(scriptContext);
            if (code == null)
                return null;

            // expose the parameter as Model
            code = "dynamic Model = parameters[0];\n" +
                   "ScriptHelper Script = new ScriptHelper() { BasePath = " + EncodeStringLiteral(scriptContext.BasePath) + "  };\n" +
                   "Script.Title = " + EncodeStringLiteral(scriptContext.Title) + ";\n" +
                   AdditionalMethodHeaderCode + "\n" +
                   code;

            if (scriptEngine != null)
                ScriptEngine = scriptEngine;

            return await ScriptEngine.ExecuteCodeAsync(code, model) as string;
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
        public async Task<string> ExecuteScriptFileAsync<TModelType>(string scriptFile, TModelType model,
            CSharpScriptExecution scriptEngine = null,
            string basePath = null)
        {
            var scriptContext = new ScriptFileContext(null)
            {
                ScriptFile = scriptFile,
                Model = model,
                BasePath = basePath
            };

            if (!FileScriptParsing(scriptContext))
                return null;

            var code = ParseScriptToCode(scriptContext);
            if (code == null)
                return null;

            // expose the parameter as Model
            code = "ScriptHelper Script = new ScriptHelper() { BasePath = " + EncodeStringLiteral(scriptContext.BasePath) + "  };\n" +
                   "Script.Title = " + EncodeStringLiteral(scriptContext.Title) + ";\n" +
                   AdditionalMethodHeaderCode + "\n" +
                   code;

            if (scriptEngine != null)
                ScriptEngine = scriptEngine;

            ScriptEngine.AddAssembly(model.GetType());
            return await ScriptEngine.ExecuteCodeAsync<string, TModelType>(code, model) as string;
        }
        


        /// <summary>
        /// This parses the script file and extracts layout and section information
        /// and updates the `Script` property
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected bool FileScriptParsing(ScriptFileContext context)
        {
            if (string.IsNullOrEmpty(context.BasePath))
                context.BasePath = Path.GetDirectoryName(context.ScriptFile);


            context.Script = File.ReadAllText(context.ScriptFile);
            
            if (string.IsNullOrEmpty(context.Script) || !context.Script.Contains("{{"))
            {                
                return true;
            }

            string basePath = context.BasePath;
            if (string.IsNullOrEmpty(context.BasePath))
                basePath = Path.GetDirectoryName(context.ScriptFile);
            basePath = Utils.NormalizePath(basePath);

            context.BasePath = basePath;          
            context.Title = ExtractPageVariable("Script.Title", context.Script);

            ParseLayoutPage(context);
            ParseSections(context);

            return true;
        }

        // Extracts a section from a Content page: {{ Script.Section("name") }} ... {{ Script.EndSection("name") }}
        static Regex sectionLocationRegEx = new Regex(@"({{ Script.Section\(""(.*?)""\) }}).*?({{ Script.EndSection\("".*?""\) }})", RegexOptions.Singleline | RegexOptions.Multiline);

        // Extract section insertion point in Layout page: {{ Script.RenderSection("name") }}
        static Regex renderSectionRegex = new Regex(@"{{ Script.RenderSection\("".*?""\) }}");

        /// <summary>
        /// This is a helper function that ooks at the content page and retrieves the ScriptLayout directive,
        /// and then tries to the load the layout template.
        ///
        /// The code then looks for the content page and merges the content page into
        /// layout template producing a single script that is assigned to context.Script.
        ///
        /// If Layout lookup fails the existing context.Script (ie. the content page) is
        /// returned.
        /// </summary>
        /// <param name="scriptPageText"></param>
        /// <param name="basePath"></param>
        /// <returns>True - Layout page processed  - No Layout found</returns>
        protected void ParseLayoutPage(ScriptFileContext context)
        {
            string scriptPageText = context.Script; // content page
            string basePath = context.BasePath;

            if (string.IsNullOrEmpty(scriptPageText))
                return;

            if (!scriptPageText.Contains("Script.Layout=") && !scriptPageText.Contains("Script.Layout ="))
                return;

            try
            {
                var layoutFile = ExtractPageVariable("Script.Layout", scriptPageText)?.Replace("\\\\","\\");
                if (layoutFile == null)
                    return; // ignore no layout

                if (!File.Exists(layoutFile))
                {
                    layoutFile = Path.Combine(basePath, layoutFile);
                    if (!File.Exists(layoutFile))
                        throw new InvalidEnumArgumentException("Page not found: " + layoutFile);

                }
                var layoutText = File.ReadAllText(layoutFile);
                if (string.IsNullOrEmpty(layoutText))
                    throw new InvalidEnumArgumentException("Couddn't read file content.");


                layoutText = StripComments(layoutText);

                layoutText = layoutText.Replace("{{ Script.RenderContent() }}", scriptPageText);

                context.Script = layoutText;
            }
            catch (ArgumentException ex)
            {
                throw new InvalidEnumArgumentException("Couldn't load Layout page. " + ex.Message);
            }
        }

        /// <summary>
        /// Parses out sections from the content page and assigns them into the
        /// ScriptContext.Sections dictionary to be later expanded into the layout
        /// page.
        /// </summary>
        /// <param name="scriptContext"></param>
        protected void ParseSections(ScriptFileContext scriptContext)
        {
            const string startSectionStart = "{{ Script.Section(\"";
    
            if (string.IsNullOrEmpty(scriptContext.Script) || !scriptContext.Script.Contains(startSectionStart))
                return;

            string script = scriptContext.Script;
            string scriptLeft = string.Empty;
            bool hasChanges = false;


            var matches = sectionLocationRegEx.Matches(script);
            if (matches.Count < 1)
                return;

            foreach (Match match in matches)
            {
                // Groups: 1 - start delim, 2 - section name, 3 - end delim
                string sectionName = match.Groups[2].Value;
                string section = match.Value;

                // get just the content of the section
                string sectionContent = StringUtils.ExtractString(section, match.Groups[1].Value, match.Groups[3].Value)?.TrimStart(new[] { '\n', '\r' });
                scriptContext.Sections[sectionName] = sectionContent;

                // remove the section from Content page
                script = script.Replace(section, string.Empty);

                hasChanges = true;
            }

            // replace the Layout RenderSection() 
            foreach (var section in scriptContext.Sections)
            {
                string sectionName = section.Key;
                string find = "{{ Script.RenderSection(\"" + sectionName + "\") }}";
                script = script.Replace(find, section.Value);
            }

            // find sections NOT REFERENCED and remove            
            matches = renderSectionRegex.Matches(script);
            foreach (Match match in matches)
            {
                script = script.Replace(match.Value, string.Empty);
            }

            if (hasChanges)
                scriptContext.Script = script;
        }

        
        /// <summary>
        /// Strips {{@  commented block @}} from script
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        protected string StripComments(string script)
        {
            var pattern = @"{{" + ScriptingDelimiters.CommentEncodingCharacter + ".*?" + ScriptingDelimiters.CommentEncodingCharacter + "}}";            
            var matches = Regex.Matches(script, pattern, RegexOptions.Multiline | RegexOptions.Singleline);
            foreach (Match match in matches)
            {
                string expression = match.Value;
                if (!string.IsNullOrEmpty(expression))
                    script = script.Replace(expression, string.Empty);
            }

            return script;
        }

        #endregion


        #region Script To Code Parsing

        /// <summary>
        /// Passes in a block of finalized 'script' code into a string using
        /// code that uses a text writer to output. You can feed the
        /// output from this method in `ExecuteCode()` or similar to
        /// parse the script into an output string that includes the
        /// processed text.
        /// </summary>
        /// <param name="scriptContext">
        /// Script context that is filled in
        /// process of parsing the script.
        /// </param>
        /// <returns></returns>
        public string ParseScriptToCode(ScriptFileContext scriptContext)
        {
            if (scriptContext == null)
                return null;

            var scriptText = scriptContext.Script;
            if (string.IsNullOrEmpty(scriptText))
                return scriptText;

            var atStart = scriptText.IndexOf(ScriptingDelimiters.StartDelim, StringComparison.OrdinalIgnoreCase);

            // no script in string - just return - this should be handled higher up
            // and is in ExecuteXXXX methods.
            if (atStart < 0)
                return "return " + EncodeStringLiteral(scriptText, true) + ";";

            // remove comment blocks {{@
            scriptText = StripComments(scriptText);


            var literal = new StringBuilder();
            using (var code = new StringWriter())
            {
                var atEnd = -1;
                string expression = null;

                
                string initialCode = @"
using( var writer = new ScriptWriter())
{
";
                code.Write(initialCode);                

                bool containsDelimEscape = initialCode.Contains("\\{\\{") || initialCode.Contains("\\}\\}");


                bool isCodeBlock = false;
                while (atStart > -1)
                {
                    atEnd = scriptText.IndexOf(ScriptingDelimiters.EndDelim);                    
                    if (atEnd == -1)
                    {
                        literal.Append(scriptText); // no end tag - take rest
                        break;
                    }
                    if (atEnd < atStart)
                    {
                        ScriptEngine.Error = true;
                        ScriptEngine.ErrorMessage = "Scripting Error: }} bracket nesting error.";
                        return null;
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
                        code.WriteLine(expression); // as is - it's plain code - no /n because the literal takes care of it
                        // process Command (new line
                        isCodeBlock = true;
                    }
                    else if (expression.StartsWith(ScriptingDelimiters.HtmlEncodingIndicator))
                    {
                        expression = expression.Substring(1).Trim();
                        code.WriteLine($"writer.WriteHtmlEncoded( {expression} );");
                    }
                    else if (expression.StartsWith(ScriptingDelimiters.RawTextEncodingIndicator))
                    {
                        expression = expression.Substring(1).Trim();
                        code.WriteLine($"writer.Write( {expression} );");
                    }                    
                    else
                    {
                        if (ScriptingDelimiters.HtmlEncodeExpressionsByDefault)
                            code.WriteLine($"writer.WriteHtmlEncoded( {expression} );");
                        else
                            code.WriteLine($"writer.Write( {expression} );");
                    }

                    // text that is left 
                    scriptText = scriptText.Substring(atEnd + ScriptingDelimiters.EndDelim.Length);

                    if(isCodeBlock)
                    {
                        // strip off any linebreaks following the code block
                        if (scriptText.StartsWith("\n"))
                            scriptText = scriptText.Substring(1);
                        else if(scriptText.StartsWith("\r\n"))
                            scriptText = scriptText.Substring(2);
                    }

                    // look for the next bit
                    atStart = scriptText.IndexOf("{{");
                    if (atStart < 0)
                    {
                        // write out remaining literal text
                        code.WriteLine(
                            $"writer.Write({EncodeStringLiteral(scriptText, true)});");
                    }

                    isCodeBlock = false;
                }

                code.WriteLine("return writer.ToString();\n\n} // using ScriptWriter");


                return code.ToString();
            }

        
        }


        /// <summary>
        /// Passes in a block of 'script' code into a string using
        /// code that uses a text writer to output. You can feed the
        /// output from this method in `ExecuteCode()` or similar to
        /// parse the script into an output string that includes the
        /// processed text.
        /// </summary>
        /// <param name="scriptText">
        /// Script context that is filled in
        /// process of parsing the script.
        /// </param>
        /// <returns></returns>
        public string ParseScriptToCode(string scriptText )
        {
            return ParseScriptToCode(new ScriptFileContext(scriptText));
        }
        #endregion


        #region Script Engine Forwarding

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

            // We want to save the generated code for debugging and error information
            var exec = new CSharpScriptExecution() { SaveGeneratedCode = true };
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

        #region Helpers

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
                        int i = (int)c;
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
        /// <param name="value">Any object - encodes .ToString()</param>
        /// <returns></returns>
        public static string HtmlEncode(object value)
        {
            if (value == null)
                return null;
            if (value is IRawString)
                return value.ToString();

            return System.Net.WebUtility.HtmlEncode(value.ToString());
        }


        public static string HtmlEncode(IRawString raw)
        {
            return raw.ToString();  // no encoding
        }

        /// <summary>
        /// Encodes a value using Html Encoding by first converting
        /// </summary>
        /// <param name="value">string value</param>
        /// <returns></returns>
        public static string HtmlEncode(string value)
        {
            if (value == null)
                return null;
            if (value == string.Empty)
                return string.Empty;            

            return System.Net.WebUtility.HtmlEncode(value);
        }
        #endregion
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
        public string RawTextEncodingIndicator { get; set; } = "!";

        /// <summary>
        /// Used to indicate a block of code should be commented {{ }}
        /// </summary>
        public string CommentEncodingCharacter { get; set; } = "@";


        /// <summary>
        /// A default instance of the delimiters
        /// </summary>
        public static ScriptingDelimiters Default { get; } = new ScriptingDelimiters();


        public string ErrorResult(string message)
        {
            return StartDelim + "ERROR: " + message + " " + EndDelim;
        }
    }
}
