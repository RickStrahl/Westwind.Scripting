using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}}";

            string result = script.ExecuteMethod(code, "HelloWorld", "Rick") as string;

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCode);

            Assert.IsFalse(script.Error);
            Assert.IsTrue(result.Contains("Hello Rick"));

            // Just invoke the method again directly without any compilation/building
            // this is the fastest way to do multiple invocations.
            result = script.InvokeMethod(script.ObjectInstance, "HelloWorld", "Markus") as string;

            Console.WriteLine($"Result: {result}");
            Assert.IsFalse(script.Error);
            Assert.IsTrue(result.Contains("Hello Markus"));

        }


        [TestMethod]
        public async Task ExecuteAsyncMethodTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true
            };

            // lets not load assembly refs from host app in 6.0 but load explicitly below
            script.AddDefaultReferencesAndNamespaces(dontLoadLoadedAssemblies: false);
#if NET6_0
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

            dynamic instance = script.ObjectInstance;

            instance.GoodbyeName = "Markus";
            result = instance.GoodbyeWorld();



            Console.WriteLine($"Result: {result}");
            Assert.IsTrue(result.Contains("Goodbye Markus"));

        }


        /// <summary>
        /// Execute a method using the old Microsoft.CSharp CodeDomProvider
        /// Faster, and doesn't require Roslyn bits, but doesn't
        /// support latest C# features
        /// </summary>
        [TestMethod]
        public void ClassicModeTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true
            };
            script.AddDefaultReferencesAndNamespaces();

            Console.WriteLine(script.References.Count);

            //script.AddAssembly("Westwind.Utilities.dll");
            //script.AddNamespace("Westwind.Utilities");

            var code = $@"
public string Add(int num1, int num2)
{{
    // string templates
    var result = num1 + "" + "" + num2 + "" =  "" + (num1 + num2);
    Console.WriteLine(result);

    return result;
}}
";

            string result = script.ExecuteMethod(code, "Add", 10, 5) as string;

            Console.WriteLine("Result: " + result);
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);
            Assert.IsFalse(script.Error, script.ErrorMessage);
        }


        /// <summary>
        /// Compile a class and return instance as dynamic
        /// </summary>
        [TestMethod]
        public void CompileClassTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            script.AddDefaultReferencesAndNamespaces();

            var code = $@"
using System;

namespace MyApp
{{
    public class Math
    {{
        public string Add(int num1, int num2)
        {{
            // string templates
            var result = num1 + "" + "" + num2 + "" = "" + (num1 + num2);
            Console.WriteLine(result);

            return result;
        }}

        public string Multiply(int num1, int num2)
        {{
           // string templates
            var result = $""{{num1}}  *  {{num2}} = {{ num1 * num2 }}"";
            Console.WriteLine(result);

            result = $""Take two: {{ result ?? ""No Result"" }}"";
            Console.WriteLine(result);

            return result;
        }}
    }}
}}
";

            dynamic math = script.CompileClass(code);

            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error,script.ErrorMessage);

            Assert.IsNotNull(math);

            string addResult = math.Add(10, 20);
            string multiResult = math.Multiply(3 , 7);


            Assert.IsTrue(addResult.Contains(" = 30"));
            Assert.IsTrue(multiResult.Contains(" = 21"));
        }


        [TestMethod]
        public void ExternalAssemblyTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true
            };

            // load runtime assemblies and common namespaces
            script.AddDefaultReferencesAndNamespaces(false);

            // Add External Assembly (current folder)
            script.AddAssembly("Westwind.Utilities.dll");

            // Add this assembly (ScriptTest type defined below used in script
            script.AddAssembly(typeof(SimpleCodeExecutionTests));

            // Alternately: Load all loaded assemblies
            //script.AddLoadedAssemblies();

            script.AddNamespace("Westwind.Utilities");

            // Add this Namespace for class reference below
            script.AddNamespace("Westwind.Scripting.Test");


            string code = @"
// ing text = parameters[0] as string;

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

            script.AddAssembly(typeof(ScriptTest));
            script.AddNamespace("Westwind.Scripting.Test");

            var model = new ScriptTest() { Message = "Hello World " };

            
            var code = @"
dynamic Model = @0;

var hclient = new System.Net.Http.HttpClient();

var al = new ArrayList();
al.Add(10);

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


            string execResult = await script.ExecuteCodeAsync<string>(code, model);

            Console.WriteLine($"Result: {execResult}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
        }
    }


    public class ScriptTest
    {
        public string Message { get; set; } = "Hello wonderful World!!!";
    }


}
