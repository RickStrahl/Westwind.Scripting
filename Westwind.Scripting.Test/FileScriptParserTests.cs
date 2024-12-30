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
            
            var result = scriptParser.ExecuteScriptFile("website/Views/Detail.html", new TestModel { Name = "Rick" },
                basePath: "website/Views/");

            Console.WriteLine(result);
            Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);

            Assert.IsNotNull(result, scriptParser.ErrorMessage);


        }
    }
}

