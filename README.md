# Westwind Csharp Scripting
### Dynamically compile and execute CSharp code at runtime

[![NuGet](https://img.shields.io/nuget/v/Westwind.Scripting.svg)](https://www.nuget.org/packages/Westwind.Scripting/)
[![](https://img.shields.io/nuget/dt/Westwind.Scripting.svg)](https://www.nuget.org/packages/Westwind.Scripting/)


<small>for Full Framework .NET 4.72 and later - no .NET Core supprot currently</small>

Get it from [Nuget](https://www.nuget.org/packages/Westwind.Scripting/):

```text
Install-Package Westwind.Scripting
```
</small>(currently you need to use the `-IncludePreRelease` flag)</small>

The small `CSharpScriptExecution` class provides an easy way to compile and execute C# code on the fly, using source code provided at runtime. You can use Roslyn compilation for the latest C# features, or classic C# 5 features for no-dependency installations.

This class makes is very easy to integrate simple scripting or text merging features into applications with minimal effort and it provides basic assembly caching so repeated calls don't recompile code.

This library provides:

#### Execution Features

* `ExecuteCode()` -  Execute an arbitrary block of code. Pass parameters, return a value
* `Evaluate()` - Evaluate a single expression from a code string and returns a value
* `ExecuteMethod()` - Provide a complete method signature and call from code
* `CompileClass()` - Generate a class instance from C# code

There are also async versions of the Execute and Evaluate methods:

* `ExecuteCodeAsync()`
* `EvaluateAsync()`
* `ExecuteMethod<TResult>()`

#### Small Script Parser
There's also a small script parser that allows you run C# scripts as templates that expand into a string using *Handlebars* like syntax with full C# code.

The Parser support C# syntax in script templates:

* Expressions
* Code Blocks with embedded script

Syntax used is:

* `{{ expression }}`
* `{{% openBlock }}`    `{{% endblock }}`


#### Supported features

* Assembly Caching so not every execution generates a new assembly
* Ability to compile entire classes and execute them
* Automatic Assembly Cleanup at shutdown
* Use Roslyn or Classic C# compiler interchangeably
* Display errors and source and line numbers


> #### Requires Roslyn Code Providers for your Project
> If you want to use Roslyn compilation for the latest C# features you have to make sure you add the `Microsoft.CodeDom.CompilerServices` NuGet Package to your application's root project to provide the required compiler binaries for your application.
>
> Note that this adds a sizable chunk of files to your application's output folder in the `\roslyn` folder. If you don't want this you can use the classic compiler, at the cost of not having access to C# 6+ features.

## Usage
Using the `CSharpScriptExecution` class is very easy. It works with code passed as strings for either a Code block, expression, one or more methods or even as a full C# class that can be turned into an instance.

You can add **Assembly References** and **Namespaces** via the `AddReferece()` and `AddNamespace()` methods.

Script Execution is gated rather than throwing directly to provide 

### Executing a Code Snippet
A code snippet can be **any block of .NET code** that can be executed. You can pass any number of parameters to the snippets which are accessible via a `parameters` object array. You can optionally `return` a value by providing a `return` statement.


```cs
var script = new CSharpScriptExecution()
{
    SaveGeneratedCode = true,
    CompilerMode = ScriptCompilerModes.Roslyn
};
script.AddDefaultReferencesAndNamespaces();

//script.AddAssembly("Westwind.Utilities.dll");
//script.AddNamespace("Westwind.Utilities");

var code = $@"
// Check some C# 6+ lang features
var s = new {{ name = ""Rick""}}; // anonymous types
Console.WriteLine(s?.name);       // null propagation

// pick up and cast parameters
int num1 = (int) @0;   // same as parameters[0];
int num2 = (int) @1;   // same as parameters[1];

// string templates
var result = $""{{num1}} + {{num2}} = {{(num1 + num2)}}"";
Console.WriteLine(result);

return result;
";

string result = script.ExecuteCode(code,10,20) as string;

Console.WriteLine($"Result: {result}");
Console.WriteLine($"Error: {script.Error}");
Console.WriteLine(script.ErrorMessage);
Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

Assert.IsFalse(script.Error, script.ErrorMessage);
Assert.IsTrue(result.Contains(" = 30"));
```

Note that the `return` in your code snippet is optional - you can just run code without a result value. 

> Any parameters you pass in can be accessed either via `parameters[0]`, `parameters[1]` etc. or using a simpler string representation of `@0`, `@1`.

### Evaluating an expression
If you want to evaluate a single expression, there's a shortcut `Evalute()` method that works pretty much the same:

```cs
var script = new CSharpScriptExecution()
{
    SaveGeneratedCode = true,
};
script.AddDefaultReferencesAndNamespaces();

// Full syntax
//object result = script.Evaluate("(decimal) parameters[0] + (decimal) parameters[1]", 10M, 20M);

// Numbered parameter syntax is easier
object result = script.Evaluate("(decimal) @0 + (decimal) @1", 10M, 20M);

Console.WriteLine($"Result: {result}");
Console.WriteLine($"Error: {script.Error}");
Console.WriteLine(script.ErrorMessage);
Console.WriteLine(script.GeneratedClassCode);

Assert.IsFalse(script.Error, script.ErrorMessage);
Assert.IsTrue(result is decimal, script.ErrorMessage);
```            

This method is a shortcut wrapper and simply wraps your code into a single line `return {exp};` statement. 

### Executing a Method
`ExecuteCode()` and `Evaluate()` are shortcuts for the slightly lower level and more flexible `ExecuteMethod()` method which as the name implies allows you to specify a single or multiple methods. In fact you can provide an **entire class body** including properties, events and nested class definitions in the code passed in. This gives a lot of flexibility as you can properly type parameters and return types:

```csharp
var script = new CSharpScriptExecution()
{
    SaveGeneratedCode = true,
    CompilerMode = ScriptCompilerModes.Roslyn,
    GeneratedClassName = "HelloWorldTestClass"
};
script.AddDefaultReferencesAndNamespaces();

string code = $@"
public string HelloWorld(string name)
{{
string result = $""Hello {{name}}. Time is: {{DateTime.Now}}."";
return result;
}}";

string result = script.ExecuteMethod(code,"HelloWorld","Rick") as string;

Console.WriteLine($"Result: {result}");
Console.WriteLine($"Error: {script.Error}");
Console.WriteLine(script.ErrorMessage);
Console.WriteLine(script.GeneratedClassCode);

Assert.IsFalse(script.Error);
Assert.IsTrue(result.Contains("Hello Rick"));
```

### More than just a Single Method
Note that you can provide more than a method in the code block - you can provide an entire **class body** including additional methods, properties/fields, events and so on. Effectively you can build out an entire class this way. After intial execution you can access the `ObjectInstance` member and use either Reflection or `dynamic` to access the functionality on that class.

```cs
var script = new CSharpScriptExecution()
{
    SaveGeneratedCode = true,
    CompilerMode = ScriptCompilerModes.Roslyn
};
script.AddDefaultReferencesAndNamespaces();

// Class body with multiple methods and properties
string code = $@"
public string HelloWorld(string name)
{{
string result = $""Hello {{name}}. Time is: {{DateTime.Now}}."";
return result;
}}

public string GoodbyeName {{ get; set; }}

public string GoodbyeWorld()
{{
string result = $""Goodbye {{GoodbyeName}}. Time is: {{DateTime.Now}}."";
return result;
}}
";

string result = script.ExecuteMethod(code, "HelloWorld", "Rick") as string;

Console.WriteLine($"Result: {result}");
Console.WriteLine($"Error: {script.Error}");
Console.WriteLine(script.ErrorMessage);
Console.WriteLine(script.GeneratedClassCode);

Assert.IsFalse(script.Error);
Assert.IsTrue(result.Contains("Hello Rick"));

// You can pick up the ObjectInstance of the generated class
// Make dynamic for easier access
dynamic instance = script.ObjectInstance;

instance.GoodbyeName = "Markus";
result = instance.GoodbyeWorld();

Console.WriteLine($"Result: {result}");
Assert.IsTrue(result.Contains("Goodbye Markus"));
```

### Compiling and Executing a Class
You can also compile an **entire class** and then get passed back a `dynamic` reference to that class so that you can explicitly use that object:


```cs
var script = new CSharpScriptExecution()
{
    SaveGeneratedCode = true,
    CompilerMode = ScriptCompilerModes.Roslyn
};
script.AddDefaultReferencesAndNamespaces();

var code = $@"
using System;

namespace MyApp
{{
    public class Math
    {{
        public string Add(int num1, int num2)
        {{
        // string templates
        var result = num1 + "" + "" + num2 + "" = "" + (num1 + num2);
        Console.WriteLine(result);
        
        return result;
        }}
        
        public string Multiply(int num1, int num2)
        {{
        // string templates
        var result = $""{{num1}}  *  {{num2}} = {{(num1 * num2)}}"";
        Console.WriteLine(result);
        
        return result;
        }}
        
    }}
}}
";

dynamic math = script.CompileClass(code);

Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

Assert.IsFalse(script.Error,script.ErrorMessage);
Assert.IsNotNull(math);

string addResult = math.Add(10, 20);
string multiResult = math.Multiply(3 , 7);


Assert.IsTrue(addResult.Contains(" = 30"));
Assert.IsTrue(multiResult.Contains(" = 21"));
```

## Template Script Execution
This library also includes a `ScriptParser` class that is a very low impact string based template engine that can embed C# expressions and codeblocks using `{{ }}` *Handlebar* style syntax. The idea is to have an easy way to turn templates into executable code that can transform template text with code into a string result.

An example usage is for the [Markdown Monster Editor](https://markdownmonster.west-wind.com/) which uses this library to provide text snippet expansion into Markdown (or other) documents. 

### Syntax Examples
A simple usage scenario might be to expand a DateTime stamp into a document as a snippet via a hotkey or explicitly

```markdown
---
- created on {{ DateTime.Now.ToString("MMM dd, yyyy") }}
```

You can also use this to expand logic. For example, this is for a custom HTML expansion in a Markdown document by wrapping an existing selection into the template markup:

```markdown
<div class="warning-box">
{{ await Model.ActiveEditor.GetSelection() }}
</div>
```

You can also use code blocks:

```markdown
**Open Editor Documents**

{{% foreach(var doc in Model.OpenDocuments) { }}
* {{ doc.Filename }}
{{% } }}

```

### Template Script Usage
There are several ways you can use this functionality:

* Parsing Templates to Code (as string)
* Parsing and Manually Executing Templates 
* Parsing and Automatically Executing Templates

#### Template to Code Parsing
You

```csharp
string script = @"
	Hello World. Date is: {{ DateTime.Now.ToString(""d"") }}!
	
	{{% for(int x=1; x<3; x++) { }}
	   Hello World
	{{% } }}
	
	DONE!
";

Console.WriteLine(script + "\n\n");

// THIS: Parses the template above to executable code
var code = ScriptParser.ParseScriptToCode(script);

Assert.IsNotNull(code, "Code should not be null or empty");
Console.WriteLine(code);
```

The code generated for the above looks like this:

```cs
var writer = new StringWriter();

writer.Write("\r\n\tHello World. Date is: ");
writer.Write(  DateTime.Now.ToString("d")  );
writer.Write("!\r\n\t\r\n\t");
 for(int x=1; x<3; x++) { 
writer.Write("\r\n\t   Hello World\r\n\t");
 } 
writer.Write("\r\n\t\r\n\tDONE!\r\n");

return writer.ToString();
```

### Execute the Code Manually
The sections above have already shown you how you can execute code so, you can use either `ExecuteMethod()` or `ExecuteCode()` to execute this code. In addition there is also a `ScriptParser.ExecuteScript()` method that automates the following process.

Manual execution can be beneficial if you're building a template that always uses the same input model. If that's the case you can explicit provide the type to the method and use `ExecuteMethod()` by explicitly defining the method signature.

This is the approach used in Markdown Monster for example, because the model passed in is always the same type.

```cs
// use an explicit class model
var model = new TestModel {Name = "rick", DateTime = DateTime.Now.AddDays(-10)};

string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{Model.Name}}
{{% } }}

And we're done with this!
";

Console.WriteLine(script );

// Get the C# code
var code = ScriptParser.ParseScriptToCode(script);

Assert.IsNotNull(code, "Code should not be null or empty");

Console.WriteLine(code);

// Customize the Execution engine to add our type and namespace
// so the compiler can use the reference directly!
var exec = new CSharpScriptExecution()
{
    SaveGeneratedCode = true,
};
exec.AddDefaultReferencesAndNamespaces();
exec.AddAssembly(typeof(ScriptParserTests));
exec.AddNamespace("Westwind.Scripting.Test");

// Create a method with a strongly typed signature to execute:
var method = @"public string HelloWorldScript(TestModel Model) { " +
             code + "}";

// Execute - we have to be explicit about which method to call and the model 
//           a concrete type
var result = exec.ExecuteMethod(method, "HelloWorldScript", model);

Assert.IsNotNull(result, exec.ErrorMessage);
Console.WriteLine(result);
```

### Execute the Code Generically
If the model is not fixed and the value passed is of a various types, you can use the generic version that using `dynamic` model typing to access the model in the template. This code is actually simpler as you can use the `ScriptParser.ExecuteScript()` method directly without explictly converting the code first.

```cs
var model = new TestModel { Name = "rick", DateTime = DateTime.Now.AddDays(-10) };

string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{Model.Name}}
{{% } }}

{{% await Task.Delay(100); }}

And we're done with this!
";

Console.WriteLine(script);

// Pass in script engine
// so we can retrieve potential error information
var exec = new CSharpScriptExecution()
{
    SaveGeneratedCode = true,
};
exec.AddDefaultReferencesAndNamespaces();

// This is not needed here because we use `dynamic`
//exec.AddAssembly(typeof(ScriptParserTests));
//exec.AddNamespace("Westwind.Scripting.Test");

string result = await ScriptParser.ExecuteScriptAsync(script, model,exec);

Assert.IsNotNull(result, exec.ErrorMessage);
Console.WriteLine(result);
```


## Usage Notes

  
## Change Log

### **Version 4.5** 

* **Add Support for Async Execution**  
You can now use various `xxxAsync()` overloads to execute methods as `Task` based operations that can be `await`ed and can use `await` inside of scripts.

* **Add ScriptParser for C# Template Scripting**  
Added a very lightweight scripting engine that uses *Handlebars* style syntax for processing C# expressions and code blocks. Look at the `ScriptParser` class.

### **Version 0.3**  

* **Updated to latest Microsoft CodeDom libraries**  
Updated to the latest Microsoft CodeDom compilation libraries which strealine the compiler process for Roslyn compilation with latest language features.

* **Remove support to pre .NET 4.72**  
In order to keep the runtime dependencies down switched the library target to `net472` which is .NET Standard compliant and so pulls in a much smaller set of dependencies. This is potentially a breaking change for older .NET applications, which will have to stick with the `0.2.x` versions.

* **Switch Projects to SDK Projects**  
Switched from classic .NET projects to the new simpler .NET SDK project format for the library and test projects.

## License
This library is published under **MIT license** terms.

**Copyright &copy; 2014-2021 Rick Strahl, West Wind Technologies**

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Give back
If you find this library useful, consider making a small donation:

<a href="https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=KPJQRQQ9BFEBW" 
    title="Find this library useful? Consider making a small donation." alt="Make Donation" style="text-decoration: none;">
	<img src="https://weblog.west-wind.com/images/donation.png" />
</a>


