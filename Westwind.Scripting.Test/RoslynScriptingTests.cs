using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Westwind.Utilities;

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

        dynamic name = ""Rick"";
        Console.WriteLine(name);

        var s = Westwind.Utilities.StringUtils.ExtractString(""132123123"",""13"",""23"");
        return json;
    }

} }";

#if NETFRAMEWORK
            AddNetFrameworkDefaultReferences();
#else
            AddNetCoreDefaultReferences();
            AddAssembly("System.Net.WebClient.dll");
#endif

            AddAssembly(typeof(Westwind.Utilities.StringUtils));

            var tree = SyntaxFactory.ParseSyntaxTree(source.Trim());
            var compilation = CSharpCompilation.Create("Executor.cs")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release))
                //.WithReferences(Basic.Reference.Assemblies.Net60.All)   // NUGET Package for all framework references
                .WithReferences(References)
                .AddSyntaxTrees(tree);

            string errorMessage = null;
            Assembly assembly = null;

        
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
            var westwindAssemblyPath = Path.GetFullPath("Westwind.Utilities.dll");

            var code = $@"
#r ""{westwindAssemblyPath}""
Console.WriteLine(Message);

dynamic name = ""Rick"";
Console.WriteLine(name);

Console.WriteLine(""Hello World #2"");

// External Reference
Console.WriteLine(StringUtils.Replicate(""42"",10));

// Uncommon (deprecated) type - still works
var wc = new System.Net.WebClient();
var json = wc.DownloadString(new Uri(""https://albumviewer.west-wind.com/api/album/37""));

return ""OK"";
";          
            var options =
                ScriptOptions.Default
                    .AddReferences(typeof(StringUtils).Assembly)
                    .AddImports("System",
                "System.IO", "System.Text",
                "System.Text.RegularExpressions", "Westwind.Utilities");


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
        }

        public void AddAssemblies(params string[] assemblies)
        {
            foreach (var file in assemblies)
                AddAssembly(file);
        }


        public void AddNetCoreDefaultReferences()
        {
            var rtPath = Path.GetDirectoryName(typeof(object).Assembly.Location) +
                         Path.DirectorySeparatorChar;

            AddAssemblies(
                rtPath + "System.Private.CoreLib.dll",
                rtPath + "System.Runtime.dll",
                rtPath + "System.Console.dll",
                rtPath + "netstandard.dll",

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
        }


    }
}
