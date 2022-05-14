using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Westwind.Scripting;

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

            script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
                GeneratedClassName = "MyTest"
            };
            script.AddDefaultReferencesAndNamespaces();

            result = script.ExecuteCode(code, 4, 10) as string;
            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);


            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsTrue(result.Contains(" = 14"));

        }

        [TestMethod]
        public void EvaluateTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            script.AddDefaultReferencesAndNamespaces();

            // Full syntax
            //object result = script.Evaluate("(decimal) parameters[0] + (decimal) parameters[1]", 10M, 20M);

            // Numbered parameter syntax is easier
            object result = script.Evaluate("(decimal) @0 + (decimal) @1", 10M, 20M);

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCode);

            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsTrue(result is decimal, script.ErrorMessage);
        }



        [TestMethod]
        public async Task EvaluateAsyncTest()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            script.AddDefaultReferencesAndNamespaces();

            // Full syntax
            //object result = script.Evaluate("(decimal) parameters[0] + (decimal) parameters[1]", 10M, 20M);

            // Numbered parameter syntax is easier
            object result = await script.EvaluateAsync($"await Task.Run(async ()=> {{ await Task.Delay(1); return (decimal) @0 + (decimal) @1; }})", 10M, 20M);

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCode);

            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsTrue(result is decimal, script.ErrorMessage);
        }



        [TestMethod]
        public void ExecuteCodeSnippetWithoutResult()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
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
        public async Task ExecuteCodeSnippetWithoutResultAsync()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            script.AddDefaultReferencesAndNamespaces();

            string result =
                await script.ExecuteCodeAsync("await Task.Run(async ()=> {{ await Task.Delay(1); Console.WriteLine($\"Time is: {DateTime.Now}\"); }});", null) as string;

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error: {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCode);

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
            script.AddDefaultReferencesAndNamespaces();

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

                // var taskResult = script.ExecuteMethod(code, "GetJsonFromAlbumViewer", 37) as Task<string>;
                // Console.WriteLine("Task: " + taskResult ?? "null");
                //
                // object objResult = await taskResult;
                //result = objResult as string;

                Console.WriteLine("done");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
            Console.WriteLine("done");

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
    }



}
