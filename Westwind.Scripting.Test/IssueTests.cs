using System;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Westwind.Scripting.Test
{
    [TestClass]
    public class IssueTests
    {
        [TestMethod]
        public async Task ReplaceWordUsing_31Test()
        {
            var script = new CSharpScriptExecution();

            // lets not load assembly refs from host app in 6.0 but load explicitly below
            script.AddDefaultReferencesAndNamespaces();

            string code = $@"
using System.Net;
            string test = ""Hello World I'm using a new test string."";
            var name = parameters[0] as string;
    Console.WriteLine($""I am using a new using command {{name}}. {{test}} "");

    
";
            try
            {
                script.ExecuteCode(code, "NoResultConsole", "Rick");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);
                return;
            }

            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);
            Assert.IsFalse(script.Error, script.ErrorMessage);
        }

    }
}
