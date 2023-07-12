using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Westwind.Scripting
{

    /// <summary>
    /// Script Helper that is injected into the script as a global `Script` variable
    ///
    /// To use:
    ///
    /// {{ Script.RenderPartial("./test.template") }} 
    /// </summary>
    public class ScriptHelper
    {

        /// <summary>
        /// This the base path that's used for ~/ or  / paths when using RenderTemplate
        ///
        /// This value is null by default and if not set the current working directory
        /// is used instead.
        /// </summary>
        public string BasePath { get; set; }


        ScriptParser _parser = new ScriptParser();

        /// <summary>
        /// Renders a partial file into the template
        /// </summary>
        /// <param name="scriptPath">Path to script file to execute</param>
        /// <param name="model">optional model to pass in</param>
        /// <returns></returns>
        public string RenderPartial(string scriptPath, object model = null)
        {
            var script = File.ReadAllText(scriptPath);
            string result = _parser.ExecuteScript(script, model);
            if (_parser.Error)
            {
                result = $"{{! Template error ({scriptPath}):  " + _parser.ErrorMessage?.Trim() + " !}";
            }

            return result;
        }

        /// <summary>
        /// Renders a partial file into the template
        /// </summary>
        /// <param name="scriptPath">Path to script file to execute</param>
        /// <param name="model">optional model to pass in</param>
        /// <returns></returns>
        public async Task<string> RenderPartialAsync(string scriptPath, object model = null)
        {
            var script = await ReadFileAsync(scriptPath);
            string result = await _parser.ExecuteScriptAsync(script, model);
            if (_parser.Error)
            {
                result = "!! " + _parser.ErrorMessage + " !!";
            }

            return result;
        }

        /// <summary>
        /// Renders a string of script to effectively allow recursive
        /// rendering of content into a fixed template
        /// </summary>
        /// <param name="scriptPath">text or script to render</param>
        /// <param name="model">optional model to pass in</param>
        /// <returns></returns>
        public string RenderScript(string script, object model = null)
        {
            //var parser = new ScriptParser()
            //{
            //    ScriptEngine = _parser.ScriptEngine,
            //};
            string result = _parser.ExecuteScript(script,  model);
            return result;
        }

        /// <summary>
        /// Renders a string of script to effectively allow recursive
        /// rendering of content into a fixed template
        /// </summary>
        /// <param name="scriptPath">text or script to render</param>
        /// <param name="model">optional model to pass in</param>
        /// <returns></returns>
        public async Task<string> RenderScriptAsync(string script, object model = null)
        {
            return await  _parser.ExecuteScriptAsync(script, model);
        }

        /// <summary>
        /// Reads the entire content of a file asynchronously
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        private async Task<string> ReadFileAsync(string filePath, Encoding encoding = null)
        {
            filePath = FixBasePath(filePath);

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fs))
            {
                return await reader.ReadToEndAsync();
            }
        }


        /// <summary>
        /// Reads the entire content of a file asynchronously
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        private string ReadFile(string filePath, Encoding encoding = null)
        {
            filePath = FixBasePath(filePath);
            return File.ReadAllText(filePath);
        }

        private string FixBasePath(string filePath)
        {
            if (!string.IsNullOrEmpty(BasePath))
            {
                if (filePath.StartsWith("~") || filePath.StartsWith("/") || filePath.StartsWith("\\"))
                {
                    filePath = Path.Combine(BasePath, filePath.TrimStart('~', '/', '\\'));
                }
            }

            return filePath;
        }
    }

}
