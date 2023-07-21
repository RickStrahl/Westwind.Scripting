using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Westwind.Utilities;

namespace Westwind.Scripting.Test
{
    [TestClass]
    public class CompileAssemblyTests
    {
        [TestMethod]
        public void CompileClassAndExecute()
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
                AllowReferencesInCode = true
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
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsTrue(result.Contains("Time is:"));
        }

   
    [TestMethod]
    public void CompileClassWithReferenceAndExecute()
            {
                var code = @"
using System;

// Reference external assembly

#r ReferenceTest.dll

using ReferenceTest;

namespace CompilationTesting {

public class TestClass
{
     public TestClass()
    {
        
    }

    public string HelloWorld(string name = null)
    {
        // Call referenced Assembly
        var t = new Test();  
        return t.HelloWorld(name);       
    } 
}

}
";
                var script = new CSharpScriptExecution()
                {
                    SaveGeneratedCode = true,
                    AllowReferencesInCode = true
                };
                script.AddDefaultReferencesAndNamespaces();

                // dynamic required since host doesn't know about this new type
                dynamic gen = script.CompileClass(code);

                Assert.IsFalse(script.Error, script.ErrorMessage + "\n" + script.GeneratedClassCodeWithLineNumbers);

                var result = gen.HelloWorld("beautiful people");

                Console.WriteLine($"Result: {result}");
                Console.WriteLine($"Error ({script.ErrorType}): {script.Error}");
                Console.WriteLine(script.ErrorMessage);
                Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

                Assert.IsFalse(script.Error, script.ErrorMessage);
                Assert.IsTrue(result.Contains("Time is:"));

            }

            [TestMethod]
        public void CompileInvalidClassDefinitionShouldNotThrow()
        {
            var code = @"
        using System;

        namespace Testing.Test
        {
            public class Test
            {
                public void Foo();
            }
        }";

            var script = new CSharpScriptExecution() { ThrowExceptions = true };

            script.AddDefaultReferencesAndNamespaces();

            dynamic result = script.CompileClass(code);

            // Should have an error
            Assert.IsTrue(script.Error, script.ErrorMessage);
        }

        [TestMethod]
        public void CompileClassAndExecuteFromStream()
        {
            var code = @"
using System;

namespace Testing.Test {

public class Test
{
     public string Name {get; set; } = ""John"";
     public DateTime Time {get; set; } = DateTime.Now;

    public Test()
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
            // typically this will be a file stream
            using (var stream = StringUtils.StringToStream(code))
            {
                var script = new CSharpScriptExecution() {SaveGeneratedCode = true};
                script.AddDefaultReferencesAndNamespaces();

                dynamic gen = script.CompileClass(stream);

                Assert.IsFalse(script.Error, script.ErrorMessage + "\n" + script.GeneratedClassCodeWithLineNumbers);

                gen.Name = "Rick";
                gen.Time = DateTime.Now.AddMonths(-1);

                var result = gen.HelloWorld();

                Console.WriteLine($"Result: {result}");
                Console.WriteLine($"Error ({script.ErrorType}): {script.Error}");
                Console.WriteLine(script.ErrorMessage);
                Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

                Assert.IsFalse(script.Error, script.ErrorMessage);
                Assert.IsTrue(result.Contains("Time is:"));
            }
        }


        [TestMethod]
        public void CompileClassAndManuallyLoadClass()
        {
            var code = @"
using System;

namespace Testing.Test {

public class Test
{
     public string Name {get; set; } = ""John"";
     public DateTime Time {get; set; } = DateTime.Now;

    public Test()
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
                SaveGeneratedCode = true
            };
            script.AddDefaultReferencesAndNamespaces();

            var type = script.CompileClassToType(code);

            // Manually create the instance with Reflection
            dynamic gen = Activator.CreateInstance(type);   // assumes parameterless ctor()
            
            Assert.IsFalse(script.Error, script.ErrorMessage + "\n" + script.GeneratedClassCodeWithLineNumbers);

            gen.Name = "Rick";
            gen.Time = DateTime.Now.AddMonths(-1);

            var result = gen.HelloWorld();

            Console.WriteLine($"Result: {result}");
            Console.WriteLine($"Error ({script.ErrorType}): {script.Error}");
            Console.WriteLine(script.ErrorMessage);
            Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

            Assert.IsFalse(script.Error, script.ErrorMessage);
            Assert.IsTrue(result.Contains("Time is:"));

            var x = new {Name = "Rick", Time = DateTime.Now};
        }

    }


}
