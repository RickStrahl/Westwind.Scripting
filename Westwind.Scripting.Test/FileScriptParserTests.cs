using System;
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
    }
}

