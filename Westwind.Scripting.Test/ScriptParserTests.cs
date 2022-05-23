using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Westwind.Scripting;

namespace Westwind.Scripting.Test
{
    [TestClass]
    public class ScriptParserTests
    {
        [TestMethod]
        public void BasicScriptParserTest()
        {
            string script = @"
	Hello World. Date is: {{ DateTime.Now.ToString(""d"") }}!
	
	{{% for(int x=1; x<3; x++) { }}
	   Hello World
	{{% } }}
	
	DONE!
";

            Console.WriteLine(script + "\n\n");

            var code = ScriptParser.ParseScriptToCode(script);

            Assert.IsNotNull(code, "Code should not be null or empty");

            Console.WriteLine(code);
        }

        [TestMethod]
        public void BasicScriptParserAndExecuteTest()
        {
            string script = @"
Hello World. Date is: {{ DateTime.Now.ToString(""d"") }}!

{{% for(int x=1; x<3; x++) { }}
{{ x }}. Hello World {{% } }}
And we're done with this!
";

            Console.WriteLine(script + "\n\n");

            
            var code = ScriptParser.ParseScriptToCode(script);

            Assert.IsNotNull(code, "Code should not be null or empty");

            Console.WriteLine(code);

            var compiler = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            compiler.AddDefaultReferencesAndNamespaces();

            var result = compiler.ExecuteCode(code);

            Console.WriteLine(result);
        }


        /// <summary>
        /// This method parses script and then manually uses the CSharpScriptEngine
        /// to execute a method. The advantage of this approach is that you get full
        /// control over the types passed in and don't have to use `dynamic` on the
        /// parameter. For non-generic solution with a fixed model this is the
        /// recommended approach.
        ///
        /// If your model is not fixed and can be any type of object or value, then
        /// you can use the easier to use `ScriptParser.ExecuteScriptAsync()` method
        /// that combines both steps into a single easy to use method (see next test)
        /// </summary>
        [TestMethod]
        public void BasicScriptParserAndManuallyExecuteWithModelTest()
        {
            var model = new TestModel {Name = "rick", DateTime = DateTime.Now.AddDays(-10)};

            string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{Model.Name}}
{{% } }}

And we're done with this!
";

            Console.WriteLine(script );


            var code = ScriptParser.ParseScriptToCode(script);

            Assert.IsNotNull(code, "Code should not be null or empty");

            Console.WriteLine(code);

            var exec = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            exec.AddDefaultReferencesAndNamespaces();
            exec.AddAssembly(typeof(ScriptParserTests));
            exec.AddNamespace("Westwind.Scripting.Test");

            var method = @"public string HelloWorldScript(TestModel Model) { " +
                         code + "}";

            var result = exec.ExecuteMethod(method, "HelloWorldScript", model);


            Console.WriteLine(exec.GeneratedClassCodeWithLineNumbers);
            Assert.IsNotNull(result, exec.ErrorMessage);
           
            

            Console.WriteLine(result);
        }

        [TestMethod]
        public async Task BasicScriptParserAndExecuteAsyncWithModelTest()
        {
            var model = new TestModel { Name = "rick", DateTime = DateTime.Now.AddDays(-10) };

            string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{Model.Name}}
{{% } }}

{{% await Task.Delay(100); }}

And we're done with this!
";

            Console.WriteLine(script);


            var code = ScriptParser.ParseScriptToCode(script);

            Assert.IsNotNull(code, "Code should not be null or empty");

            Console.WriteLine(code);

            var exec = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            exec.AddDefaultReferencesAndNamespaces();

            // explicitly add the type and namespace so the script can find the model type
            // which we are passing in explicitly here
            exec.AddAssembly(typeof(ScriptParserTests));
            exec.AddNamespace("Westwind.Scripting.Test");

            var method = @"public async Task<string> HelloWorldScript(TestModel Model) { " +
                         code + "}";

            var result = await exec.ExecuteMethodAsync<string>(method, "HelloWorldScript", model);

            Assert.IsNotNull(result, exec.ErrorMessage);
            
            Console.WriteLine(result);
        }


        /// <summary>
        /// This method uses the `ScriptParser.ExecuteScriptAsync()` method to
        /// generically execute a method that can receive a single input parameter.
        /// The parameter is dynamic cast since it can be anything.
        ///
        /// If your parameter is always the same and fixed, you may want to consider
        /// using the previous example and provide fixed typing to the executed method.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public void ScriptParserExecuteWithModelTest()
        {
            var model = new TestModel { Name = "rick", DateTime = DateTime.Now.AddDays(-10) };

            string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{Model.Name}}
{{% } }}

And we're done with this!
";

            Console.WriteLine(script);


            // Optional - build customized script engine
            // so we can add custom
            var exec = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            exec.AddDefaultReferencesAndNamespaces(dontLoadLoadedAssemblies: false);
            //exec.AddAssembly(typeof(ScriptParserTests));
            

            exec.AddNamespace("Westwind.Scripting.Test");

            string result = ScriptParser.ExecuteScript(script, model, exec);

            Console.WriteLine(result);

            Console.WriteLine(exec.GeneratedClassCodeWithLineNumbers);
            Assert.IsNotNull(result, exec.ErrorMessage);
        }

        /// <summary>
        /// This method uses the `ScriptParser.ExecuteScriptAsync()` method to
        /// generically execute a method that can receive a single input parameter.
        /// The parameter is dynamic cast since it can be anything.
        ///
        /// If your parameter is always the same and fixed, you may want to consider
        /// using the previous example and provide fixed typing to the executed method.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ScriptParserExecuteAsyncWithModelTest()
        {
            var model = new TestModel { Name = "rick", DateTime = DateTime.Now.AddDays(-10) };

            string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{Model.Name}}
{{% } }}

{{% await Task.Delay(10); }}

And we're done with this!
";

            Console.WriteLine(script);


            // Optional - build customized script engine
            // so we can add custom
            var exec = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            exec.AddDefaultReferencesAndNamespaces();

            //exec.AddAssembly(typeof(ScriptParserTests));
            //exec.AddNamespace("Westwind.Scripting.Test");

            string result = await ScriptParser.ExecuteScriptAsync(script, model, exec);

            Console.WriteLine(result);
            Console.WriteLine(exec.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, exec.ErrorMessage ) ;
            
        }

        [TestMethod]
        public void NoCSharpCodeSnippetTest()
        {
            string script = @"
<div>
Hello World. Date is: today!
</div>
";
            Console.WriteLine(script);


            // Optional - build customized script engine
            // so we can add custom
            var exec = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            exec.AddDefaultReferencesAndNamespaces();

            
            string result = ScriptParser.ExecuteScript(script,null, exec);

            Console.WriteLine(result);
            Console.WriteLine(exec.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, exec.ErrorMessage);
        }

        [TestMethod]
        public async Task NoCSharpCodeSnippetAsyncTest()
        {
            string script = @"
<div>
Hello World. Date is: today!
</div>
";
            Console.WriteLine(script);


            // Optional - build customized script engine
            // so we can add custom
            var exec = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
            };
            exec.AddDefaultReferencesAndNamespaces();


            string result = await ScriptParser.ExecuteScriptAsync(script, null, exec);

            Console.WriteLine(result);
            Console.WriteLine(exec.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, exec.ErrorMessage);
        }


    }

    public class TestModel
    {
        public string  Name { get; set; }
        public DateTime DateTime { get; set; } = DateTime.Now;
    }
}
