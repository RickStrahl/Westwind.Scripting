using System.Collections.Generic;

namespace Westwind.Scripting;

/// <summary>
/// Context object used for File based script parsing. The main purpose
/// of this context is to pass data through so that Layout and Sections
/// and partials can be processed reliably.
/// </summary>
public class ScriptFileContext
{        
    public ScriptFileContext(string scriptText, string basePath = null )
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
    /// The layout page if any to use for this script. Path is relative
    /// the detail page.
    ///
    /// Provided so compilation works not used in code.
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
}
