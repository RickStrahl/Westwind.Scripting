using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Westwind.Utilities;

namespace Westwind.Scripting.Test
{
    [TestClass]
    public class SimpleCodeExecutionTests
    {
        [TestMethod]
        public void ExecuteCodeSnippetWithResult()
        {

            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
                GeneratedNamespace = "ScriptExecutionTesting",
                GeneratedClassName = "MyTest",
            //    OutputAssembly = @"/temp/test2.dll"  // optionally save the assembly for later use or binary storage
            };
            script.AddDefaultReferencesAndNamespaces();

            var code = $@"
// Check some C# 6+ lang features
var s = new {{ name = ""Rick""}}; // anonymous types
Console.WriteLine(s?.name);       // null propagation

int num1 = (int)parameters[0];
int num2 = (int)parameters[1];

// string templates
var result = $""{{num1}} + {{num2}} = {{(num1 + num2)}}"";
Console.WriteLine(result);

return result;
";

            string result = script.ExecuteCode(code, 10, 20) as string;

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsTrue(result.Contains(" = 30"));

            result = script.ExecuteCode(code, 15, 10) as string;

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);


            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsTrue(result.Contains(" = 25"));

            script = new CSharpScriptExecution() {SaveGeneratedCode = true, GeneratedClassName = "MyTest"};
            script.AddDefaultReferencesAndNamespaces();

            result = script.ExecuteCode(code, 4, 10) as string;
            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);


            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsTrue(result.Contains(" = 14"));

        }


        [TestMethod]
        public async Task ExecuteCodeAsyncWithResult()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
                GeneratedNamespace = "ScriptExecutionTesting",
                GeneratedClassName = "MyTest"
            };
            script.AddDefaultReferencesAndNamespaces();

            //script.AddAssembly("Westwind.Utilities.dll");
            //script.AddNamespace("Westwind.Utilities");

            var code = $@"
// Check some C# 6+ lang features
var s = new {{ name = ""Rick""}}; // anonymous types
Console.WriteLine(s?.name);       // null propagation

int num1 = (int)parameters[0];
int num2 = (int)parameters[1];

// Some Async code
await Task.Delay(10);

// string templates
var result = $""{{num1}} + {{num2}} = {{(num1 + num2)}}"";
Console.WriteLine(result);

return result;
";

            string result = await script.ExecuteCodeAsync<string>(code, 10, 20) as string;

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsTrue(result.Contains(" = 30"));


        }

        [TestMethod]
        public void EvaluateTest()
        {
            var script = new CSharpScriptExecution() {SaveGeneratedCode = true,};
            script.AddDefaultReferencesAndNamespaces();

            // Full syntax
            //object result = script.Evaluate("(decimal) parameters[0] + (decimal) parameters[1]", 10M, 20M);

            // Numbered parameter syntax is easier
            var result = script.Evaluate<decimal>("(decimal) @0 + (decimal) @1", 10M, 20M);

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCode);

            Assert.IsFalse(script.Error, script.ErrorMessage);
        }



        [TestMethod]
        public async Task EvaluateAsyncTest()
        {
            var script = new CSharpScriptExecution() {SaveGeneratedCode = true,};
            script.AddDefaultReferencesAndNamespaces();

            // Full syntax
            //object result = script.Evaluate("(decimal) parameters[0] + (decimal) parameters[1]", 10M, 20M);

            // Numbered parameter syntax is easier
            var result = await script.EvaluateAsync<decimal>(
                $@"await Task.Run( async ()=> {{
    await Task.Delay(1);
    return (decimal) @0 + (decimal) @1;
}})", 10M, 20M);

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
        }



        [TestMethod]
        public void ExecuteCodeSnippetWithoutResult()
        {
            var script = new CSharpScriptExecution() {SaveGeneratedCode = true,};
            script.AddDefaultReferencesAndNamespaces();

            string result =
                script.ExecuteCode("Console.WriteLine($\"Time is: {DateTime.Now}\");", null) as string;

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCode);

            Assert.IsFalse(script.Error, script.ErrorMessage);
        }


        [TestMethod]
        public async Task ExecuteCodeSnippetWithTypedResultAsync()
        {
            var script = new CSharpScriptExecution() {SaveGeneratedCode = true,};
            script.AddDefaultReferencesAndNamespaces();

            string code = @"
await Task.Run(async () => {
    {
        Console.WriteLine($""Time before: {DateTime.Now.ToString(""HH:mm:ss:fff"")}"");
        await Task.Delay(20);
        Console.WriteLine($""Time after: {DateTime.Now.ToString(""HH:mm:ss:fff"")}"");
    }
});

return $""Done at {DateTime.Now.ToString(""HH:mm:ss:fff"")}"";
";


            string result = await script.ExecuteCodeAsync<string>(code, null);

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsTrue(result.StartsWith("Done at"));
        }


        [TestMethod]
        public async Task ExecuteCodeWithDynamicModelAsync()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            script.AddDefaultReferencesAndNamespaces();

            script.AddAssembly(typeof(ScriptTest));
            script.AddNamespace("Westwind.Scripting.Test");

            var model = new ScriptTest() { Message = "Hello World " };

            
            var code = @"
dynamic Model = @0;

await Task.Delay(10); // test async

string result =  Model.Message +  "" "" + DateTime.Now.ToString();
return result;
";


            string execResult = await script.ExecuteCodeAsync<string>(code, model);

            Console.WriteLine($"Result: {execResult}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
        }

        [TestMethod]
        public async Task ExecuteCodeWithDynamicModelWithReferencesAsync()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
                AllowReferencesInCode = true
            };
            script.AddDefaultReferencesAndNamespaces();
            
            // do this in the code snippet
            //script.AddAssembly(typeof(ScriptTest));
            //script.AddNamespace("Westwind.Scripting.Test");

            var model = new ScriptTest()
            {
                Message = "Hello World ",
            };


            var code = @"
#r Westwind.ScriptExecution.Test.dll
using Westwind.Scripting.Test;

dynamic Model = @0;

await Task.Delay(10); // test async

string result =  Model.Message +  "" "" + DateTime.Now.ToString();
return result;
";


            string execResult = await script.ExecuteCodeAsync<string>(code, model);

            Console.WriteLine($"Result: {execResult}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
        }




        [TestMethod]
        public async Task ExecuteCodeWithTypedModelAsync()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            script.AddDefaultReferencesAndNamespaces();
            script.AddAssembly(typeof(ScriptTest));
            script.AddNamespace("Westwind.Scripting.Test");

            var model = new ScriptTest() { Message = "Hello World " };


            var code = @"
await Task.Delay(10); // test async

string result =  Model.Message +  "" "" + DateTime.Now.ToString();
return result;
";


            string execResult = await script.ExecuteCodeAsync<string, ScriptTest>(code, model);

            Console.WriteLine($"Result: {execResult}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
        }

        [TestMethod]
        public void ExecuteMethodTest()
        {
            var exec = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
                AllowReferencesInCode = true
            };
            exec.AddDefaultReferencesAndNamespaces();
            exec.AddAssembly("System.Net.WebClient.dll");

            string code = $@"
#r ReferenceTest.dll

public string HelloWorld(string name)
{{
    var wc = new System.Net.WebClient();
    wc.DownloadString(new Uri(""https://west-wind.com""));

    string result = $""Hello {{name}}. Time is: {{DateTime.Now}}."";

    var first = new int[] {{ 1,2,3,4,5 }}.First();
    Console.WriteLine(first);
    
    dynamic name2 = ""RIck"";
    Console.WriteLine(name2);

    var t = new ReferenceTest.Test();
    var hello = t.HelloWorld();
    Console.WriteLine(hello);

    return result;
}}";

            string result = exec.ExecuteMethod(code, "HelloWorld", "Rick") as string;

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {exec.Error}");
            Console.WriteLine(exec.ErrorMessage);
            Console.WriteLine(exec.GeneratedClassCode);

            Assert.IsFalse(exec.Error);
            Assert.IsTrue(result.Contains("Hello Rick"));

            // Just invoke the method again directly without any compilation/building
            // this is the fastest way to do multiple invocations.
            result = exec.InvokeMethod(exec.ObjectInstance, "HelloWorld", "Markus") as string;

            Console.WriteLine($"Result: {result}");
            Assert.IsFalse(exec.Error);
            Assert.IsTrue(result.Contains("Hello Markus"));

        }


        [TestMethod]
        public void ExecuteMethodWithExternalReferenceTest()
        {
            var exec = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
                AllowReferencesInCode = true
            };
            exec.AddDefaultReferencesAndNamespaces();
            exec.AddAssembly("System.Net.WebClient.dll");

            string code = $@"
#r ReferenceTest.dll

public string HelloWorld(string name)
{{
    var wc = new System.Net.WebClient();
    wc.DownloadString(new Uri(""https://west-wind.com""));


    var first = new int[] {{ 1,2,3,4,5 }}.First();
    Console.WriteLine(first);
    
    dynamic name2 = ""RIck"";
    Console.WriteLine(name2);

    var t = new ReferenceTest.Test();
    var hello = ""Referenced:  "" + t.HelloWorld(name);
    Console.WriteLine(hello);

    return hello;
}}";

            string result = exec.ExecuteMethod(code, "HelloWorld", "Rick") as string;

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {exec.Error}");
            

            Assert.IsFalse(exec.Error, exec.ErrorMessage + "\n" + exec.GeneratedClassCode);
            // from reference assembly Test.Hello
            Assert.IsTrue(result.Contains("Time is:"));

            // Just invoke the method again directly without any compilation/building
            // this is the fastest way to do multiple invocations.
            result = exec.InvokeMethod(exec.ObjectInstance, "HelloWorld", "Markus") as string;

            Console.WriteLine($"Result: {result}");
            Assert.IsFalse(exec.Error);
            Assert.IsTrue(result.Contains("Hello, Markus"));

        }



        [TestMethod]
        public async Task ExecuteAsyncMethodTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true
            };

            // lets not load assembly refs from host app in 6.0 but load explicitly below
            script.AddDefaultReferencesAndNamespaces();
#if NET6_0_OR_GREATER
            // Add all .NET60 Runtime Assemblies - Nuget: Basic.References.Net60
            //script.AddAssemblies(Basic.Reference.Assemblies.Net60.All);  // need this because base lib doesn't load WebClient for example

            // or explicitly add assemblies we need :(
            script.AddAssembly("System.Net.WebClient.dll");
#endif

            string code = $@"
public async Task<string> GetJsonFromAlbumViewer(int id)
{{
     var wc = new WebClient();
    var uri = new Uri(""https://albumviewer.west-wind.com/api/album/"" + id);

    string json = ""123"";
    try{{
        json =  await wc.DownloadStringTaskAsync(uri);
    }}
    catch(Exception ex) {{
        Console.WriteLine(""ERROR in method: "" + ex.Message);
    }}
    Console.WriteLine(""done with output..."");

    return json;
}}";
            string result = null;
            try
            {
                result = await script.ExecuteMethodAsync<string>(code, "GetJsonFromAlbumViewer", 37);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine($"Error Message: {script.ErrorMessage}");
            Console.WriteLine(script.GeneratedClassCode);

            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsNotNull(result,"Not a JSON response");
        }



        [TestMethod]
        public async Task ExecuteAsyncMethodWithNoResultTest()
        {
            var script = new CSharpScriptExecution();

            // lets not load assembly refs from host app in 6.0 but load explicitly below
            script.AddDefaultReferencesAndNamespaces();


            string code = $@"
public async Task NoResultConsole(int id)
{{
    Console.WriteLine($""Just writing some output...{{id}}"");    
}}";
        
            try
            {
                 await script.ExecuteMethodAsyncVoid(code, "NoResultConsole", 37);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            Assert.IsFalse(script.Error, script.ErrorMessage);
        }


        [TestMethod]
        public void ExecuteMethodWithRuntimeExceptionTest()
        {
            var exec = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
                ThrowExceptions = false,  // capture error
                CompileWithDebug = true   // provide error info for stack trace
            };
            exec.AddDefaultReferencesAndNamespaces();
            exec.AddAssembly("System.Net.WebClient.dll");

            string code = $@"
public string HelloWorld(string name)
{{
    var wc = new System.Net.WebClient();
    wc.DownloadString(new Uri(""https://west-wind.com""));

    string result = $""Hello {{name}}. Time is: {{DateTime.Now}}."";

    // This should cause a runtime error
    string x = null;
    x = x.Trim();

    var first = new int[] {{ 1,2,3,4,5 }}.First();
    Console.WriteLine(first);
    
    dynamic name2 = ""RIck"";
    Console.WriteLine(name2);

    return result;
}}";

            string result = exec.ExecuteMethod(code, "HelloWorld", "Rick") as string;

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {exec.Error}");
            Console.WriteLine(exec.ErrorMessage);

            // Since we compiled for Debug we should get a line number
            Console.WriteLine(exec.LastException.StackTrace);

            // which you can correlate to the generated code
            Console.WriteLine(exec.GeneratedClassCodeWithLineNumbers);

            // yup we had an error
            Assert.IsTrue(exec.Error);
        }


        [TestMethod]
        public async Task ExecuteAsyncVoidMethodTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
                AllowReferencesInCode = true
            };

            // lets not load assembly refs from host app in 6.0 but load explicitly below
            script.AddDefaultReferencesAndNamespaces();
            script.AddAssemblies("System.Net.WebCLient.dll");

            string code = $@"
public async Task<object> GetJsonFromAlbumViewer(int id)
{{
    Console.WriteLine(""Starting..."");

    var wc = new WebClient();
    var uri = new Uri(""https://albumviewer.west-wind.com/api/album/"" + id);

    string json = ""123"";
    try{{
Console.WriteLine(""Retrieving..."");
        json =  await wc.DownloadStringTaskAsync(uri);

 Console.WriteLine(""JSON retrieved..."");
    }}
    catch(Exception ex) {{
        Console.WriteLine(""ERROR in method: "" + ex.Message);
    }}

    Console.WriteLine(""All done in method"");

    return json;
}}";

            try
            {
                // void method
                var result = await script.ExecuteMethodAsync(code, "GetJsonFromAlbumViewer", 37);
                Console.WriteLine("Returned Value: " + result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine($"Error Message: {script.ErrorMessage}");
            Console.WriteLine(script.GeneratedClassCode);

            Assert.IsFalse(script.Error, script.ErrorMessage);
        }


        [TestMethod]
        public void ExecuteMoreThanOneMethodTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true
            };
            script.AddDefaultReferencesAndNamespaces();

            string code = $@"
public string HelloWorld(string name)
{{
string result = $""Hello {{name}}. Time is: {{DateTime.Now}}."";
return result;
}}

public string GoodbyeName {{ get; set; }}

public string GoodbyeWorld()
{{
string result = $""Goodbye {{GoodbyeName}}. Time is: {{DateTime.Now}}."";
return result;
}}
";

            string result = script.ExecuteMethod(code, "HelloWorld", "Rick") as string;

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCode);

            Assert.IsFalse(script.Error);
            Assert.IsTrue(result.Contains("Hello Rick"));

            // grab the last created instance
            dynamic instance = script.ObjectInstance;

            instance.GoodbyeName = "Kevin";
            result = instance.GoodbyeWorld();

            Console.WriteLine($"Result: {result}");
            Assert.IsTrue(result.Contains("Goodbye Kevin"));
        }


        /// <summary>
        /// Execute a method using the old Microsoft.CSharp CodeDomProvider
        /// Faster, and doesn't require Roslyn bits, but doesn't
        /// support latest C# features
        /// </summary>
        [TestMethod]
        public void SimplestMethodExecutionTest()
        {
            // Create configured instance with Default References and Namespaces loaded
            var exec = CSharpScriptExecution.CreateDefault();
            
            var code = $@"
public string Add(int num1, int num2)
{{
    // string templates
    var result = num1 + "" + "" + num2 + "" =  "" + (num1 + num2);
    Console.WriteLine(result);

    return result;
}}
";
            string result = exec.ExecuteMethod<string>(code, "Add", 10, 5);

            Console.WriteLine("Result: " + result);
            Console.WriteLine(exec.GeneratedClassCodeWithLineNumbers);
            Assert.IsFalse(exec.Error, exec.ErrorMessage);
        }


        [TestMethod]
        public void ExternalAssemblyTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true
            };

            // load runtime assemblies and common namespaces
            script.AddDefaultReferencesAndNamespaces();

            // Add External Assembly (current folder)
            script.AddAssembly("Westwind.Utilities.dll");

            // Add this assembly (ScriptTest type defined below used in script
            script.AddAssembly(typeof(SimpleCodeExecutionTests));

            // Alternately: Load all loaded assemblies
            script.AddLoadedReferences();

            script.AddNamespace("Westwind.Utilities");

            // Add this Namespace for class reference below
            script.AddNamespace("Westwind.Scripting.Test");


            string code = @"
// string text = parameters[0] as string;

var scriptTest = new ScriptTest();
string text = scriptTest.Message;
var newWorld = StringUtils.ReplaceString(text,""Hello"",""Goodbye cruel"", true);
return newWorld;
";

            string result = script.ExecuteCode(code, "Hello World!") as string;

            Console.WriteLine(result + "\n");
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);
            Assert.IsNotNull(result,script.ErrorMessage);

        }

        [TestMethod]
        public void ExecuteMethodWithLinqAndExtraClassTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true
            };

            // load runtime assemblies and common namespaces
            script.AddDefaultReferencesAndNamespaces();



            string code = @"public string LinqTest(string search)
{
    var list = new List<TestItem>()
    {
        new TestItem { Name=""Rick"" },
        new TestItem { Name=""Brian"" },
        new TestItem { Name=""James"" }
    };

    var match = list.FirstOrDefault( (ti) =>  ti.Name == search );
    return match.Name;
}

// Embedded Class
public class TestItem {
   public string Name {get; set; }
}

";

            string result = script.ExecuteMethod(code, "LinqTest","Brian") as string;

            Console.WriteLine(result + "\n");
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);
            Assert.IsNotNull(result, script.ErrorMessage);

        }

        [TestMethod]
        public void ExecuteMethodWithExceptionTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true
            };
            script.AddDefaultReferencesAndNamespaces();

            string code = $@"
public string HelloWorld(string name)
{{
    string result = null;
    result = result.ToString();  // boom

    return result;
}}";

            string result = script.ExecuteMethod(code, "HelloWorld", "Rick") as string;

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine($"Message: {script.ErrorMessage}");
            Console.WriteLine($"Type: {script.ErrorType}");
            Console.WriteLine($"stack: " + script.LastException?.StackTrace);
            Console.WriteLine($"    inner: " + script.LastException?.InnerException?.StackTrace);

            Console.WriteLine(script.GeneratedClassCode);

            Assert.IsTrue(script.Error);
            Assert.AreEqual(script.ErrorType, ExecutionErrorTypes.Runtime,"Should be a runtime error: " + script.ErrorMessage);

        }

        [TestMethod]
        public async Task TestDefaultAssemblyRequirements()
        {

            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true
            };

            // Don't use host assemblies - default + any manual adds!
            script.AddDefaultReferencesAndNamespaces();


            string code = $@"
public async Task<string> HelloWorld(string name)
{{

    var bytes = System.Text.Encoding.UTF8.GetBytes(""Text"");
    Console.WriteLine(bytes);  // Encoding

    var path = Path.GetFullPath(""\\temp\\test.txt"");
    Console.WriteLine(path); // System.IO

    var num = new int[] {{ 1, 2, 3, 4, 5 }};
    Console.WriteLine(num.Last());  // Linq

    await Task.Delay(1);

    string result = ""Hello World at: "" + DateTime.Now.ToString();
    return result;
}}";

            string result = await script.ExecuteMethodAsync<string>(code, "HelloWorld", "Rick");

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine($"Message: {script.ErrorMessage}");
            Console.WriteLine($"Type: {script.ErrorType}");
            Console.WriteLine($"stack: " + script.LastException?.StackTrace);
            Console.WriteLine($"    inner: " + script.LastException?.InnerException?.StackTrace);

            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error);

        }


        [TestMethod]
        public async Task ExecuteMethodWithExceptionAsyncTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true
            };
            script.AddDefaultReferencesAndNamespaces();

            
            string code = $@"
public async Task<string> HelloWorld(string name)
{{
    string result = null;
    result = result.ToString();  // boom

    await Task.Delay(1);

    return result;
}}";

            string result = await script.ExecuteMethodAsync<string>(code, "HelloWorld", "Rick");

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine($"Message: {script.ErrorMessage}");
            Console.WriteLine($"Type: {script.ErrorType}");
            Console.WriteLine($"stack: " + script.LastException?.StackTrace);
            Console.WriteLine($"    inner: " + script.LastException?.InnerException?.StackTrace);

            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsTrue(script.Error);
            Assert.IsTrue(script.ErrorType == ExecutionErrorTypes.Runtime);
        }


        [TestMethod]
        public async Task ExecuteCodeWithManyDefaultTypeslAsync()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            script.AddDefaultReferencesAndNamespaces();
            script.AddLoadedReferences();

            script.AddAssembly("System.Net.Http.dll");
            script.AddAssembly("System.Net.WebClient.dll");

            script.AddAssembly(typeof(ScriptTest));
            script.AddNamespace("Westwind.Scripting.Test");

            var model = new ScriptTest() { Message = "Hello World " };

            
            var code = @"
dynamic Model = @0;

var hclient = new System.Net.Http.HttpClient();

var al = new ArrayList();
al.Add(10);

var cookie = new System.Net.Cookie();

var path = Path.GetFullPath(""\\temp\\test.txt"");

var bytes = Encoding.UTF8.GetBytes(""Hello World"");

var list = new List<int> { 1,2,3,5 };

var uri = new Uri(""https://albumviewer.west-wind.com/api/album/1"");
var client = new WebClient();
string result = client.DownloadString( uri);

var hc = new HttpClient();

await Task.Delay(10); // test async

result =  result + ""\n"" + Model.Message +  "" "" + DateTime.Now.ToString();
return result;
";
            // have to add this reference for .NET Core
            script.AddAssembly("System.Net.WebClient.dll");

            

            string execResult = await script.ExecuteCodeAsync<string>(code, model);

            Console.WriteLine($"Result: {execResult}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
        }


        [TestMethod]
        public void TwoDynamicClassesTest()
        {
            var class1Code = @"
using System;

namespace Test1 {
    public class Person
    {
        public string Name {get; set; } = ""Rick"";
        public string Description {get; set; } = ""Testing"";
    } 
}
";

            var class2Code = @"
using System;
using Test1;

namespace Test
{

    public class Customer
    {
        public Test1.Person CustomerInfo {get; set; } = new Test1.Person();
        public string CustomerNumber  { get; set; }         
    } 
}
";

            var script = new CSharpScriptExecution();
            script.AddLoadedReferences();
            script.SaveGeneratedCode = true;
            script.GeneratedClassName = "__person";
            script.OutputAssembly = @"c:\temp\person.dll";

            var personType = script.CompileClassToType(class1Code);
            var person = Activator.CreateInstance(personType);

            
            Assert.IsNotNull(person, "Person should not be null. " + script.ErrorMessage + "\n" + script.GeneratedClassCodeWithLineNumbers);
            Console.WriteLine("Location: " + personType.Assembly.Location);
            
            //script = new CSharpScriptExecution();
            //script.AddDefaultReferencesAndNamespaces(); //AddLoadedReferences();
            //script.AddAssembly(script.OutputAssembly);
            
            script.SaveGeneratedCode = true;
            script.GeneratedClassName = "__customer";
            script.OutputAssembly = @"c:\temp\customer.dll";

            script.AddAssembly(personType);
            
            var customerType = script.CompileClassToType(class2Code);

            Assert.IsNotNull(customerType, "Customer should not be null. " + script.ErrorMessage + "\n" + script.GeneratedClassCodeWithLineNumbers);
            Console.WriteLine(customerType);
            Console.WriteLine(customerType.Assembly.Location);

            dynamic customer = Activator.CreateInstance(customerType);

            
            Assert.IsNotNull(customer.CustomerInfo.Name, "Customer should not be null");
            Console.WriteLine(customer.CustomerInfo.Name);
        }
    }


    public class ScriptTest
    {
        public string Message { get; set; } = "Hello wonderful World!!!";
        public string Name { get; set; }
        public int Id { get; set; }
    }


}
