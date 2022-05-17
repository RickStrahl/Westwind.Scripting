using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Westwind.Scripting
{


    /// <summary>
    /// This helper can help start up Roslyn before first call so that there's no
    /// long startup delay for first script execution and you can also optionally
    /// shut Roslyn down and kill the VBCSCompiler that otherwise stays loaded
    /// even after shutting down your application.
    /// </summary>
    public class RoslynLifetimeManager
    {
        /// <summary>
        /// Run a script execution asynchronously in the background to warm up Roslyn.
        /// Call this during application startup or anytime before you run the first
        /// script to ensure scripts execute quickly.
        ///
        /// Although this method returns `Task` so it can be tested
        /// for success, in applications you typically will call this
        /// without `await` on the result task and just let it operate
        /// in the background.
        /// </summary>
        public static Task<bool> WarmupRoslyn()
        {
            // warm up Roslyn in the background
            return Task.Run(() =>
            {
                var script = new CSharpScriptExecution();
                script.AddDefaultReferencesAndNamespaces();
                var result = script.ExecuteCode("int x = 1; return x;", null);

                return result is 1;
            });
        }

        /// <summary>
        /// Call this method to shut down the VBCSCompiler if our
        /// application started it.
        /// </summary>
        public static void ShutdownRoslyn(string appStartupPath = null)
        {
            if (string.IsNullOrEmpty(appStartupPath))
                appStartupPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            var processes = Process.GetProcessesByName("VBCSCompiler");
            foreach (var process in processes)
            {
                // only shut down 'our' VBCSCompiler
                var fn = GetMainModuleFileName(process);
                if (fn.Equals(appStartupPath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // ignore kill operation errors
                    }
                }
            }
        }


        [DllImport("Kernel32.dll")]
        private static extern bool QueryFullProcessImageName(
            [In] IntPtr hProcess,
            [In] uint dwFlags,
            [Out] StringBuilder lpExeName,
            [In, Out] ref uint lpdwSize);

        public static string GetMainModuleFileName(Process process)
        {
            var fileNameBuilder = new StringBuilder(1024);
            uint bufferLength = (uint) fileNameBuilder.Capacity + 1;
            return QueryFullProcessImageName(process.Handle, 0, fileNameBuilder, ref bufferLength)
                ? fileNameBuilder.ToString()
                : null;
        }

    }
}
