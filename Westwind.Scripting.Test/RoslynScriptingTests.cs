using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Westwind.Scripting.Test
{
    [TestClass]
    public class RoslynScriptingTests
    {
        [TestMethod]
        public async Task RoslynCompileAndRun()
        {
            var source = @"using System;
using System.Text;
using System.Reflection;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

namespace __ScriptExecution {
public class __Executor { 

    public async Task<string> GetJsonFromAlbumViewer(int id)
    {
        Console.WriteLine(""Starting..."");

        var wc = new WebClient();
        var uri = new Uri(""https://albumviewer.west-wind.com/api/album/"" + id);

        string json = ""123"";
        try{
            Console.WriteLine(""Retrieving..."");
            json =  await wc.DownloadStringTaskAsync(uri);

            Console.WriteLine(""JSON retrieved..."");
        }
        catch(Exception ex) {
            Console.WriteLine(""ERROR in method: "" + ex.Message);
        }

        Console.WriteLine(""All done in method"");

        return json;
    }

} }";

#if NETFRAMEWORK
            AddNetFrameworkDefaultReferences();
#else
            AddNetCoreDefaultReferences();
            AddAssembly(typeof(System.Net.WebClient));
#endif

            var tree = SyntaxFactory.ParseSyntaxTree(source.Trim());
            var compilation = CSharpCompilation.Create("Executor.cs")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release))
                .WithReferences(References)
                .AddSyntaxTrees(tree);

            string errorMessage = null;
            Assembly assembly = null;

            bool isFileAssembly = false;
            Stream codeStream = null;
            using (codeStream = new MemoryStream())
            {
                // Actually compile the code
                EmitResult compilationResult = null;
                compilationResult = compilation.Emit(codeStream);

                // Compilation Error handling
                if (!compilationResult.Success)
                {
                    var sb = new StringBuilder();
                    foreach (var diag in compilationResult.Diagnostics)
                    {
                        sb.AppendLine(diag.ToString());
                    }
                    errorMessage = sb.ToString();

                    Assert.IsTrue(false, errorMessage);

                    return;
                }
                assembly = Assembly.Load(((MemoryStream)codeStream).ToArray());
            }

            dynamic instance = assembly.CreateInstance("__ScriptExecution.__Executor");

            var json = await instance.GetJsonFromAlbumViewer(37);

            Console.WriteLine(json);
        }


        [TestMethod]
        public async Task RoslynCSharpScriptingTest()
        {
            var code = @"
#r ""System.Console.dll""

using System;
//dynamic Model = this;

Console.WriteLine(Message);

Console.WriteLine(""Hello World #2"");
return ""OK"";
";


            var options = ScriptOptions.Default;

            options.AddReferences(
                typeof(Console).Assembly,
                typeof(System.Net.WebClient).Assembly,
                typeof(System.Uri).Assembly,
                typeof(System.Dynamic.DynamicObject).Assembly, // System.Code
                typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly,
                typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly, // Microsoft.CSharp
                typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly);

            options.AddReferences(MetadataReference.CreateFromFile(typeof(System.Uri).Assembly.Location));
                
            options.AddImports("System",
                "System.Dynamic", "System.IO", "System.Text",
                "System.Text.RegularExpressions");


            //var script = CSharpScript.Create(code, options, model.GetType());

            //ScriptState<
            //var result = await script.RunAsync(model);

            ScriptState<object> result;
            try
            {
                var model = new Test.ScriptTest { Message = "Hello World", Name = "Rick" };
                
                result = await CSharpScript.RunAsync(code, options, model);
                Console.WriteLine(result.ReturnValue);
            }
            catch (CompilationErrorException ex)
            {
                Console.WriteLine(code);

                var sb = new StringBuilder();
                foreach (var err in ex.Diagnostics)
                    sb.AppendLine(err.ToString());

                Console.WriteLine(sb.ToString());
            }
            // Runtime Errors
            catch (Exception ex)
            {
                Console.WriteLine(code);
                Console.WriteLine(ex.ToString());
            }

        }


        [TestMethod]
        public async Task RoslynCSharpScripting2Test()
        {
            var code = @"
using System;

dynamic name = ""rick"";

//Console.WriteLine(""Starting..."");

var wc = new System.Net.WebClient();
var uri = new Uri(""https://albumviewer.west-wind.com/api/album/"" + Id);

string json = ""123"";
try{
    //Console.WriteLine(""Retrieving..."");
    //json =  await wc.DownloadStringTaskAsync(uri);

    //Console.WriteLine(""JSON retrieved..."");
}
catch(Exception ex) {
    Console.WriteLine(""ERROR in method: "" + ex.Message);
}

//Console.WriteLine(""All done in method"");

return json;
";

            var options = ScriptOptions.Default
                .WithReferences(typeof(System.Uri).Assembly,
                    typeof(System.Console).Assembly,
                    typeof(System.Text.RegularExpressions.Match).Assembly,
                    typeof(System.Net.WebClient).Assembly,
                    typeof(System.Dynamic.DynamicObject).Assembly, // System.Code
                    typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly,
                    typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly, // Microsoft.CSharp
                    typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly)
                .WithImports("System", "System.Net",
                    "System.Dynamic", "System.IO", "System.Text",
                    "System.Text.RegularExpressions");

            ScriptState<string> result = null;
            try
            {
                var model = new ScriptTest { Message = "Hello World", Name = "Rick", Id=37 };
                result = await CSharpScript.RunAsync<string>(code, options: options, globals: model);
                Console.WriteLine(result.ReturnValue);
            }
            catch (CompilationErrorException ex)
            {
                Console.WriteLine(code);

                var sb = new StringBuilder();
                foreach (var err in ex.Diagnostics)
                    sb.AppendLine(err.ToString());

                Console.WriteLine(sb.ToString());
            }
            // Runtime Errors
            catch (Exception ex)
            {
                Console.WriteLine(code);
                Console.WriteLine(ex.ToString());
            }
        }


        public HashSet<PortableExecutableReference> References { get; set; } =
            new HashSet<PortableExecutableReference>();

        public bool AddAssembly(Type type)
        {
            try
            {
                if (References.Any(r => r.FilePath == type.Assembly.Location))
                    return true;

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


        public void AddNetFrameworkDefaultReferences()
        {
            AddAssembly("mscorlib.dll");
            AddAssembly("System.dll");
            AddAssembly("System.Core.dll");
            AddAssembly("Microsoft.CSharp.dll");
            AddAssembly("System.Net.Http.dll");

            AddAssembly(typeof(Microsoft.CodeAnalysis.CSharpExtensions));

            // this library and CodeAnalysis libs
            AddAssembly(typeof(ReferenceList)); // Scripting Library
        }

        public void AddAssemblies(params string[] assemblies)
        {
            foreach (var file in assemblies)
                AddAssembly(file);
        }


        public void AddNetCoreDefaultReferences()
        {

            AddAssemblies(
                "System.Private.CoreLib.dll",
                "System.Runtime.dll",

                "System.Console.dll",
                "System.Linq.dll",
                "System.Linq.Expressions.dll", // IMPORTANT!
                "System.Text.RegularExpressions.dll", // IMPORTANT!
                "System.IO.dll",
                "System.Net.Primitives.dll",
                "System.Net.Http.dll",
                "System.Private.Uri.dll",
                "System.Reflection.dll",
                "System.ComponentModel.Primitives.dll",

                "System.Collections.Concurrent.dll",
                "System.Collections.NonGeneric.dll",

                "Microsoft.CSharp.dll",
                "Microsoft.CodeAnalysis.dll",
                "Microsoft.CodeAnalysis.CSharp.dll"
            );

            // this library and CodeAnalysis libs
            AddAssembly(typeof(ReferenceList)); // Scripting Library

        }


    }
}
