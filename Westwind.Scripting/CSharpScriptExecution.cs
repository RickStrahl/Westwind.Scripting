using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


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
        /// Filename for the output assembly to generate. If empty the
        /// assembly is generated in memory (dynamic filename managed by
        /// the .NET runtime)
        /// </summary>
        public string OutputAssembly { get; set; }


        /// <summary>
        /// Last generated code for this code snippet
        /// </summary>
        public string GeneratedClassCode { get; set; }

        public string GeneratedClassCodeWithLineNumbers => Utils.GetTextWithLineNumbers(GeneratedClassCode);


        public string GeneratedNamespace { get; set; } = "__ScriptExecution";

        public string GeneratedClassName { get; set; } = "__" + Utils.GenerateUniqueId();


        /// <summary>
        /// Determines whether GeneratedCode will be set with the source
        /// code for the full generated class
        /// </summary>
        public bool SaveGeneratedCode { get; set; }

        /// <summary>
        /// If true throws exceptions rather than failing silently
        /// and returning error state. Default is false.
        /// </summary>
        public bool ThrowExceptions { get; set; }


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



        public CSharpScriptExecution()
        {

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

            object instance = ObjectInstance;

            if (instance == null)
            {
                int hash = GenerateHashCode(code);

                var sb = GenerateClass(code);

                if (!CachedAssemblies.ContainsKey(hash))
                {
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

                object tempInstance = CreateInstance();
                if (tempInstance == null)
                    return null;
            }

            var result = InvokeMethod(ObjectInstance, methodName, parameters);
            return result;
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
            var result = ExecuteMethod(code, methodName,  parameters);

            if (result is TResult)
                return (TResult)result;

            return default;
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
        public async Task<TResult> ExecuteMethodAsync<TResult>(string code, string methodName, params object[] parameters)
        {
            // this result will be a task of object (async method called)
            var taskResult = ExecuteMethod(code, methodName, parameters) as Task<TResult>;

            
            if (taskResult == null)
                return default;

            var result = await taskResult;

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
                                 "{" +
                                 code +
                                 Environment.NewLine +
                                 // force a return value - compiler will optimize this out
                                 // if the code provides a return
                                 "return null;" +
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
                                 "{" +
                                 code +
                                 Environment.NewLine +
                                 // force a return value - compiler will optimize this out
                                 // if the code provides a return
                                 "return default;" +
                                 Environment.NewLine +
                                 "}",
                "ExecuteCode", parameters);

            return (TResult)result;
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

            var objTask = ExecuteMethod("public async Task<object> ExecuteCode(params object[] parameters)" +
                                        Environment.NewLine +
                                        "{" +
                                        code +
                                        Environment.NewLine +
                                        // force a return value - compiler will optimize this out
                                        // if the code provides a return
                                        "return default;" +
                                        Environment.NewLine +
                                        "}",
                "ExecuteCode", parameters);

            if (objTask == null || !(objTask is Task<object>))
                return Task.FromResult<object>(null);

            return (Task<object>) objTask;
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

            var objTask = ExecuteMethod($"public async Task<{typeName}> ExecuteCode(params object[] parameters)" +
                                        Environment.NewLine +
                                        "{" +
                                        code +
                                        Environment.NewLine +
                                        // force a return value - compiler will optimize this out
                                        // if the code provides a return
                                        "return default;" +
                                        Environment.NewLine +
                                        "}",
                "ExecuteCode", parameters);

            if (objTask == null || !(objTask is Task<TResult>))
                return Task.FromResult<TResult>(default);

            return (Task<TResult>) objTask;
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
        public Task<TResult> ExecuteCodeAsync<TResult,TModelType >(string code, TModelType model)
        {
            ClearErrors();

            var typeName = typeof(TModelType).FullName;

            var res = ExecuteMethodAsync<TResult>($"public async Task<object> ExecuteCode({typeName} Model)" +
                                        Environment.NewLine +
                                        "{" +
                                        code +
                                        Environment.NewLine +
                                        // force a return value - compiler will optimize this out
                                        // if the code provides a return
                                        "return null;" +
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
        /// Looks in cached assemblies
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

        #region Compilation and Code Generation

        /// <summary>
        /// Compiles and runs the source code for a complete assembly.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public bool CompileAssembly(string source)
        {
            ClearErrors();

            var tree = SyntaxFactory.ParseSyntaxTree(source.Trim());

            var compilation = CSharpCompilation.Create(GeneratedClassName + ".cs")
                .WithOptions(
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
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

                Assembly = Assembly.LoadFrom(OutputAssembly);
            }

            return true;
        }

        /// <summary>
        /// This method compiles a class and hands back a
        /// dynamic reference to that class that you can
        /// call members on.
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

                CachedAssemblies[code.GetHashCode()] = Assembly;
            }
            else
            {
                Assembly = CachedAssemblies[hash];
            }

            // Figure out the class name
            return Assembly.ExportedTypes.First();
        }

        private StringBuilder GenerateClass(string code)
        {
            StringBuilder sb = new StringBuilder();

            //*** Program lead in and class header
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
            sb.AppendLine(code);
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

#region Configuration Methods

/// <summary>
/// Adds basic System assemblies and namespaces so basic
/// operations work.
/// </summary>
/// <param name="dontAddLoadedAssemblies">
/// In .NET Core it's recommended you add all host assemblies to ensure
/// that any referenced assemblies are also accessible in your
/// script code. Important as in Core there are many small libraries
/// that comprise the core BCL/FCL.
///
/// For .NET Full this is not as important as most BCL/FCL features
/// are automatically pulled by the System and System.Core default
/// inclusions.
///
/// By default host assemblies are loaded.
/// </param>
public void AddDefaultReferencesAndNamespaces(bool dontLoadLoadedAssemblies = false)
{
    AddAssembly(typeof(ReferenceList));
    AddAssembly(typeof(Microsoft.CodeAnalysis.CSharpExtensions));
#if NETCORE
      AddAssembly(typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo));
#endif
#if NET462
            AddAssembly(typeof(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException));
#endif

            var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);

    //var files = Directory.GetFiles(runtimePath, "*.dll");
    //foreach (var file in files)
    //{
    //    AddAssembly(file);
    //}


    if (!dontLoadLoadedAssemblies)
        AddLoadedAssemblies();


    AddNamespaces("System",
        "System.Text",
        "System.Reflection",
        "System.IO",
        "System.Net",
        "System.Collections",
        "System.Collections.Generic",
        "System.Collections.Concurrent",
        "System.Text.RegularExpressions",
        "System.Threading.Tasks",
        "System.Linq");

    }

        /// <summary>
        /// Explicitly adds all referenced assemblies of the currently executing
        /// process.
        ///
        /// Useful in .NET Core to ensure that all those little tiny system assemblies
        /// that comprise NetCoreApp.App etc. dependencies get pulled in.
        ///
        /// For full framework this is less important as the base runtime pulls
        /// in all the system and system.core types.
        /// </summary>
        public void AddLoadedAssemblies()
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
            foreach(var reference in references)
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


        public bool AddAssembly(PortableExecutableReference reference)
        {
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
                var systemReference = MetadataReference.CreateFromFile(type.Assembly.Location);
                References.Add(systemReference);
            }
            catch
            {
                return false;
            }
            return true;
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
        }

        private void SetErrors(Exception ex)
        {
            Error = true;
            LastException = ex.GetBaseException();
            ErrorMessage = LastException.Message;

            if (ThrowExceptions)
                throw LastException;
        }

#endregion

#region Reflection Helpers


        /// <summary>
        /// Helper method to invoke a method on an object using Reflection
        /// </summary>
        /// <param name="instance">An object instance. You can pass script.ObjectInstance</param>
        /// <param name="method">The method name as a string</param>
        /// <param name="parameters">a variable list of parameters to pass</param>
        /// <returns></returns>
        public object InvokeMethod(object instance, string method, params object[] parameters)
        {
            ClearErrors();

            // *** Try to run it
            try
            {
                // *** Just invoke the method directly through Reflection
                return instance.GetType()
                    .InvokeMember(method, BindingFlags.InvokeMethod, null, instance, parameters);
            }
            catch (Exception ex)
            {
                SetErrors(ex);
            }

            return null;
        }

        /// <summary>
        /// Creates an instance of the object specified
        /// by the GeneratedNamespace and GeneratedClassName.
        ///
        /// Sets the ObjectInstance member which is returned
        /// </summary>
        /// <returns>Instance of the class or null on error</returns>
        public object CreateInstance()
        {
            ClearErrors();

            if (ObjectInstance != null)
                return ObjectInstance;

            // *** Create an instance of the new object

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

#endregion


        ///// <summary>
        /////  cleans up the compiler
        ///// </summary>
        //public void Dispose()
        //{
        //    Compiler?.Dispose();
        //}
    }
}
