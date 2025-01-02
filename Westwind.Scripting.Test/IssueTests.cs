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
        public void ReplaceWordUsing_31Test()
        {
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true
            };

            // lets not load assembly refs from host app in 6.0 but load explicitly below
            script.AddDefaultReferencesAndNamespaces();

            string code = $@"
// this should be discovered
using System.Net.Http;

// this caused a problem prior to fix 1.5.1
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

            // there should be no error (invalid namespace from bad using translation)
            Assert.IsFalse(script.Error, script.ErrorMessage);

            // make our using statement is in the generated code
            Assert.IsTrue(script.GeneratedClassCode.Contains("using System.Net.Http;"));

            // no error all good
        }


        /// <summary>
        /// By default CompileClass caches the generated object instance,
        /// so executing new code on the same instance executes the first code,
        /// unless you:
        /// * Set the script.ObjectInstance = null on new code execution        
        /// * Use the script.DisableObjectCaching = true option
        /// </summary>
        [TestMethod]
        public void MultipleExecution_30Test()
        {
            var code = @"
using System;

namespace CompilationTesting {

public class TestClass
{
    public string Name {get; set; } = ""John"";
    public DateTime Time {get; set; } = DateTime.Now;

    public TestClass()
    {
        
    }

    public string HelloWorld(string name = null)
    {
        if (string.IsNullOrEmpty(name))
             name = Name;

        return ""Hello, "" + Name + "". Time is: "" + Time.ToString(""MMM dd, yyyy"");
        
    } 
}

}
";
            var script = new CSharpScriptExecution()
            {
                SaveGeneratedCode = true,
                AllowReferencesInCode = true,
                DisableAssemblyCaching = true,
                //DisableObjectCaching = true    // this enables 
            };
            script.AddDefaultReferencesAndNamespaces();
      

            // dynamic required since host doesn't know about this new type
            dynamic gen = script.CompileClass(code);

            Assert.IsFalse(script.Error, script.ErrorMessage + "\n" + script.GeneratedClassCodeWithLineNumbers);

            gen.Name = "Rick";
            gen.Time = DateTime.Now.AddMonths(-1);

            var result = gen.HelloWorld();

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error ({script.ErrorType}): {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            //Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsTrue(result.Contains("Time is:"));

            code = @"
using System;

namespace CompilationTesting {

public class Test2Class
{
    public string Name {get; set; } = ""Jane"";
    public DateTime Time {get; set; } = DateTime.Now.AddYears(10);

    public Test2Class()
    {
        
    }

    public string HelloWorld(string name = null)
    {
        if (string.IsNullOrEmpty(name))
             name = Name;

        return ""Hellorio, "" + Name + "". Timex is: "" + Time.ToString(""MMM dd, yyyy"");
        
    } 
}

}
";

            // this enables excecution of the new code or DisableObjectCaching = true
            script.ObjectInstance = null;

            // dynamic required since host doesn't know about this new type                        
            gen = script.CompileClass(code);

            Assert.IsFalse(script.Error, script.ErrorMessage + "\n" + script.GeneratedClassCodeWithLineNumbers);

            gen.Name = "Janet";
            gen.Time = DateTime.Now.AddYears(20);

            result = gen.HelloWorld();

            Console.WriteLine(result);

        }

    }
}
