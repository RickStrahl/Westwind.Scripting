using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Westwind.Scripting.Test
{
    [TestClass]
    public class FileScriptParserTests
    {


        [TestMethod]
        public void SimpleFileScriptTest()
        {
            var scriptParser = new ScriptParser();
            var result = scriptParser.ExecuteScriptFile("website/Views/SelfContained.html", new TestModel { Name = "Rick" });

            Console.WriteLine(result);
            Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ErrorMessage);
        }

        [TestMethod]
        public void LayoutFileScriptTest()
        {
            var scriptParser = new ScriptParser();
            scriptParser.ScriptingDelimiters.HtmlEncodeExpressionsByDefault = true;

            var result = scriptParser.ExecuteScriptFile("website/Views/Detail.html",
                                new TestModel { Name = "Rick" },
                                basePath: "website/Views/");

            Console.WriteLine(result);
            Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ErrorMessage);
        }

        [TestMethod]
        public async Task LayoutFileAsyncScriptTest()
        {
            var scriptParser = new ScriptParser();
         

            var result = await scriptParser.ExecuteScriptFileAsync("website/Views/Detail.html",
                new TestModel { Name = "Rick" },
                basePath: "website/Views/");

            Console.WriteLine(result);
            Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ErrorMessage);
        }

        [TestMethod]
        public async Task LayoutFileAsyncTypedModelScriptTest()
        {
            var scriptParser = new ScriptParser();

            var result = await scriptParser.ExecuteScriptFileAsync<TestModel>("website/Views/Detail.html",
                new TestModel { Name = "Rick" },
                basePath: "website/Views/");

            Console.WriteLine(result);
            Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ErrorMessage);
        }

        /// <summary>
        /// Ensure that multiple runs of the same script parser instance
        /// will update when new script is passed to the same instance even
        /// while cached if code/script has been modified (ie. live script scenario)
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ScriptFileModificationMultiRunTest()
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

            var file = "./website/fileTemplate.template";
            File.WriteAllText(file, script);

            var scriptParser = new ScriptParser();
            scriptParser.AddAssembly(typeof(ScriptParserTests));

            string result = await scriptParser.ExecuteScriptFileAsync(file, model);

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

            File.WriteAllText(file, script);

            result = await scriptParser.ExecuteScriptFileAsync(file, model);

            Console.WriteLine(result);


            Console.WriteLine(scriptParser.Error + " " + scriptParser.ErrorType + " " + scriptParser.ErrorMessage + " ");
            Console.WriteLine(scriptParser.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ErrorMessage);

            Assert.IsTrue(result.Contains("MODIFIED!"),"Should contain Modified\n" + result);
        }
    }
}

