using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;


namespace Westwind.Scripting
{
    /// <summary>
    /// Class that can be used to execute code snippets or entire blocks of methods
    /// dynamically. Two methods are provided:
    ///
    /// * ExecuteCode -  executes code. Pass parameters and return a value
    /// * ExecuteMethod - lets you provide one or more method bodies to execute
    /// * Evaluate - Evaluates an expression on the fly (uses ExecuteCode internally)
    /// * CompileClass - compiles a class and returns the a class instance
    ///
    /// Assemblies used for execution are cached and are reused for a given block
    /// of code provided.
    /// </summary>
    public class CSharpScriptExecution
    {
        /// <summary>
        /// Internal list of assemblies that are cached for snippets of the same type.
        /// List holds a list of cached assemblies with a hash code for the code executed as
        /// the key.
        /// </summary>
        protected static ConcurrentDictionary<int, Assembly> CachedAssemblies =
            new ConcurrentDictionary<int, Assembly>();

        /// <summary>
        /// List of additional namespaces to add to the script
        /// </summary>
        public NamespaceList Namespaces { get; } = new NamespaceList();


        /// <summary>
        /// List of additional assembly references that are added to the
        /// compiler parameters in order to execute the script code.
        /// </summary>
        public ReferenceList References { get; } = new ReferenceList();


        /// <summary>
        /// Last generated code for this code snippet with line numbers
        /// </summary>
        public string GeneratedClassCodeWithLineNumbers => Utils.GetTextWithLineNumbers(GeneratedClassCode);

        /// <summary>
        /// Name of the namespace that a class is generated in
        /// </summary>
        public string GeneratedNamespace { get; set; } = "__ScriptExecution";

        /// <summary>
        /// Name of the class when a class name is not explicitly provided
        /// as part of the code compiled.
        ///
        /// Always used for the module name.
        ///
        /// By default this value is a unique generated id. Make sure if you
        /// create multiple compilations that you change the name here,
        /// otherwise you end up with duplicate module names that fail.
        /// </summary>
        public string GeneratedClassName { get; set; } = "__" + Utils.GenerateUniqueId();

        /// <summary>
        /// Last generated code for this code snippet if SaveGeneratedCode = true
        /// </summary>
        public string GeneratedClassCode { get; set; }

        /// <summary>
        /// Determines whether GeneratedCode will be set with the source
        /// code for the full generated class
        /// </summary>
        public bool SaveGeneratedCode { get; set; }

        /// <summary>
        /// If true throws exceptions when executing the code rather
        /// than setting the `Error`, `ErrorMessage` and `LastException`
        /// properties.
        /// 
        /// Note: Compilation errors will not throw, but always set properties!
        /// </summary>
        public bool ThrowExceptions { get; set; }

        /// <summary>
        /// If true parses references in code that are referenced with:
        /// #r assembly.dll
        /// </summary>
        public bool AllowReferencesInCode { get; set; } = false;

        /// <summary>
        /// Filename for the output assembly to generate. If empty the
        /// assembly is generated in memory (dynamic filename managed by
        /// the .NET runtime)
        /// </summary>
        public string OutputAssembly { get; set; }

        /// <summary>
        /// Determines whether the code is compiled in Debug or Release mode
        /// Defaults to Release and there's no good reason for scripts to use
        /// anything else since debug info is not available in Reflection invoked
        /// or dynamically invoked methods.
        ///
        /// Useful only when generating classes with OutputAssembly set when
        /// creating self-contained assemblies for other uses.
        /// </summary>
        public bool CompileWithDebug { get; set; }


        #region Error Handling Properties

        /// <summary>
        /// Error message if an error occurred during the invoked
        /// method or script call
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Error flag that is set if an error occurred during the invoked
        /// method or script call
        /// </summary>
        public bool Error { get; set; }


        /// <summary>
        /// Determines whether the error is a compile time
        /// error or runtime error
        /// </summary>
        public ExecutionErrorTypes ErrorType { get; set; } = ExecutionErrorTypes.None;


        /// <summary>
        /// Last Exception fired when a runtime error occurs
        ///
        /// Generally this only contains the error message, but
        /// there's no call stack information available due
        /// to the Reflection or dynamic code invocation
        /// </summary>
        public Exception LastException { get; set; }

#endregion


#region Internal Settings


        /// <summary>
        /// Internal reference to the Assembly Generated
        /// </summary>
        protected Assembly Assembly { get; set; }

        /// <summary>
        /// Internal reference to the generated type that
        /// is to be invoked
        /// </summary>
        public object ObjectInstance { get; set; }

#endregion

        /// <summary>
        /// Creates a default Execution Engine which has:
        ///
        /// * AddDefaultReferences and Namespaces set
        /// * SaveGeneratedCode = true
        ///
        /// Optionally allows to pass in references and namespaces
        /// </summary>
        /// <param name="references"></param>
        /// <param name="namespaces"></param>
        /// <param name="referenceTypes"></param>
        /// <returns></returns>
        public static CSharpScriptExecution CreateDefault(
            string[] references = null,
            string[] namespaces = null,
            Type[] referenceTypes = null)
        {
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

        #region Execution Methods


        /// <summary>
        /// Executes a complete method by wrapping it into a class, compiling
        /// and instantiating the class and calling the method.
        ///
        /// Class should include full class header (instance type, return value and parameters)
        ///
        /// Example:
        /// "public string HelloWorld(string name) { return name; }"
        ///
        /// "public async Task&lt;string&gt; HelloWorld(string name) { await Task.Delay(1); return name; }"
        ///
        /// Async Method Note: Keep in mind that
        /// the method is not cast to that result - it's cast to object so you
        /// have to unwrap it:
        /// var objTask = script.ExecuteMethod(asyncCodeMethod); // object result
        /// var result = await (objTask as Task&lt;string&gt;);  //  cast and unwrap
        /// </summary>
        /// <param name="code">One or more complete methods.</param>
        /// <param name="methodName">Name of the method to call.</param>
        /// <param name="parameters">any number of variable parameters</param>
        /// <returns></returns>
        public object ExecuteMethod(string code, string methodName, params object[] parameters)
        {
            ClearErrors();

            int hash = GenerateHashCode(code);

            // check for #r and using directives
            code = ParseReferencesInCode(code);
            
            if (!CachedAssemblies.ContainsKey(hash))
            {
                var sb = GenerateClass(code);
                if (!CompileAssembly(sb.ToString()))
                    return null;

                CachedAssemblies[hash] = Assembly;
            }
            else
            {
                Assembly = CachedAssemblies[hash];

                // Figure out the class name
                var type = Assembly.ExportedTypes.First();
                GeneratedClassName = type.Name;
                GeneratedNamespace = type.Namespace;
            }

            var instance = CreateInstance(); // also stores into `ObjectInstance` so it can be reused
            if (instance == null)
                return null;

            return InvokeMethod(instance, methodName, parameters);
        }



        /// <summary>
        /// Executes a complete method by wrapping it into a class, compiling
        /// and instantiating the class and calling the method.
        ///
        /// Class should include full class header (instance type, return value and parameters)
        ///
        /// Example:
        /// "public string HelloWorld(string name) { return name; }"
        ///
        /// "public async Task&lt;string&gt; HelloWorld(string name) { await Task.Delay(1); return name; }"
        ///
        /// Async Method Note: Keep in mind that
        /// the method is not cast to that result - it's cast to object so you
        /// have to unwrap it:
        /// var objTask = script.ExecuteMethod(asyncCodeMethod); // object result
        /// var result = await (objTask as Task&lt;string&gt;);  //  cast and unwrap
        /// </summary>
        /// <param name="code">One or more complete methods.</param>
        /// <param name="methodName">Name of the method to call.</param>
        /// <param name="parameters">any number of variable parameters</param>
        /// <returns></returns>
        public TResult ExecuteMethod<TResult>(string code, string methodName, params object[] parameters)
        {
            var result = ExecuteMethod(code, methodName, parameters);

            if (result is TResult)
                return (TResult) result;

            return default;
        }


        /// <summary>
        /// Executes a complete async method by wrapping it into a class, compiling
        /// and instantiating the class and calling the method and unwrapping the
        /// task result.
        ///
        /// Class should include full class header (instance type, return value and parameters)
        ///
        /// "public async Task&lt;object&gt; HelloWorld(string name) { await Task.Delay(1); return name; }"
        /// "public async Task HelloWorld(string name) { await Task.Delay(1); Console.WriteLine(name); }"
        /// 
        /// Async Method Note: Keep in mind that
        /// the method is not cast to that result - it's cast to object so you
        /// have to unwrap it:
        /// var objTask = script.ExecuteMethod(asyncCodeMethod); // object result
        /// var result = await (objTask as Task&lt;string&gt;);  //  cast and unwrap
        /// </summary>
        /// <param name="code">One or more complete methods.</param>
        /// <param name="methodName">Name of the method to call.</param>
        /// <param name="parameters">any number of variable parameters</param>
        /// <returns>result value of the method</returns>
        public async Task<object> ExecuteMethodAsync(string code, string methodName, params object[] parameters)
        {
            // this result will be a task of object (async method called)
            var taskResult = ExecuteMethod(code, methodName, parameters) as Task<object>;

            if (taskResult == null)
                return default;

            object result = null;
            if (ThrowExceptions)
            {
                result = await ((Task<object>) taskResult);
            }
            else
            {
                try
                {
                    result = await ((Task<object>) taskResult);
                }
                catch (Exception ex)
                {
                    SetErrors(ex);
                    ErrorType = ExecutionErrorTypes.Runtime;
                    return default;
                }
            }

            return result; 
        }


        /// <summary>
        /// Executes a complete async method by wrapping it into a class, compiling
        /// and instantiating the class and calling the method and unwrapping the
        /// task result.
        ///
        /// Class should include full class header (instance type, return value and parameters)
        ///
        /// "public async Task&lt;string&gt; HelloWorld(string name) { await Task.Delay(1); return name; }"
        ///
        /// Async Method Note: Keep in mind that
        /// the method is not cast to that result - it's cast to object so you
        /// have to unwrap it:
        /// var objTask = script.ExecuteMethod(asyncCodeMethod); // object result
        /// var result = await (objTask as Task&lt;string&gt;);  //  cast and unwrap
        /// </summary>
        /// <param name="code">One or more complete methods.</param>
        /// <param name="methodName">Name of the method to call.</param>
        /// <param name="parameters">any number of variable parameters</param>
        /// <typeparam name="TResult">The result type (string, object, etc.) of the method</typeparam>
        /// <returns>result value of the method</returns>
        public async Task<TResult> ExecuteMethodAsync<TResult>(string code, string methodName,
            params object[] parameters)
        {
            // this result will be a task of object (async method called)
            var taskResult = ExecuteMethod(code, methodName, parameters) as Task<TResult>;


            if (taskResult == null)
                return default;

            TResult result;
            if (ThrowExceptions)
            {
                result = await taskResult;
            }
            else
            {
                try
                {
                    result = await taskResult;
                }
                catch (Exception ex)
                {
                    SetErrors(ex);
                    ErrorType = ExecutionErrorTypes.Runtime;
                    return default;
                }
            }

            return (TResult) result;
        }

        /// <summary>
        /// Evaluates a single value or expression that returns a value.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public object Evaluate(string code, params object[] parameters)
        {
            ClearErrors();

            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Can't evaluate empty code. Please pass code.");

            code = ParseCodeWithParametersArray(code, parameters);
            return ExecuteCode("return " + code + ";", parameters);
        }

        /// <summary>
        /// Evaluates a single value or expression that returns a value.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public TResult Evaluate<TResult>(string code, params object[] parameters)
        {
            ClearErrors();

            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Can't evaluate empty code. Please pass code.");

            code = ParseCodeWithParametersArray(code, parameters);
            return ExecuteCode<TResult>("return " + code + ";", parameters);
        }

        /// <summary>
        /// Evaluates an awaitable expression that returns a value
        ///
        /// Example:
        /// script.EvaluateAsync("await ActiveEditor.GetSelection()",model);
        /// </summary>
        /// <param name="code">Code to execute</param>
        /// <param name="parameters">Optional parameters to pass. Access as `object parameters[]` in expression</param>
        /// <returns></returns>
        public Task<object> EvaluateAsync(string code, params object[] parameters)
        {
            ClearErrors();

            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Can't evaluate empty code. Please pass code.");

            code = ParseCodeWithParametersArray(code, parameters);
            return ExecuteCodeAsync("return " + code + ";", parameters);
        }


        /// <summary>
        /// Evaluates an awaitable expression that returns a value
        ///
        /// Example:
        /// script.EvaluateAsync<string></string>("await ActiveEditor.GetSelection()",model);
        /// </summary>
        /// <param name="code">code to execute</param>
        /// <param name="parameters">Optional parameters to pass. Access as `object parameters[]` in expression</param>
        /// <returns></returns>
        public Task<TResult> EvaluateAsync<TResult>(string code, params object[] parameters)
        {
            ClearErrors();

            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Can't evaluate empty code. Please pass code.");

            code = ParseCodeWithParametersArray(code, parameters);

            var result = ExecuteCodeAsync<TResult>("return " + code + ";", parameters);
            if (result == null)
                return default;
            return result;
        }


        /// <summary>
        /// Executes a snippet of code. Pass in a variable number of parameters
        /// (accessible via the parameters[0..n] array) and return an object parameter.
        /// Code should include:  return (object) SomeValue as the last line or return null
        /// </summary>
        /// <param name="code">The code to execute</param>
        /// <param name="parameters">The parameters to pass the code
        /// You can reference parameters as @0, @1, @2 in code to map
        /// to the parameter array items (ie. @1 instead of parameters[1])
        /// </param>
        /// <returns></returns>
        public object ExecuteCode(string code, params object[] parameters)
        {
            ClearErrors();

            code = ParseCodeWithParametersArray(code, parameters);

            return ExecuteMethod("public object ExecuteCode(params object[] parameters)" +
                                 Environment.NewLine +
                                 "{\n" +
                                 code +
                                 Environment.NewLine +
                                 // force a return value - compiler will optimize this out
                                 // if the code provides a return
                                 (!code.Contains("return ")
                                     ? "return default;" + Environment.NewLine
                                     : string.Empty) +
                                 Environment.NewLine +
                                 "}",
                "ExecuteCode", parameters);
        }

        /// <summary>
        /// Executes a snippet of code. Pass in a variable number of parameters
        /// (accessible via the parameters[0..n] array) and return an object parameter.
        /// Code should include:  return (object) SomeValue as the last line or return null
        /// </summary>
        /// <param name="code">The code to execute</param>
        /// <param name="parameters">The parameters to pass the code
        /// You can reference parameters as @0, @1, @2 in code to map
        /// to the parameter array items (ie. @1 instead of parameters[1])
        /// </param>
        /// <returns>Result cast to a type you specify</returns>
        public TResult ExecuteCode<TResult>(string code, params object[] parameters)
        {
            ClearErrors();

            code = ParseCodeWithParametersArray(code, parameters);

            var result = ExecuteMethod("public object ExecuteCode(params object[] parameters)" +
                                       Environment.NewLine +
                                       "{\n" +
                                       code +
                                       Environment.NewLine +
                                       // force a return value - compiler will optimize this out
                                       // if the code provides a return
                                       (!code.Contains("return ")
                                           ? "return default;" + Environment.NewLine
                                           : string.Empty) +
                                       Environment.NewLine +
                                       "}",
                "ExecuteCode", parameters);

            return (TResult) result;
        }

        /// <summary>
        /// Executes a snippet of code. Pass in a variable number of parameters
        /// (accessible via the parameters[0..n] array) and return an object parameter.
        /// Code should include:  return (object) SomeValue as the last line or return null
        /// </summary>
        /// <param name="code">The code to execute</param>
        /// <param name="parameters">The parameters to pass the code
        /// You can reference parameters as @0, @1, @2 in code to map
        /// to the parameter array items (ie. @1 instead of parameters[1])
        /// </param>
        /// <returns>Result cast to a type you specify</returns>
        public TResult ExecuteCode<TResult, TModelType>(string code, TModelType model)
        {
            ClearErrors();

            var modelType = typeof(TModelType).FullName;
            var resultType = typeof(TResult).FullName;

            var result = ExecuteMethod<TResult>($"public {resultType} ExecuteCode({modelType} Model)" +
                                       Environment.NewLine +
                                       "{\n" +
                                       code +
                                       Environment.NewLine +
                                       // force a return value - compiler will optimize this out
                                       // if the code provides a return
                                       (!code.Contains("return ")
                                           ? "return default;" + Environment.NewLine
                                           : string.Empty) +
                                       "}",
                "ExecuteCode", model);

            return result;
        }


        /// <summary>
        /// Executes a snippet of code. Pass in a variable number of parameters
        /// (accessible via the parameters[0..n] array) and return an `object` value.
        ///
        /// Code should always return a result:
        /// include:  `return (object) SomeValue` or `return null`
        /// </summary>
        /// <param name="code">The code to execute</param>
        /// <param name="parameters">The parameters to pass the code
        /// You can reference parameters as @0, @1, @2 in code to map
        /// to the parameter array items (ie. @1 instead of parameters[1])
        /// </param>
        /// <returns></returns>
        public Task<object> ExecuteCodeAsync(string code, params object[] parameters)
        {
            ClearErrors();

            code = ParseCodeWithParametersArray(code, parameters);

            return ExecuteMethodAsync<object>("public async Task<object> ExecuteCode(params object[] parameters)" +
                                              Environment.NewLine +
                                              "{\n" +
                                              code +
                                              Environment.NewLine +
                                              // force a return value - compiler will optimize this out
                                              // if the code provides a return

                                              (!code.Contains("return ")
                                                  ? "return default;" + Environment.NewLine
                                                  : string.Empty) +
                                              "}",
                "ExecuteCode", parameters);
        }


        /// <summary>
        /// Executes a snippet of code. Pass in a variable number of parameters
        /// (accessible via the parameters[0..n] array) and return an `object` value.
        ///
        /// Code should always return a result:
        /// include:  `return (object) SomeValue` or `return null`
        /// </summary>
        /// <param name="code">The code to execute</param>
        /// <param name="parameters">The parameters to pass the code
        /// You can reference parameters as @0, @1, @2 in code to map
        /// to the parameter array items (ie. @1 instead of parameters[1])
        /// </param>
        /// <returns></returns>
        public Task<TResult> ExecuteCodeAsync<TResult>(string code, params object[] parameters)
        {
            ClearErrors();

            code = ParseCodeWithParametersArray(code, parameters);

            var typeName = typeof(TResult).FullName;

            return ExecuteMethodAsync<TResult>(
                $"public async Task<{typeName}> ExecuteCode(params object[] parameters)" +
                Environment.NewLine +
                "{" +
                code +
                Environment.NewLine +
                // force a return value - compiler will optimize this out
                // if the code provides a return
                (!code.Contains("return ")
                    ? "return default;" + Environment.NewLine
                    : string.Empty) +
                Environment.NewLine +
                "}",
                "ExecuteCode", parameters);
        }

        /// <summary>
        /// Executes a snippet of code. Pass in a variable number of parameters
        /// (accessible via the parameters[0..n] array) and return an `object` value.
        ///
        /// Code should always return a result:
        /// include:  `return (object) SomeValue` or `return null`
        /// </summary>
        /// <param name="code">The code to execute</param>
        /// <param name="model">an optional model to pass to the code which is
        /// then accessible as a `Model` property in the code.
        /// </param>
        /// <returns></returns>
        public Task<TResult> ExecuteCodeAsync<TResult, TModelType>(string code, TModelType model)
        {
            ClearErrors();

            var resultTypename = typeof(TModelType).FullName;
            var typeName = typeof(TResult).FullName;

            var res = ExecuteMethodAsync<TResult>($"public async Task<{typeName}> ExecuteCode({resultTypename} Model)" +
                                                  Environment.NewLine +
                                                  "{\n" +
                                                  code +
                                                  Environment.NewLine +
                                                  // force a return value - compiler will optimize this out
                                                  // if the code provides a return
                                                  (!code.Contains("return ")
                                                      ? "return default;" + Environment.NewLine
                                                      : string.Empty) +
                                                  Environment.NewLine +
                                                  "}",
                "ExecuteCode", model);
            return res;
        }

        /// <summary>
        /// Executes a method from an assembly that was previously compiled
        /// </summary>
        /// <param name="code"></param>
        /// <param name="assembly"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public object ExecuteCodeFromAssembly(string code, Assembly assembly, params object[] parameters)
        {
            ClearErrors();

            Assembly = assembly;

            ObjectInstance = CreateInstance();
            if (ObjectInstance == null)
                return null;

            return ExecuteMethod(code, "ExecuteMethod", parameters);
        }

        /// <summary>
        /// Executes a method from an assembly that was previously compiled.
        ///
        /// Creates the instance based on the current settings of this class.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="assembly"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public Task<TResult> ExecuteCodeFromAssemblyAsync<TResult>(string code, Assembly assembly,
            params object[] parameters)
        {
            ClearErrors();

            Assembly = assembly;

            ObjectInstance = CreateInstance();
            if (ObjectInstance == null)
                return null;

            return ExecuteMethodAsync<TResult>(code, "ExecuteMethod", parameters);
        }

        #endregion

        #region Execute Script


        /// <summary>
        /// Executes a script template that interpolates `{{ }}` C# expressions
        /// and `{{% }}` C# code blocks in a string.
        /// </summary>
        /// <param name="csharpTemplate"></param>
        /// <param name="model"></param>
        /// <typeparam name="TModelType"></typeparam>
        /// <returns></returns>
        public string ExecuteScript<TModelType>(string csharpTemplate, TModelType model)
        {
            var script = new ScriptParser() {ScriptEngine = this};
            return script.ExecuteScript<TModelType>(csharpTemplate, model);
        }

        /// <summary>
        /// Executes a script template that interpolates `{{ }}` C# expressions
        /// and `{{% }}` C# code blocks in a string.
        /// </summary>
        /// <param name="csharpTemplate"></param>
        /// <param name="model"></param>
        /// <typeparam name="TModelType"></typeparam>
        /// <returns></returns>
        public Task<string> ExecuteScriptAsync<TModelType>(string csharpTemplate, TModelType model)
        {
            var script = new ScriptParser() { ScriptEngine = this };
            return script.ExecuteScriptAsync<TModelType>(csharpTemplate, model);
        }

        #endregion

        #region Compilation and Code Generation

        /// <summary>
        /// Compiles a class and creates an assembly from the compiled class.
        ///
        /// Assembly is stored on the `.Assembly` property. Use `noLoad()`
        /// to bypass loading of the assembly
        ///
        /// Must include parameterless ctor()
        /// </summary>
        /// <param name="source">Source code</param>
        /// <param name="noLoad">if set doesn't load the assembly (useful only when OutputAssembly is set)</param>
        /// <returns></returns>
        public bool CompileAssembly(string source, bool noLoad = false)
        {
            ClearErrors();

            var tree = SyntaxFactory.ParseSyntaxTree(source.Trim());

            
            var optimizationLevel = CompileWithDebug ? OptimizationLevel.Debug : OptimizationLevel.Release;
            
            
            var compilation = CSharpCompilation.Create(GeneratedClassName)
                .WithOptions(new CSharpCompilationOptions(
                            OutputKind.DynamicallyLinkedLibrary,
                            optimizationLevel: optimizationLevel)
                )
                .AddReferences(References)
                .AddSyntaxTrees(tree);

            if (SaveGeneratedCode)
                GeneratedClassCode = tree.ToString();

            bool isFileAssembly = false;
            Stream codeStream = null;
            if (string.IsNullOrEmpty(OutputAssembly)) 
            {
                codeStream = new MemoryStream(); // in-memory assembly
            }
            else
            {
                codeStream = new FileStream(OutputAssembly, FileMode.Create, FileAccess.Write);
                isFileAssembly = true;
            }

            using (codeStream)
            {
                EmitResult compilationResult = null;
                if (CompileWithDebug)
                {
                    var debugOptions = CompileWithDebug ? DebugInformationFormat.Embedded : DebugInformationFormat.Pdb;
                    compilationResult = compilation.Emit(codeStream,
                        options: new EmitOptions(debugInformationFormat: debugOptions ));
                }
                else 
                    compilationResult = compilation.Emit(codeStream);

                // Compilation Error handling
                if (!compilationResult.Success)
                {
                    var sb = new StringBuilder();
                    foreach (var diag in compilationResult.Diagnostics)
                    {
                        sb.AppendLine(diag.ToString());
                    }

                    ErrorType = ExecutionErrorTypes.Compilation;
                    ErrorMessage = sb.ToString();

                    // no exception here during compilation - return the error
                    SetErrors(new ApplicationException(ErrorMessage),true);  
                    return false;
                }
            }

            if (!noLoad)
            {
                if (!isFileAssembly)
                    Assembly = Assembly.Load(((MemoryStream) codeStream).ToArray());
                else
                    Assembly = Assembly.LoadFrom(OutputAssembly);
            }

            return true;
        }

        /// <summary>
        /// Compiles the source code for a complete class and then loads the
        /// resulting assembly. 
        ///
        /// Loads the generated assembly and sets the `.Assembly` property if successful.
        /// 
        /// If `OutputAssembly` is set, the assembly is compiled to the specified file.
        /// Otherwise the assembly is compiled 'in-memory' and cleaned up by the host
        /// application/runtime.
        ///
        /// Must include parameterless ctor()
        /// </summary>
        /// <param name="codeInputStream">Stream that contains C# code</param>
        /// <param name="noLoad">If set won't load the assembly and just compiles it. Useful only if OutputAssembly is set so you can explicitly load the assembly later.</param>
        /// <returns></returns>
        public bool CompileAssembly(Stream codeInputStream, bool noLoad = false)
        {
            ClearErrors();

            var sourceCode = SourceText.From(codeInputStream);

            var tree = SyntaxFactory.ParseSyntaxTree(sourceCode);

            var compilation = CSharpCompilation.Create(GeneratedClassName + ".cs")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .WithReferences(References)
                .AddSyntaxTrees(tree);

            if (SaveGeneratedCode)
                GeneratedClassCode = tree.ToString();


            if (string.IsNullOrEmpty(OutputAssembly)) // in Memory
            {
                using (var codeStream = new MemoryStream())
                {
                    var compilationResult = compilation.Emit(codeStream);

                    // Compilation Error handling
                    if (!compilationResult.Success)
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (var diag in compilationResult.Diagnostics)
                        {
                            sb.AppendLine(diag.ToString());
                        }

                        ErrorMessage = sb.ToString();
                        SetErrors(new ApplicationException(ErrorMessage));
                        return false;
                    }

                    if (!noLoad)
                        Assembly = Assembly.Load(codeStream.ToArray());
                }
            }
            else
            {
                using (var codeStream = new FileStream(OutputAssembly, FileMode.Create, FileAccess.Write))
                {
                    var compilationResult = compilation.Emit(codeStream);

                    // Compilation Error handling
                    if (!compilationResult.Success)
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (var diag in compilationResult.Diagnostics)
                        {
                            sb.AppendLine(diag.ToString());
                        }

                        ErrorMessage = sb.ToString();
                        SetErrors(new ApplicationException(ErrorMessage));
                        return false;
                    }
                }

                if (!noLoad)
                    Assembly = Assembly.LoadFrom(OutputAssembly);
            }

            return true;
        }

        /// <summary>
        /// This method compiles a class and hands back a
        /// dynamic reference to that class that you can
        /// call members on.
        ///
        /// Must have include parameterless ctor()
        /// </summary>
        /// <param name="code">Fully self-contained C# class</param>
        /// <returns>Instance of that class or null</returns>
        public dynamic CompileClass(string code)
        {
            var type = CompileClassToType(code);
            if (type == null)
                return null;

            // Figure out the class name
            GeneratedClassName = type.Name;
            GeneratedNamespace = type.Namespace;

            return CreateInstance();
        }

        /// <summary>
        /// This method compiles a class and hands back a
        /// dynamic reference to that class that you can
        /// call members on.
        /// 
        /// Must have include parameterless ctor()
        /// </summary>
        /// <param name="code">Fully self-contained C# class</param>
        /// <returns>Instance of that class or null</returns>
        public dynamic CompileClass(Stream code)
        {
            var type = CompileClassToType(code);
            if (type == null)
                return null;

            // Figure out the class name
            GeneratedClassName = type.Name;
            GeneratedNamespace = type.Namespace;

            return CreateInstance();
        }


        /// <summary>
        /// This method compiles a class and hands back a
        /// dynamic reference to that class that you can
        /// call members on.
        /// </summary>
        /// <param name="code">Fully self-contained C# class</param>
        /// <returns>Instance of that class or null</returns>
        public Type CompileClassToType(string code)
        {
            int hash = code.GetHashCode();

            GeneratedClassCode = code;

            if (!CachedAssemblies.ContainsKey(hash))
            {
                if (!CompileAssembly(code))
                    return null;

                CachedAssemblies[hash] = Assembly;
            }
            else
            {
                Assembly = CachedAssemblies[hash];
            }

            // Figure out the class name
            return Assembly.ExportedTypes.First();
        }


        /// <summary>
        /// This method expects a fully self-contained class file
        /// including namespace and using wrapper to compile
        /// from an input stream.
        /// </summary>
        /// <param name="codeStream">Fully self-contained C# class</param>
        /// <returns>A type reference to the generated class</returns>
        public Type CompileClassToType(Stream codeStream)
        {
            int hash = codeStream.GetHashCode();

            
            if (!CachedAssemblies.ContainsKey(hash))
            {
                if (!CompileAssembly(codeStream))
                    return null;

                CachedAssemblies[hash] = Assembly;
            }
            else
            {
                Assembly = CachedAssemblies[hash];
            }

            // Figure out the class name
            return Assembly.ExportedTypes.First();
        }


        /// <summary>
        /// This method creates a class wrapper around a passed in class body.
        ///
        /// The wrapper creates the namespace, adds usings, and creates
        /// the class header based on the property settings for the instance.
        ///
        /// You pass in the 'body' of the class which is properties, constants, methods etc.
        /// to fill out the class
        /// </summary>
        /// <param name="classBody">The class body - methods, properties, constants etc. without a class header</param>
        /// <returns></returns>
        private StringBuilder GenerateClass(string classBody)
        {
            StringBuilder sb = new StringBuilder();

            // Add default usings
            sb.AppendLine(Namespaces.ToString());


            // *** Namespace headers and class definition
            sb.Append("namespace " + GeneratedNamespace + " {" +
                      Environment.NewLine +
                      Environment.NewLine +
                      $"public class {GeneratedClassName}" +
                      Environment.NewLine + "{ " +
                      Environment.NewLine + Environment.NewLine);

            //*** The actual code to run in the form of a full method definition.
            sb.AppendLine();
            sb.AppendLine(classBody);
            sb.AppendLine();

            sb.AppendLine("} " +
                          Environment.NewLine +
                          "}"); // Class and namespace closed

            if (SaveGeneratedCode)
                GeneratedClassCode = sb.ToString();

            return sb;
        }

        private string ParseCodeWithParametersArray(string code, object[] parameters)
        {
            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    code = code.Replace("@" + i, "parameters[" + i + "]");
                }
            }

            return code;
        }

#endregion

#region Refereneces and Namespaces


        /// <summary>
        /// Adds core system assemblies and namespaces for basic operation.
        ///
        /// Any additional references need to be explicitly added.
        ///
        /// Alternatelively use: AddLoadedReferences()
        /// </summary>
        public void AddDefaultReferencesAndNamespaces()
        {


#if NET462
            AddNetFrameworkDefaultReferences();
            AddAssembly(typeof(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException));
#endif
#if NETCORE
            AddNetCoreDefaultReferences();
            AddAssembly(typeof(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException));
#endif

            AddNamespaces(DefaultNamespaces);
        }

        ///// <summary>
        ///// Adds basic System assemblies and namespaces so basic
        ///// operations work.
        ///// </summary>
        ///// <param name="dontLoadLoadedAssemblies">
        ///// In .NET Core it's recommended you add all host assemblies to ensure
        ///// that any referenced assemblies are also accessible in your
        ///// script code. Important as in Core there are many small libraries
        ///// that comprise the core BCL/FCL.
        /////
        ///// For .NET Full this is not as important as most BCL/FCL features
        ///// are automatically pulled by the System and System.Core default
        ///// inclusions.
        /////
        ///// By default host assemblies are loaded.
        ///// </param>
        //[Obsolete("Please use AddDefaultReferencesAndNamespaces() or AddLoadedAssemblies()")]
        //public void AddDefaultReferencesAndNamespaces(bool dontLoadLoadedAssemblies)
        //{
        //    // this library and CodeAnalysis libs
        //    AddAssembly(typeof(ReferenceList));
            
        //    if (!dontLoadLoadedAssemblies)
        //        AddLoadedReferences();

        //    AddNamespaces(DefaultNamespaces);
        //}

        /// <summary>
        /// Explicitly adds all referenced assemblies of the currently executing
        /// process. Also adds default namespaces.
        ///
        /// Useful in .NET Core to ensure that all those little tiny system assemblies
        /// that comprise NetCoreApp.App etc. dependencies get pulled in.
        ///
        /// For full framework this is less important as the base runtime pulls
        /// in all the system and system.core types.
        ///
        /// Alternative: use LoadDefaultReferencesAndNamespaces() and manually add
        ///               
        /// </summary>
        public void AddLoadedReferences()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    if (string.IsNullOrEmpty(assembly.Location)) continue;
                    AddAssembly(assembly.Location);
                }
                catch
                {
                }
            }

            AddAssembly("Microsoft.CSharp.dll"); // dynamic

#if NETCORE
            AddAssemblies(
                "System.Linq.Expressions.dll", // IMPORTANT!
                "System.Text.RegularExpressions.dll" // IMPORTANT!
            );
#endif

            AddNamespaces(DefaultNamespaces);
        }

        public void AddNetFrameworkDefaultReferences()
        {
            AddAssembly("mscorlib.dll");
            AddAssembly("System.dll");
            AddAssembly("System.Core.dll");
            AddAssembly("Microsoft.CSharp.dll");
            AddAssembly("System.Net.Http.dll");

            // this library and CodeAnalysis libs
            AddAssembly(typeof(ReferenceList)); // Scripting Library
        }

        public void AddNetCoreDefaultReferences()
        {
            var rtPath = Path.GetDirectoryName(typeof(object).Assembly.Location) +
                               Path.DirectorySeparatorChar;

            AddAssemblies(
                rtPath + "System.Private.CoreLib.dll",
                rtPath + "System.Runtime.dll",
                rtPath + "System.Console.dll",

                rtPath + "System.Text.RegularExpressions.dll", // IMPORTANT!
                rtPath + "System.Linq.dll",
                rtPath + "System.Linq.Expressions.dll", // IMPORTANT!

                rtPath + "System.IO.dll",
                rtPath + "System.Net.Primitives.dll",
                rtPath + "System.Net.Http.dll",
                rtPath + "System.Private.Uri.dll",
                rtPath + "System.Reflection.dll",
                rtPath + "System.ComponentModel.Primitives.dll",
                rtPath + "System.Globalization.dll",
                rtPath + "System.Collections.Concurrent.dll",
                rtPath + "System.Collections.NonGeneric.dll",
                rtPath + "Microsoft.CSharp.dll"
            );

            // this library and CodeAnalysis libs
            AddAssembly(typeof(ReferenceList)); // Scripting Library
        }

        /// <summary>
        /// Adds an assembly from disk. Provide a full path if possible
        /// or a path that can resolve as part of the application folder
        /// or the runtime folder.
        /// </summary>
        /// <param name="assemblyDll">assembly DLL name. Path is required if not in startup or .NET assembly folder</param>
        public bool AddAssembly(string assemblyDll)
        {
            if (string.IsNullOrEmpty(assemblyDll)) return false;

            var file = Path.GetFullPath(assemblyDll);

            if (!File.Exists(file))
            {
                // check framework or dedicated runtime app folder
                var path = Path.GetDirectoryName(typeof(object).Assembly.Location);
                file = Path.Combine(path, assemblyDll);
                if (!File.Exists(file))
                    return false;
            }

            if (References.Any(r => r.FilePath == file)) return true;

            try
            {
                var reference = MetadataReference.CreateFromFile(file);
                References.Add(reference);
            }
            catch
            {
                return false;
            }

            return true;
        }



        public bool AddAssembly(PortableExecutableReference reference)
        {
            if (References.Any(r => r.FilePath == reference.FilePath))
                return true;

            References.Add(reference);
            return true;
        }

/// <summary>
/// Adds an assembly reference from an existing type
/// </summary>
/// <param name="type">any .NET type that can be referenced in the current application</param>
public bool AddAssembly(Type type)
{
    try
    {
        // *** TODO: need a better way to identify for in memory dlls that don't have location
        if (References.Any(r => r.FilePath == type.Assembly.Location))
            return true;

        if (string.IsNullOrEmpty(type.Assembly.Location))
        {
#if NETCORE
            unsafe
            {
                bool result = type.Assembly.TryGetRawMetadata(out byte* metaData, out int size);
                var moduleMetaData = ModuleMetadata.CreateFromMetadata( (nint) metaData, size);
                var assemblyMetaData = AssemblyMetadata.Create(moduleMetaData);
                References.Add(assemblyMetaData.GetReference());
            }
#else
            return false;
#endif
        }
        else
        {
            var systemReference = MetadataReference.CreateFromFile(type.Assembly.Location);
            References.Add(systemReference);
        }
    }
    catch
    {
        return false;
    }

    return true;
}

        /// <summary>
        /// Add several reference assemblies in batch.
        ///
        /// Useful for use with  Basic.ReferenceAssemblies from Nuget
        /// to load framework dependencies in Core
        ///
        /// Example:
        /// ReferenceAssemblies.Net60
        /// ReferenceAssemblies.NetStandard20 
        /// </summary>
        /// <param name="references">MetaDataReference or PortableExecutiveReference</param>
        public void AddAssemblies(IEnumerable<PortableExecutableReference> references)
        {
            foreach (var reference in references)
            {
                References.Add(reference);
            }
        }


        /// <summary>
        /// Adds a list of assemblies to the References
        /// collection.
        /// </summary>
        /// <param name="assemblies"></param>
        public void AddAssemblies(params string[] assemblies)
        {
            foreach (var file in assemblies)
                AddAssembly(file);
        }



        /// <summary>
        /// Adds a namespace to the referenced namespaces
        /// used at compile time.
        /// </summary>
        /// <param name="nameSpace"></param>
        public void AddNamespace(string nameSpace)
        {
            if (string.IsNullOrEmpty(nameSpace))
            {
                Namespaces.Clear();
                return;
            }

            if (!Namespaces.Contains(nameSpace))
                Namespaces.Add(nameSpace);
        }

        /// <summary>
        /// Adds a set of namespace to the referenced namespaces
        /// used at compile time.
        /// </summary>
        public void AddNamespaces(params string[] namespaces)
        {
            foreach (var ns in namespaces)
            {
                if (!string.IsNullOrEmpty(ns))
                    AddNamespace(ns);
            }
        }

#endregion


#region Errors

        private void ClearErrors()
        {
            LastException = null;
            Error = false;
            ErrorMessage = null;
            ErrorType = ExecutionErrorTypes.None;
        }


        /// <summary>
        /// Error wrapper that assigns exception, errormessage and error flag.
        /// Also throws exception by if ThrowException is enabled
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="noExceptions"></param>
        private void SetErrors(Exception ex, bool noExceptions = false)
        {
            Error = true;
            LastException = ex.GetBaseException();
            ErrorMessage = LastException.Message;

            if (ThrowExceptions && !noExceptions)
                throw LastException;
        }

        public override string ToString()
        {
            return $"CSharpScriptExecution - {ErrorMessage}";
        }

#endregion


#region String Helpers

        /// <summary>
        /// Parses references with this syntax:
        ///
        /// #r assembly.dll
        ///
        /// Each match found is added to the assembly list
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private string ParseReferencesInCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return code;

            if (!code.Contains("#r ") && !code.Contains("using "))
                return code;


            StringBuilder sb = new StringBuilder();
            var snippetLines = GetLines(code);

            foreach (var line in snippetLines)
            {
                if (line.Trim().Contains("#r "))
                {
                    if (AllowReferencesInCode)
                    {
                        string assemblyName = line.Replace("#r ", "").Trim();
                        AddAssembly(assemblyName);
                        sb.AppendLine("// " + line);
                        continue;
                    }
                    sb.AppendLine("// not allowed: " + line);
                    continue;
                }

                if (line.Trim().Contains("using ") && !line.Contains("("))
                {
                    string ns = line.Replace("using ", "").Replace(";", "").Trim();
                    AddNamespace(ns);
                    sb.AppendLine("// " + line);
                    continue;
                }

                sb.AppendLine(line);
            }

            return sb.ToString();
        }



        /// <summary>
        /// Parses a string into an array of lines broken
        /// by \r\n or \n
        /// </summary>
        /// <param name="s">String to check for lines</param>
        /// <param name="maxLines">Optional - max number of lines to return</param>
        /// <returns>array of strings, or null if the string passed was a null</returns>
        public static string[] GetLines(string s, int maxLines = 0)
        {
            if (s == null)
                return null;

            s = s.Replace("\r\n", "\n");

            if (maxLines < 1)
                return s.Split(new char[] { '\n' });

            return s.Split(new char[] { '\n' }, maxLines);
        }



        /// <summary>
        /// Returns 0 offset line number where matched line lives
        /// </summary>
        /// <param name="code"></param>
        /// <param name="matchLine"></param>
        /// <returns></returns>
        public static int FindCodeLine(string code, string matchLine)
        {
            matchLine = matchLine.Trim();

            var lines = CSharpScriptExecution.GetLines(code);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Trim() == matchLine.Trim())
                    return i;
            }

            return -1;
        }


#endregion

#region Reflection Helpers


        /// <summary>
        /// Helper method to invoke a method on an object using Reflection
        /// </summary>
        /// <param name="instance">An object instance. null uses ObjectInstance property if set.</param>
        /// <param name="method">The method name as a string</param>
        /// <param name="parameters">a variable list of parameters to pass</param>
        /// <exception cref="ArgumentNullException">Throws if the instance is null</exception>
        /// <returns>result from method call or null.</returns>
        public object InvokeMethod(object instance, string method, params object[] parameters)
        {
            ClearErrors();

            if (instance == null)
                instance = ObjectInstance;

            if (instance == null)
                throw new ArgumentNullException("Can't invoke Script Method: Instance not available.");

            if (ThrowExceptions)
            {
                return instance.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, instance, parameters);
            } 

            try
            {
                return instance.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, instance, parameters);
            }
            catch (Exception ex)
            {
                SetErrors(ex);
                ErrorType = ExecutionErrorTypes.Runtime;
            }

            return null;
        }

        
        /// <summary>
        /// Creates an instance of the object specified
        /// by the GeneratedNamespace and GeneratedClassName
        /// in the currently active, compiled assembly
        ///
        /// Sets the ObjectInstance member which is returned
        /// </summary>
        /// <param name="force">If true force to create a new instance regardless whether an instance is already loaded</param>
        /// <returns>Instance of the class or null on error</returns>
        public object CreateInstance(bool force = false)
        {
            ClearErrors();

            if (ObjectInstance != null && !force)
                return ObjectInstance;

            try
            {
                ObjectInstance = Assembly.CreateInstance(GeneratedNamespace + "." + GeneratedClassName);
                return ObjectInstance;
            }
            catch (Exception ex)
            {
                SetErrors(ex);
            }

            return null;
        }

        /// <summary>
        /// Generates a hashcode for a block of code
        /// in combination with the compiler mode.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private int GenerateHashCode(string code)
        {
            return (code).GetHashCode();
        }

        /// <summary>
        /// Returns path of the runtime or in self contained install local folder
        /// </summary>
        /// <returns></returns>
        private string GetRuntimePath()
        {
            return Path.GetDirectoryName(typeof(object).Assembly.Location);
        }

#endregion


        /// <summary>
        /// List of default namespaces that are added when adding default references and namespaces
        /// </summary>
        public static string[] DefaultNamespaces =
        {
            "System", "System.Text", "System.Reflection", "System.IO", "System.Net", "System.Net.Http",
            "System.Collections", "System.Collections.Generic", "System.Collections.Concurrent",
            "System.Text.RegularExpressions", "System.Threading.Tasks", "System.Linq"
        };
    }

    public enum ExecutionErrorTypes
    {
        Compilation,
        Runtime,
        None
    }
}
