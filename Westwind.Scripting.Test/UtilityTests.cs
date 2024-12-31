using System;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Westwind.Scripting.Test
{
    [TestClass]
    public class UtilityTests
    {
        [TestMethod]
        public async Task RoslynWarmupTest()
        {
            var result = await  RoslynLifetimeManager.WarmupRoslyn();
            Assert.IsTrue(result, "Warmup execution failed.");
        }

        [TestMethod]
        public void FindCodeLineTest()
        {
            string code = @"using System;
using System.Text;
using System.Reflection;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

namespace __ScriptExecution {

public class __bkosfhec
{ 



public async Task<object> GetJsonFromAlbumViewer(int id)
{
    Console.WriteLine(""Starting..."");

    var wc = new WebClient();
    var uri = new Uri(""https://albumviewer.west-wind.com/api/album/"" + id);

    string json = ""123"";
    try{
Console.WriteLine(""Retrieving..."");
        json =  await wc.DownloadStringTaskAsync(uri);

 Console.WriteLine(""JSON retrieved..."");
    }
    catch(Exception ex) {
        Console.WriteLine(""ERROR in method: "" + ex.Message);
    }

    Console.WriteLine(""All done in method"");

    return json;
}

} 
}";

            string matchLine = "public async Task<object> GetJsonFromAlbumViewer(int id)";

            int match = CSharpScriptExecution.FindCodeLine(code, matchLine );
            Console.WriteLine(match);
            var lineX = CSharpScriptExecution.GetLines(code)[match];
            Console.WriteLine(lineX);
            Assert.IsTrue(lineX == matchLine);

        }

#if NET6_0_OR_GREATER
        [TestMethod]
        public void UseAlternateAssemblyLoadContext_LoadsAssembliesInAlternateContextTest()
        {
            var myContext = new AssemblyLoadContext("MyContext", true);

            string codeBlock =
@"
int a = 0;
int b = (int) @0;
return a + b;";

            for (int i = 0; i < 10; i++)
            {
                var exec = new CSharpScriptExecution() { SaveGeneratedCode = false };
                exec.AddDefaultReferencesAndNamespaces();
                exec.AlternateAssemblyLoadContext = myContext;

                exec.ExecuteCode<int>(codeBlock, i);
            }

            Assert.AreEqual(1, myContext.Assemblies.Count());
            myContext.Unload();
        }

        [TestMethod]
        public void DisabledAssemblyCaching_GeneratesAssemblyForEachExecution()
        {
            var myContext = new AssemblyLoadContext("MyContext", true);

            string codeBlock =
@"
int a = 0;
int b = (int) @0;
return a + b;";

            for (int i = 0; i < 10; i++)
            {
                var exec = new CSharpScriptExecution() { SaveGeneratedCode = true };
                exec.AddDefaultReferencesAndNamespaces();
                exec.AlternateAssemblyLoadContext = myContext;
                exec.DisableAssemblyCaching = true;

                var result = exec.ExecuteCode<int>(codeBlock, i);
                Console.WriteLine(exec.ErrorMessage);
                System.Console.WriteLine(result.ToString());
            }

            Assert.AreEqual(10, myContext.Assemblies.Count());
            myContext.Unload();
        }
#endif
    }

}
