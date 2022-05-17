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
    }
}
