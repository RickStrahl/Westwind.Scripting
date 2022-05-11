using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
    public class CSharpScriptExecution : IDisposable
    {
        /// <summary>
        /// Internal list of assemblies that are cached for snippets of the same type.
        /// List holds a list of cached assemblies with a hash code for the code executed as
        /// the key.
        /// </summary>
        protected static ConcurrentDictionary<int, Assembly> CachedAssemblies = new ConcurrentDictionary<int, Assembly>();

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

        #region Compiler Settings

        public ScriptCompilerModes CompilerMode { get; set; } = ScriptCompilerModes.Roslyn;

        //protected ICodeCompiler Compiler
        //{
        //    get
        //    {
        //        if (_compiler == null)
        //        {
        //            //_compiler = new CSharpCodeProvider();
        //            //if (CompilerMode == ScriptCompilerModes.Roslyn)
        //            //    _compiler = new CSharpCodeProvider();
        //            //    _compiler = CSharpCodeProvider.CreateProvider("CSharp") as CSharpCodeProvider;
        //            //else
        //            //    _compiler = new Microsoft.CSharp.CSharpCodeProvider();

        //            _compiler = new CSharpCodeProvider().CreateCompiler(); // CodeDomProvider.CreateProvider("CSharp");
        //        }
        //        return _compiler;
        //    }
        //}
        //private ICodeCompiler _compiler;


        protected CodeDomProvider Compiler
        {
            get
            {
                if (_compiler == null)
                {

                    if (CompilerMode == ScriptCompilerModes.Roslyn)
                        _compiler = new Microsoft.CodeDom.Providers.DotNetCompilerPlatform.CSharpCodeProvider();
                    else
                        _compiler = new Microsoft.CSharp.CSharpCodeProvider();

                    //_compiler = new CSharpCodeProvider().CreateCompiler(); // CodeDomProvider.CreateProvider("CSharp");
                }
                return _compiler;
            }
        }
        private CodeDomProvider _compiler;

        /// <summary>
        /// Internal Compiler Parameters
        /// </summary>
        protected CompilerParameters Parameters { get; } = new CompilerParameters();

        /// <summary>
        /// Compiler Results from the Compilation Process with
        /// detailed error information if an error occurs.
        /// </summary>
        public CompilerResults CompilerResults { get; set; }


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
        public object ExecuteMethod(string code,string methodName, params object[] parameters)
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
        public Task<TResult> ExecuteMethodAsync<TResult>(string code, string methodName, params object[] parameters)
        {
            object result = ExecuteMethod(code, methodName, parameters);
            if (result == null)
                return Task.FromResult(default(TResult));

            return  (Task<TResult>) result;
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

            code = ParseCodeNumberedParameters(code, parameters);
            return ExecuteCode("return " + code + ";", parameters);
        }

        /// <summary>
        /// Evaluates a single value or expression that returns a value.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public Task<object> EvaluateAsync(string code, params object[] parameters)
        {
            ClearErrors();

            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Can't evaluate empty code. Please pass code.");

            code = ParseCodeNumberedParameters(code, parameters);
            return ExecuteCodeAsync("return " + code + ";", parameters);
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

            code = ParseCodeNumberedParameters(code, parameters);

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
        /// <returns></returns>
        public Task<object> ExecuteCodeAsync(string code, params object[] parameters)
        {
            ClearErrors();

            code = ParseCodeNumberedParameters(code, parameters);

            return ExecuteMethod("public async Task<object> ExecuteCode(params object[] parameters)" +
                                 Environment.NewLine +
                                 "{" +
                                 code +
                                 Environment.NewLine +
                                 // force a return value - compiler will optimize this out
                                 // if the code provides a return
                                 "return null;" +
                                 Environment.NewLine +
                                 "}",
                "ExecuteCode", parameters) as Task<object>;
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
        public Task<TResult> ExecuteCodeFromAssemblyAsync<TResult>(string code, Assembly assembly, params object[] parameters)
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
        /// This method compiles a class and hands back a
        /// dynamic reference to that class that you can
        /// call members on.
        /// </summary>
        /// <param name="code">Fully self-contained C# class</param>
        /// <returns>Instance of that class or null</returns>
        public dynamic CompileClass(string code)
        {
            var type = CompileClassToType(code);
            if ( type == null)
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
            return  Assembly.ExportedTypes.First();
        }

        /// <summary>
        /// Compiles and runs the source code for a complete assembly.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public bool CompileAssembly(string source)
        {
            ClearErrors();

            if (OutputAssembly == null)
                Parameters.GenerateInMemory = true;
            else
            {
                Parameters.OutputAssembly = OutputAssembly;
                Parameters.GenerateInMemory = false;
            }

            foreach (var assembly in References)
            {
                Parameters.ReferencedAssemblies.Add(assembly);
            }

            CompilerResults = Compiler.CompileAssemblyFromSource(Parameters, source);

            if (CompilerResults.Errors.HasErrors)
            {
                // *** Create Error String
                ErrorMessage = CompilerResults.Errors.Count + " Errors:";
                for (int x = 0; x < CompilerResults.Errors.Count; x++)
                    ErrorMessage = ErrorMessage + "\r\nLine: " + CompilerResults.Errors[x].Line + " - " +
                                   CompilerResults.Errors[x].ErrorText;

                SetErrors(new ApplicationException(ErrorMessage));

                return false;
            }

            Assembly = CompilerResults.CompiledAssembly;

            return true;
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

        private string ParseCodeNumberedParameters(string code, object[] parameters)
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
        /// Adds an assembly to be added to the compilation context.
        /// </summary>
        /// <param name="assemblyDll">assembly DLL name. Path is required if not in startup or .NET assembly folder</param>
        public void AddAssembly(string assemblyDll)
        {
            if (string.IsNullOrEmpty(assemblyDll))
            {
                References.Clear();
                return;
            }

            References.Add(assemblyDll);
        }

        /// <summary>
        /// Adds an assembly reference from an existing type
        /// </summary>
        /// <param name="type">any .NET type that can be referenced in the current application</param>
        public void AddAssembly(Type type)
        {
            AddAssembly(type.Assembly.Location);
        }

        /// <summary>
        /// Adds a list of assemblies to the References
        /// collection.
        /// </summary>
        /// <param name="assemblies"></param>
        public void AddAssemblies(params string[] assemblies)
        {
            foreach (var assembly in assemblies)
                AddAssembly(assembly);
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

        /// <summary>
        /// Adds basic System assemblies and namespaces so basic
        /// operations work.
        /// </summary>
        public void AddDefaultReferencesAndNamespaces()
        {
            AddAssembly("System.dll");
            AddAssembly("System.Core.dll");
            AddAssembly("Microsoft.CSharp.dll");

            AddNamespace("System");
            AddNamespace("System.Text");
            AddNamespace("System.Reflection");
            AddNamespace("System.IO");
            AddNamespace("System.Net");
            AddNamespace("System.Threading.Tasks");
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
            return (code + CompilerMode).GetHashCode();
        }

        #endregion


        /// <summary>
        ///  cleans up the compiler
        /// </summary>
        public void Dispose()
        {
            Compiler?.Dispose();
        }
    }

    /// <summary>
    ///
    /// </summary>
    public enum ScriptCompilerModes
    {
        /// <summary>
        /// Uses the built-in C# 5.0 compiler. Using this compiler
        /// requires no additional assemblies
        /// </summary>
        Classic,

        /// <summary>
        /// Uses the Roslyn Compiler. When this flag is set make
        /// sure that the host project includes this package:
        ///
        /// Microsoft.CodeDom.Providers.DotNetCompilerPlatform
        ///
        /// This adds a the compiler binaries to your application
        /// so be aware of the overhead.
        /// </summary>
        Roslyn
    }
}
