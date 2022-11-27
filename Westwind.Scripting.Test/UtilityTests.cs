using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Westwind.Utilities;

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

    }
}
