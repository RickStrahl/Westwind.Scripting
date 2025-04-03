using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Westwind.Utilities;

namespace Westwind.Scripting.Test
{
    [TestClass]
    public class ScriptParserTests
    {

        [TestMethod]
        public void ExecuteScriptNoModelTest()
        {
            string script = @"

Hello World. Date is: {{ DateTime.Now.ToString(""d"") }}!

{{% for(int x=1; x<3; x++) { }}
{{ x }}. Hello World
{{% } }}

DONE!
";
            Console.WriteLine(script + "\n\n");

            var scriptParser = new ScriptParser();
            var result = scriptParser.ExecuteScript(script, null);

            Console.WriteLine(result);
            Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(script, "Code should not be null or empty");
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
        public void ExecuteScriptWithModelTest()
        {
            var model = new TestModel { Name = "rick", DateTime = DateTime.Now.AddDays(-10) };

            string script = @"
{{%
    using Westwind.Utilities;
}}
Hello World. Date is: {{ DateTime.Now.ToString(""d"") }}!

{{% for(int x=1; x<3; x++) { }}
{{ x }}. {{ StringUtils.Replicate(""Hello World "",2) }}
{{% } }}

And we're done with this!
";
            // Optional - build customized script engine
            // so we can add custom
            
            var scriptParser = new ScriptParser();
            scriptParser.AddAssembly(typeof(Westwind.Utilities.StringUtils));
            // scriptParser.AddNamespace("Westwind.Utilities");  // done in code

            // Execute
            string result = scriptParser.ExecuteScript(script, model);

            Console.WriteLine(result);

            Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);
            Assert.IsNotNull(result, scriptParser.ScriptEngine.ErrorMessage);
            
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
        public async Task ExecuteScriptAsyncWithModelTest()
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

            var scriptParser = new ScriptParser();
            scriptParser.AddAssembly(typeof(ScriptParserTests));
            scriptParser.AddNamespace("Westwind.Scripting.Test");
            

            string result = await scriptParser.ExecuteScriptAsync(script, model);

            Console.WriteLine(result);
            Console.WriteLine("Error (" + scriptParser.ScriptEngine.ErrorType + "): " + scriptParser.ScriptEngine.ErrorMessage);
            Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ScriptEngine.ErrorMessage);
        }

        [TestMethod]
        public async Task ExecuteScriptAsyncWithCompilerErrorTest()
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

            //{ {% await Task.Delay(10); } }
            var scriptParser = new ScriptParser();
            scriptParser.ScriptEngine.SaveGeneratedCode = true;
            scriptParser.ScriptEngine.AddAssembly(typeof(ScriptParserTests));
            scriptParser.ScriptEngine.AddNamespace("Westwind.Scripting.Test");

            //string result = scriptParser.ExecuteScript(script, model);
            string result = await scriptParser.ExecuteScriptAsync(script, model);

            Console.WriteLine(result);
            Console.WriteLine("Error (" + scriptParser.ScriptEngine.ErrorType + "): " + scriptParser.ScriptEngine.ErrorMessage);
            Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ScriptEngine.ErrorMessage);
        }


        [TestMethod]
        public void ExecuteScriptWithModelWithReferenceTest()
        {
            var model = new TestModel { Name = "Rick & Dale", DateTime = DateTime.Now.AddDays(-10) };

            string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{ Model.Name }}
{{% } }}

And we're done with this!
";

            Console.WriteLine(script);


            // Optional - build customized script engine
            // so we can add custom

            var scriptParser = new ScriptParser();
            scriptParser.ScriptingDelimiters.HtmlEncodeExpressionsByDefault = true;

            // add dependencies
            scriptParser.AddAssembly(typeof(ScriptParserTests));
            scriptParser.AddNamespace("Westwind.Scripting.Test");

            // Execute
            string result = scriptParser.ExecuteScript(script, model);

            Console.WriteLine(result);

            Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);
            Assert.IsNotNull(result, scriptParser.ScriptEngine.ErrorMessage);
        }



        [TestMethod]
        public void CSharpExecutionExecuteScriptWithModelWithReferenceTest()
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

            // Optional - build customized script engine
            // so we can add custom

            var exec = CSharpScriptExecution.CreateDefault();

            // add dependencies
            exec.AddAssembly(typeof(ScriptParserTests));
            exec.AddNamespace("Westwind.Scripting.Test");

            // Execute
            string result = exec.ExecuteScript(script, model);

            Console.WriteLine(result);

            Console.WriteLine(script);
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
        public void ScriptParserExecuteWithModelExecuteMultipleTest()
        {
            var model = new TestModel { Name = "rick", DateTime = DateTime.Now.AddDays(-10) };

            string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{Model.Name}}
      {{% for (int y = 1; y < 3; y++) { }}
       {{ y}}. Yowsers
      {{% } }}
              
{{% } }}

And we're done with this!
";

            Console.WriteLine(script);

            var scriptParser = new ScriptParser();

            // Optional - build customized script engine
            // so we can add custom
            var exec = scriptParser.ScriptEngine;

            exec.AddAssembly(typeof(ScriptParserTests));
            exec.AddNamespace("Westwind.Scripting.Test");

            string result = scriptParser.ExecuteScript(script, model, exec);

            Console.WriteLine(result);

            Console.WriteLine(exec.GeneratedClassCodeWithLineNumbers);
            Assert.IsNotNull(result, exec.ErrorMessage);

            model.Name = "Johnny";
            result = scriptParser.ExecuteScript(script, model, exec);
            Console.WriteLine(result);

        }



        [TestMethod]
        public void BasicScriptParserTest()
        {
            string script = @"
Hello World. Date is: {{ DateTime.Now.ToString(""d"") }}!

{{% for(int x=1; x<3; x++) { }} {{ x }}. Hello World
{{% } }}

DONE!
";
            Console.WriteLine(script + "\n\n");

            var scriptParser = new ScriptParser();
            scriptParser.ScriptingDelimiters.HtmlEncodeExpressionsByDefault = true;
            var code = scriptParser.ParseScriptToCode(script);

            Assert.IsNotNull(code, "Code should not be null or empty");

            Console.WriteLine(code);

            var result = scriptParser.ExecuteScript(script, null);

            Console.WriteLine(result);

            Console.WriteLine("---\n" + scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ScriptEngine.ErrorMessage);
        }

        [TestMethod]
        public void DoubleBracketsInTextParserTest()
        {
            string script = @"
Hello World this should not be treated as an expression \{\{ not-expression \}\}.
Time is {{ DateTime.Now }}.
DONE!
";
            Console.WriteLine(script + "\n\n");

            var scriptParser = new ScriptParser();
            var code = scriptParser.ParseScriptToCode(script);

            Assert.IsNotNull(code, "Code should not be null or empty");

            Console.WriteLine(code);

            var result = scriptParser.ExecuteScript(script, null);

            Console.WriteLine(result);

            Console.WriteLine("---\n" + scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ScriptEngine.ErrorMessage);
        }


        [TestMethod]
        public void DefaultHtmlEncodedExpressionsParserTest()
        {
            string script = @"
{{%
string text = ""This is some \""text\"" that should be encoded & formatted."";
writer.Write(Script.HtmlEncode(""This & that written out from script""));
}}

Here is some encoded text:

---
{{ text }}
---

This text should **not** be encoded:

{{! text }}
{{ RawString.Raw(text) }}
{{ new RawString(text) }}
{{: RawString.Raw(text) }}    // This won't encode regardless of the override

DONE!
";
            Console.WriteLine("Script Code\n---\n" + script + "\n\n");

            var scriptParser = new ScriptParser();
            scriptParser.ScriptingDelimiters.HtmlEncodeExpressionsByDefault = true;

            var code = scriptParser.ParseScriptToCode(script);

            Assert.IsNotNull(code, "Code should not be null or empty");

            var result = scriptParser.ExecuteScript(script, null);

            Console.WriteLine("Script Output\n--\n" + result);

            Console.WriteLine("ParseScript Output\n---\n" + code + "\n---\n");


            Console.WriteLine("Generated Class Code\n---\n" + scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ScriptEngine.ErrorMessage);
        }

        [TestMethod]
        public void BasicScriptParserAndExecuteTest()
        {
            string script = @"
Hello World. Date is: {{ DateTime.Now.ToString(""d"") }}!

{{:""Hello & World! "".Trim() }}

{{ this.ToString() }}
";

            //{{% for(int x=1; x<3; x++) { }}
            //{{ x }}. Hello World {{% } }}
            //And we're done with this!
            //";

            Console.WriteLine(script + "\n\n");

            var scriptParser = new ScriptParser();
            var code = scriptParser.ParseScriptToCode(script);

            Assert.IsNotNull(code, "Code should not be null or empty");

            Console.WriteLine(code);

            // Explicit let Script Engine Execute code
            var result = scriptParser.ScriptEngine.ExecuteCode(code);


            Assert.IsNotNull(result, scriptParser.ScriptEngine.ErrorMessage);

            Console.WriteLine(result);

            Console.WriteLine("---\n" + scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);
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

            var scriptParser = new ScriptParser();
            var code = scriptParser.ParseScriptToCode(script);

            Assert.IsNotNull(code, "Code should not be null or empty");

            Console.WriteLine(code);


            scriptParser.ScriptEngine.AddAssembly(typeof(ScriptParserTests));
            scriptParser.ScriptEngine.AddNamespace("Westwind.Scripting.Test");

            var method = @"public string HelloWorldScript(TestModel Model) { " +
                         code + "}";

            var result = scriptParser.ScriptEngine.ExecuteMethod(method, "HelloWorldScript", model);


            Console.WriteLine(scriptParser.GeneratedClassCodeWithLineNumbers);
            Assert.IsNotNull(result, scriptParser.ErrorMessage);

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

            var scriptParser = new ScriptParser();
            var code = scriptParser.ParseScriptToCode(script);

            Assert.IsNotNull(code, "Code should not be null or empty");

            Console.WriteLine(code);

            var exec = new CSharpScriptExecution() { SaveGeneratedCode = true };
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
            Console.WriteLine(exec.GeneratedClassCodeWithLineNumbers);
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


            var scriptParser = new ScriptParser();
            string result = scriptParser.ExecuteScript(script, null);

            Console.WriteLine(result);
            Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ScriptEngine.ErrorMessage);
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


            var scriptParser = new ScriptParser();
            string result = await scriptParser.ExecuteScriptAsync(script, null);

            Console.WriteLine(result);
            Console.WriteLine(scriptParser.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ErrorMessage);
        }


        [TestMethod]
        public async Task BasicPartialTest()
        {
            var model = new TestModel { Name = "rick", DateTime = DateTime.Now.AddDays(-10) };
            string script = """
<div>
Hello World. Date is: {{ DateTime.Now.ToString() }}

{{ await Script.RenderPartialAsync("./Templates/Time_Partial.csscript") }}
Done.
</div>
""";
            Console.WriteLine(script + "\n---");


            var scriptParser = new ScriptParser();
            scriptParser.AddAssembly(typeof(ScriptParserTests));


            string result = await scriptParser.ExecuteScriptAsync(script, model);
            Console.WriteLine(result);
            Console.WriteLine(scriptParser.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ErrorMessage);
        }


        [TestMethod]
        public async Task BasicRenderScriptTest()
        {
            var model = new TestModel { Name = "rick", DateTime = DateTime.Now.AddDays(-10), Expression = "Time: {{ DateTime.Now.ToString(\"HH:mm:ss\") }}" };
            string script = """
<div>
Hello World. Date is: {{ DateTime.Now.ToString() }}
<b>{{ Model.Name }}</b>

{{ await Script.RenderScriptAsync(Model.Expression,null) }}

Done.
</div>
""";
            Console.WriteLine(script + "\n---");


            var scriptParser = new ScriptParser();
            scriptParser.AddAssembly(typeof(ScriptParserTests));


            string result = await scriptParser.ExecuteScriptAsync(script, model);

            Console.WriteLine(result);
            Console.WriteLine(scriptParser.Error + " " + scriptParser.ErrorType + " " + scriptParser.ErrorMessage + " ");
            Console.WriteLine(scriptParser.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ErrorMessage);
        }


        /// <summary>
        /// Ensure that multiple runs of the same script parser instance
        /// will update when new script is passed to the same instance even
        /// while cached if code/script has been modified (ie. live script scenario)
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ScriptModificationMultiRunTest()
        {
            var model = new TestModel { Name = "rick", DateTime = DateTime.Now.AddDays(-10), Expression = "Time: {{ DateTime.Now.ToString(\"HH:mm:ss\") }}" };
            string script = """
                            <div>
                            Hello World. Date is: {{ DateTime.Now.ToString() }}
                            <b>{{ Model.Name }}</b>

                            {{ await Script.RenderScriptAsync(Model.Expression,null) }}

                            Done.
                            </div>
                            """;
            Console.WriteLine(script + "\n---");


            var scriptParser = new ScriptParser();
            scriptParser.AddAssembly(typeof(ScriptParserTests));

            string result = await scriptParser.ExecuteScriptAsync(script, model);

            Console.WriteLine(result + "\n----\n\n");

            script = """
                            <h1>MODIFIED!</h1>
                            <div>
                            Hello World. Date is: {{ DateTime.Now.ToString() }}
                            <b>{{ Model.Name }}</b>

                            {{ await Script.RenderScriptAsync(Model.Expression,null) }}

                            Done.
                            </div>
                            """;
            Console.WriteLine(script + "\n---");


            result = await scriptParser.ExecuteScriptAsync(script, model);

            Console.WriteLine(result);


            Console.WriteLine(scriptParser.Error + " " + scriptParser.ErrorType + " " + scriptParser.ErrorMessage + " ");
            Console.WriteLine(scriptParser.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ErrorMessage);
            
            Assert.IsTrue(result.Contains("MODIFIED!"));
        }

    }

    public class TestModel
    {
        public string  Name { get; set; }
        public DateTime DateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Test for Nested Expression Parsing
        /// </summary>
        public string Expression { get; set; }
    }
}
