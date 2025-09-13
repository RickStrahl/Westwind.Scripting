using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace Westwind.Scripting;

/// <summary>
/// Context object used for File based script parsing. The main purpose
/// of this context is to pass data through so that Layout and Sections
/// and partials can be processed reliably.
/// </summary>
public class ScriptFileContext
{
    public ScriptFileContext(string scriptText, string basePath = null)
    {
        Script = scriptText;
        BasePath = basePath;
    }

    /// <summary>
    /// The base path used for / and ~/ resolution
    /// If not specified the document's path (for files)
    /// or the current directory (for strings) is used
    /// </summary>
    public string BasePath { get; set; }

    /// <summary>
    /// The actual script code that's passed and updated
    /// through out the request processing process
    /// </summary>
    public string Script { get; set; }


    /// <summary>
    /// The model that will be passed to the execution code
    /// </summary>
    public object Model { get; set; }

    /// <summary>
    /// The layout page if any to use for this script. Path can be:
    /// 
    /// * Relative to the Script Page
    /// * Relative to the Base Path
    /// * Absolute path
    ///
    /// This value is parsed and if {{ }}  are contained separately
    /// evaluated using reflection evaluation.
    ///
    /// Ensure you use `Model.` prefix for individual context items
    /// (ie. Model.Topic, Model.Project etc.) rather 
    /// than the bootstrapped Topic and Project as you can normally
    /// in expressions of context variables.        
    /// </summary>
    public string Layout { get; set; }

    /// <summary>
    /// The title of the page        
    /// </summary>
    public string Title { get; set; }


    /// <summary>
    /// Dictionary of sections that are captured and passed through
    /// </summary>
    internal Dictionary<string, string> Sections { get; set; } = new();

    /// <summary>
    /// The top level script that is being processing
    /// </summary>
    public string ScriptFile { get; set; }

    /// <summary>
    /// Resolves a relative path to a fully qualified file system path
    /// of a script file using (in this order):
    ///
    /// * Absolute full path
    /// * Relative Path (to ScriptFile)
    /// * ~ Virtual Base path
    /// * Base Path
    /// 
    /// </summary>
    /// <param name="scriptPath">Path to resolve to a full path</param>
    /// <returns></returns>
    /// <exception cref="InvalidEnumArgumentException"></exception>
    public string ResolvePath(string scriptPath)
    {
            if (string.IsNullOrEmpty(scriptPath))
                return string.Empty;

            bool isTilde = scriptPath?.StartsWith("~") ?? false;
            string parentPagePath = Path.GetDirectoryName(ScriptFile) ;

            if (isTilde || !File.Exists(scriptPath))
            {
                string newScriptPath = string.Empty;

                // check parent path
                if (!isTilde && !string.IsNullOrEmpty(parentPagePath))
                {
                    newScriptPath = Path.Combine(parentPagePath, scriptPath);
                }
                if (string.IsNullOrEmpty(newScriptPath) || !File.Exists(newScriptPath))
                {
                    if (isTilde)
                        scriptPath = scriptPath.TrimStart(['~', '/', '\\']);
                    
                    newScriptPath = Path.Combine(BasePath, scriptPath);

                    if (!File.Exists(newScriptPath))
                        throw new InvalidEnumArgumentException("Page not found: " + scriptPath);
                }

                scriptPath = newScriptPath;
            }

            return scriptPath;
        }

}

