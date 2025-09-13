using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Westwind.Scripting;

namespace Westwind.Scripting;

/// <summary>
/// Very basic script evaluator that allows expression expansion without
/// compilation against a provided list of instance objects. Only works
/// with {{ expr }} syntax and only with explicit objects instances that
/// have been provided in the AllowedInstances dictionary.
///
/// This allows for limited script evaluation in user provided content
/// for example, inside of a body block of documentation.
///
/// Support is limited to named instances  that you provide in the `AllowedInstances`
/// dictionary. Any 'name' provided is matched to the expression instance or
/// the default instance if no match is found.
///
/// Method execution currently only works with non-parameter methods.
///
/// LIMITATIONS: MANY!
/// * Methods support only named instance calls
/// * Only support string and logic literal values
/// * Method nesting is not supported
/// </summary>
public class ScriptEvaluator
{
    /// <summary>
    /// Instances that are allowed to be used in expressions. Specify the instance
    /// name that is in scope in expressions (ie. {{ Project.Title }}) == "Project")
    /// and the actual instance.                
    /// </summary>
    public Dictionary<string, object> AllowedInstances { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Optional - Default expressions instance if the expression is not providing an
    /// instance prefix.
    ///
    /// This key value pair has to match one of the items in the AllowedInstances so
    /// you are mapping the non-prefixed to an actual instance.
    /// </summary>
    public KeyValuePair<string, object> DefaultInstance { get; set; }

    public ScriptingDelimiters Delimiters { get; set; } = new ScriptingDelimiters();

    /// <summary>
    /// Expands evaluated {{ expr }} expressions in a content block. Uses
    /// the AllowedInstances dictionary to evaluate expressions on
    /// allowed instances based on the instance name in the expressions
    /// (ie. {{ Project.Title }} is passing in 'Project' instance)
    /// </summary>
    /// <param name="content">Content with embedded script expressions</param>
    /// <param name="htmlDecode">If true html decodes generated code expressions. Leave at false unless you run into issues.</param>
    /// <returns></returns>
    public string Evaluate(string content, bool htmlDecode = false)
    {
        var scripts = ParseScriptExpressions(content, htmlDecode);
        return ExpandExpressions(content, scripts);
    }


    /// <summary>
    /// Expands expressions in a previously parsed string content block
    /// and replaces script expressions with evaluated content
    /// </summary>
    /// <param name="content"></param>
    /// <param name="scripts"></param>
    /// <returns></returns>
    public string ExpandExpressions(string content, IList<ScriptExpression> scripts)
    {
        foreach (var script in scripts)
        {
            if (script.DontProcess ||
                (script.Instance != null &&
                !AllowedInstances.TryGetValue(script.Instance, out var instance)))
                continue;

            try
            {
                script.ResultValue = EvaluateExpression(script.Code);
            }
            catch (Exception ex)
            {
                content = content.Replace(script.ScriptTag, $"{Delimiters.StartDelim} ERROR: {script.Code} - {ex.GetBaseException().Message} {Delimiters.EndDelim}");
            }

            string evaled = script.ResultValue?.ToString();
            if (!script.ScriptTag.StartsWith($"{Delimiters.StartDelim}!") && script.ResultValue is not IRawString)
                evaled = System.Net.WebUtility.HtmlEncode(evaled);

            content = content.Replace(script.ScriptTag, evaled);
        }

        return content;
    }

    public object EvaluateExpression(string code)
    {
        object result = null;

        foreach (var instance in AllowedInstances)
        {
            if (code.StartsWith(instance.Key + "."))
            {
                var member = code.Substring(instance.Key.Length + 1);
                if (member.Contains("("))
                {
                    var method = member.Substring(0, member.IndexOf('('));
                    var idx = member.IndexOf("(");
                    var idx2 = member.LastIndexOf(")");
                    string parmString = string.Empty;
                    if (idx2 - idx > 1)
                        parmString = member.Substring(idx + 1, idx2 - idx - 1);

                    var parms = ParseMethodParameters(parmString);

                    //var parms = new string[] { };
                    //if (!string.IsNullOrEmpty(parmString))
                    //    parms = parmString.Split(',');                        
                    List<object> args = new List<object>();
                    foreach (var param in parms)
                    {
                        try
                        {
                            args.Add(EvaluateExpression(param));
                        }
                        catch
                        {
                            throw new Exception(param + " expression evaluation failed.");
                        }
                    }
                    if (args is { Count: > 0 })
                    {
                        object[] ar = args.ToArray();
                        result = ReflectionUtils.CallMethodEx(instance.Value, method, ar);
                        return result;
                    }

                    return ReflectionUtils.CallMethodEx(instance.Value, method);
                }

                try
                {
                    return ReflectionUtils.GetPropertyEx(instance.Value, member);
                }
                catch
                {
                    throw new Exception($"[ {code} ] expression evaluation failed.");
                }
            }

        }

        if (code.StartsWith("\"") && code.EndsWith("\""))
            return code.Substring(1, code.Length - 2);
        else if (code == "true")
            return true;
        else if (code == "false")
            return false;



        return result;
    }

    static List<string> ParseMethodParameters(string parameterString)
    {
        List<string> result = new List<string>();
        int parenthesesCount = 0;
        bool inStringLiteral = false;
        var currentParam = new List<char>();

        for (int i = 0; i < parameterString.Length; i++)
        {
            char ch = parameterString[i];

            // Toggle string literal state
            if (ch == '"' && (i == 0 || parameterString[i - 1] != '\\')) // Handle escaped quotes
            {
                inStringLiteral = !inStringLiteral;
            }

            // Split on commas only if not inside a string literal or parentheses
            if (ch == ',' && parenthesesCount == 0 && !inStringLiteral)
            {
                result.Add(new string(currentParam.ToArray()).Trim());
                currentParam.Clear();
            }
            else
            {
                // Update parentheses count if not inside a string literal
                if (!inStringLiteral)
                {
                    if (ch == '(') parenthesesCount++;
                    else if (ch == ')') parenthesesCount--;
                }

                currentParam.Add(ch);
            }
        }

        // Add the last parameter
        if (currentParam.Count > 0)
        {
            result.Add(new string(currentParam.ToArray()).Trim());
        }

        return result;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="content"></param>
    /// <param name="htmlDecode">If true html decodes the code  generated code expressions</param>
    /// <returns></returns>
    public IList<ScriptExpression> ParseScriptExpressions(string content, bool htmlDecode = false)
    {
        var scripts = new List<ScriptExpression>();

        var exp = Delimiters.StartDelim + ".*?" + Delimiters.EndDelim;
        // allow Topic, Project, Helpers, Script
        var matches = Regex.Matches(content, exp, RegexOptions.Singleline | RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            string text = match.Value;
            var item = new ScriptExpression()
            {
                ScriptTag = text
            };

            string code = StringUtils.ExtractString(text, Delimiters.StartDelim, Delimiters.EndDelim)?
                                     .TrimStart(':', '!', '@', '=')  // strip expression modifiers
                                     .Trim();

            if (htmlDecode)
                code = WebUtility.HtmlDecode(code);

            item.Code = code;
            var tokens = code.Split('.');
            if (tokens.Length < 2)
            {
                if (!string.IsNullOrEmpty(DefaultInstance.Key))
                    item.Instance = DefaultInstance.Key;
                else
                    item.DontProcess = true;
            }
            else
            {
                item.Instance = tokens[0];
                var member = tokens[1];
                if (member.Contains("("))
                {
                    item.ExpressionMode = ScriptExpressionModes.Method;
                    int idx1 = item.Code.IndexOf('(');
                    int idx2 = item.Code.LastIndexOf(')');

                    item.Method = member.Substring(0, member.IndexOf('('));
                    // TODO Parse Parameters -string only allowed
                    int diff = idx2 - idx1 - 1;

                    if (diff > 1)
                    {
                        var parms = item.Code.Substring(idx1 + 1, diff);
#if NETFRAMEWORK || NETSTANDARD2_0
                        var tok = parms.Split([','], StringSplitOptions.RemoveEmptyEntries);
#else
                        var tok = parms.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
#endif
                        item.MethodParameters.AddRange(tok);
                    }

                }
                else
                {
                    item.ExpressionMode = ScriptExpressionModes.Property;
                    item.Property = member;
                }
            }

            scripts.Add(item);
        }

        return scripts;
    }
}


public class ScriptExpression
{
    public string ScriptTag { get; set; }

    public string Code { get; set; }

    public string Instance { get; set; }

    public string Property { get; set; }

    public string Method { get; set; }

    public List<string> MethodParameters { get; set; } = new List<string>();

    public object ResultValue { get; set; }

    public ScriptExpressionModes ExpressionMode { get; set; }

    public bool DontProcess { get; set; }

    public override string ToString() => ScriptTag;

}

public enum ScriptExpressionModes
{
    Method,
    Property,
    Expression
}
