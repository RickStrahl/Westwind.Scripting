# Westwind CSharp Scripting

**Dynamically compile and execute CSharp code at runtime**

[![NuGet](https://img.shields.io/nuget/v/Westwind.Scripting.svg)](https://www.nuget.org/packages/Westwind.Scripting/)
[![](https://img.shields.io/nuget/dt/Westwind.Scripting.svg)](https://www.nuget.org/packages/Westwind.Scripting/)

This small library provides an easy way to compile and execute C# code from source code provided at runtime. It uses Roslyn to provide compilation services for string based code via the `CSharpScriptExecution` class and lightweight, self contained C# script templates via the `ScriptParser` class that can evaluate expressions and structured C# statements using  Handlebars-like (`{{ expression }}` and `{{% code }}`) script templates.

Get it from [Nuget](https://www.nuget.org/packages/Westwind.Scripting/):

```text
Install-Package Westwind.Scripting
```

It supports the following targets:

* .NET 6.0 (net60)
* Full .NET Framework (net462)
* .NET Standard 2.0 (netstandard2.0)
 
For more detailed information and a discussion of the concepts and code that runs this library, you can also check out this introductory blog post:

* [Runtime Code Compilation Revisited for Roslyn](https://weblog.west-wind.com/posts/2022/Jun/07/Runtime-CSharp-Code-Compilation-Revisited-for-Roslyn)

## Features
* Easy C# code compilation and execution for:
	* Code blocks  (generic execution)
	* Full methods (method header/result value)
	* Expressions  (evaluate expressions)
	* Full classes (compile and load)
* Async execution support
* Caching of already compiled code
* Ability to compile entire classes and load, execute them
* Error Handling
	* Intercept compilation and execution errors
	* Detailed compiler error messages
	* Access to compiled output w/ line numbers
* Roslyn Warmup 
* Template Scripting Engine using Handlebars-like with C# syntax

### CSharpScriptExecution: C# Runtime Compilation and Execution
Runtime code compilation and execution is handled via the `CSharpScriptExecution` class.

* `ExecuteCode()` -  Execute an arbitrary block of code. Pass parameters, return a value
* `Evaluate()` - Evaluate a single expression from a code string and returns a value
* `ExecuteMethod()` - Provide a complete method signature and call from code
* `CompileClass()` - Generate a class instance from C# code

There are also async versions of the Execute and Evaluate methods:

* `ExecuteMethodAsync()`
* `ExecuteCodeAsync()`
* `EvaluateAsync()`

All method also have additional generic return type overloads.

Additionally you can also compile self-contained classes:

* `CompileClass()`
* `CompileClassToType()`
* `CompileAssembly()`

These provide either quick compilation for later use, or direct type or assembly instantiation for immediate on the fly execution.

### ScriptParser: C# Template Script Expansion
Script Templating using a *Handlebars* like syntax that can expand **C# expressions** and **C# structured code** in text templates that produce transformed text output, can be achieved using the `ScriptParser` class.

Methods:

* `ExecuteScript()`     
* `ExecuteScriptAsync()`
* `ParseScriptToCode()`

Script parser expansion syntax used is:

* `{{ expression }}`
* `{{% openBlock }}` other text or expressions or code blocks `{{% endblock }}`

> #### Important: Large Runtime Dependencies on Roslyn Libraries
> Please be aware that this library has a dependency on `Microsoft.CodeAnalysis` which contains the Roslyn compiler components used by this component. This dependency incurs a 10+mb runtime dependency and a host of support files that are added to your project output.

## Quick Start Examples
To get you going quickly here are a few simple examples that demonstrate functionality. I recommend you read the more detailed instructions below but these examples give you a quick idea on how this library works.

### Execute generic C# code with Parameters and Result Value

```cs
var script = new CSharpScriptExecution() { SaveGeneratedCode = true };
script.AddDefaultReferencesAndNamespaces();

var code = $@"
// pick up and cast parameters
int num1 = (int) @0;   // same as parameters[0];
int num2 = (int) @1;   // same as parameters[1];

var result = $""{{num1}} + {{num2}} = {{(num1 + num2)}}"";

Console.WriteLine(result);  // just for kicks in a test

return result;
";

// should return a string: (`"10 + 20 = 30"`)
string result = script.ExecuteCode<string>(code,10,20);

if (script.Error) 
{
	Console.WriteLine($"Error: {script.ErrorMessage}");
	Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);
	return
}	
```

### Execute Async Code with a strongly typed Model 

```csharp
var script = new CSharpScriptExecution() {  SaveGeneratedCode = true };
script.AddDefaultReferencesAndNamespaces();

// have to add references so compiler can resolve
script.AddAssembly(typeof(ScriptTest));
script.AddNamespace("Westwind.Scripting.Test");

var model = new ScriptTest() { Message = "Hello World " };

var code = @"
// To Demonstrate Async support
await Task.Delay(10); // test async

string result =  Model.Message +  "" "" + DateTime.Now.ToString();
return result;
";

// Use generic version to specify result and model types
string execResult = await script.ExecuteCodeAsync<string, ScriptTest>(code, model);
```

> Note that you can forego the strongly typed model by using the non-generic `ExecuteCodeAsync()` or `ExecuteCode()` methods which use `dynamic` instead of the strong type. This allows the compiler to resolve `Model` without explicitly having to add the reference.

### Evaluate a single expression

```cs
var script = new CSharpScriptExecution();
script.AddDefaultReferencesAndNamespaces();

// Numbered parameter syntax is easier
var result = script.Evaluate<decimal>("(decimal) @0 + (decimal) @1", 10M, 20M);

Assert.IsFalse(script.Error, script.ErrorMessage );
```

### Template Script Parsing
```csharp
var model = new TestModel {Name = "rick", DateTime = DateTime.Now.AddDays(-10)};

string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{Model.Name}}
{{% } }}

And we're done with this!
";

Console.WriteLine(script);


// Optional - build customized script engine
// so we can add custom

var scriptParser = new ScriptParser();

// add dependencies
scriptParser.AddAssembly(typeof(ScriptParserTests));
scriptParser.AddNamespace("Westwind.Scripting.Test");

// Execute
string result = scriptParser.ExecuteScript(script, model);

Console.WriteLine(result);

Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);
Assert.IsNotNull(result, scriptParser.ScriptEngine.ErrorMessage);
```

which produces:

```txt
Hello World. Date is: 5/22/2022!

1. Hello World rick

2. Hello World rick

And we're done with this!
```

## Usage
Using the `CSharpScriptExecution` class is very easy to use. It works with code passed as strings for either a block of code, an expression, one or more methods or even as a full C# class that can be turned into an instance.

There are methods for:

* Generic code execution
* Complete method execution
* Expression Evaluation
* In-memory class compilation and loading

> #### Important: Large Roslyn Dependencies
> If you choose to use this library, please realize that you will pull in a very large dependency on the `Microsoft.CodeAnalysis` Roslyn libraries which accounts for 10+mb of runtime files that have to be distributed with your application.

### Setting up for Compilation: CSharpScriptExecution
Compiling code is easy - setting up for compilation and ensuring that all your dependencies are available is a little more complicated and also depends on whether you're using full .NET Framework or .NET Core or .NET Standard.

In order to compile code the compiler requires that all dependencies are referenced both for assemblies that need to be compiled against as well as any namespaces that you plan to access in your code and don't want to explicitly mention.

#### Adding Assemblies and Namespaces
There are a number of methods that help with this:

* `AddDefaultReferencesAndNamespaces()`
* `AddLoadedReferences()`
* `AddAssembly()`
* `AddAssemblies()`
* `AddNamespace()`
* `AddNamespaces()`

Initial setup for code execution then looks like this:

```cs
var script = new CSharpScriptExecution()
{
    SaveGeneratedCode = true  // useful for errors and trouble shooting
};

// load a default set of assemblies that provides common base class functionality
script.AddDefaultReferencesAndNamespaces();

// Add any additional dependencies
script.AddAssembly(typeof(MyApplication));       // by type
script.AddAssembly("Westwind.Utilitiies.dll");   // by assembly file

// Add additional namespaces you might use in your code snippets
script.AddNamespace("MyApplication");
script.AddNamespace("MyApplication.Utilities");
script.AddNamespace("Westwind.Utilities");
script.AddNamespace("Westwind.Utilities.Data");
```

#### Allowing Assemblies and Namespaces in Code
You can also add namespaces and - optionally - assembly references in code.

##### Namespaces
You can add valid namespace references in code by using the following syntax:

```cs
using Westwind.Utilities

var errors = StringUtils.GetLines(Model.Errors);
```
Namespaces are always parsed if present.

##### Assembly References
Assembly references are **disabled by default** as they are a potential security issue. But you can enable them via the `AllowReferencesInCode` property set to `true`.

Once enabled you can embed references like this:

```cs
#r MarkdownMonster.exe
using MarkdownMonser

var title = mmApp.Configuration.ApplicationName;
```

Assemblies are searched for in the application folder and in the runtime folder.

####

#### Configuration Properties
The `CSharpScriptExecution` has only a few configuration options available:

* **SaveGeneratedCode**  
If `true` captures the generated class code for the compilation that is used to execute your code. This will include the class and method wrappers around the code. You can use the `GeneratedCode` or `GeneratedCodeWithLineNumbers` properties to retrieve the code. The line numbers will match up with compilation errors return in the `ErrorMessage` so you can display an error message with compiler errors along with the code to optionally review the code in failure. 

* **AllowReferencesInCode**  
If `true` allows references to be added in script code via
`#r assembly.dll`.

* **OutputAssembly**  
You can optionally specify a filename to which the assembly is compiled. If this value is not set the assembly is generated in-memory which is the default.

* **GeneratedClassName**  
By default the class generated from any of the code methods generates a random class name. You can override the class name so you can load any generated types explicitly. Generally it doesn't matter what the class name is as the dynamic methods find the single class generated in the assembly.

* **ThrowExceptions**  
If `true` runtime errors will throw runtime execution exceptions rather than silently failing and setting error properties.  
The default is `false` and the recommended approach is to explicitly check for errors after compilation and execution, by checking `Error`, `ErrorMessage` and `LastException` properties which we highly recommend.

> ***Note**: Compiler errors don't throw - only runtime errors do. Compiler errors set properties of the object as do execution errors when `ThrowExecptions =  false`.*

**Error Properties** 

When compilation errors occur the following error properties are set:

* **Error**  
A simple boolean flag that lets you quickly check for an error.

* **ErrorMessage**  
An error message string that shows any compilation errors along with line numbers into the generated code.

* **ErrorType**  
Determines whether the error is `Compilation` or `Runtime` error.

* **GeneratedCode and GeneratedCodeWithLineNumbers**  
If you receive Error Messages with line numbers it might be useful to have the source code that was generated to co-relate the error to. If `true` compiled source code is saved - otherwise this property is null.

#### Error Properties
`CSharpScriptExecution` has two error modes:

* Compilation Errors
* Runtime Errors

By default runtime errors are captured and forwarded into the error properties of this class. You can always check the `Error` property to determine if a script error occurred. 

If you perfer you can set the `ThrowExceptions` property to `true` to throw on execution errors.

## Executing Code
Let's start with the most generic execution functionality which is `ExecuteCode()` and `ExecuteCodeAsync()` which let you execute a block of code, optionally pass in parameters and return a result value.

The code you pass can use a `object[] parameters` array, to access any parameters you pass and can `return` a result value that you can pick up when executing the code. Note that you can also replace `parameters[0]` with `@0` and `parameters[1]` with  `@1` and so on.

### ExecuteCode()
The following is a simple example of a code snippet that performs a calculation by adding to values and returning a string:

```cs
var script = new CSharpScriptExecution() { SaveGeneratedCode = true };
script.AddDefaultReferencesAndNamespaces();

var code = $@"
// pick up and cast parameters
int num1 = (int) @0;   // same as parameters[0];
int num2 = (int) @1;   // same as parameters[1];

var result = $""{{num1}} + {{num2}} = {{(num1 + num2)}}"";

Console.WriteLine(result);  // just for kicks in a test

return result;
";

// should return (`"10 + 20 = 30"`)
string result = script.ExecuteCode(code,10,20) as string;

Console.WriteLine($"Result: {result}");
Console.WriteLine($"Error: {script.Error}");
Console.WriteLine(script.ErrorMessage);
Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);

Assert.IsFalse(script.Error, script.ErrorMessage);
Assert.IsTrue(result.Contains(" = 30"));
```

Note that the `return` in the code snippet is optional so you can omit it if you don't need to pass anything back.

This non-generic version returns a result of type `object`. You can use generic overloads to specify the result type as well as an optional single input model type. 

### Basic Error Handling
If an error occurs during compilation the error is handled and the `Error` and `ErrorMessage` properties are set. If a runtime error occurs the code fires an exception in your code. You can also access the generated source code that is actually executed using `GeneratedClassCode` or `GeneratedClassCodeWithLineNumbers` - if the `SaveGeneratedCode` property is `true`.

```cs
var script = new CSharpScriptExecution() { SaveGeneratedCode = true };
script.AddDefaultReferencesAndNamespaces();

string result = null;
result = script.ExecuteCode(code,10,20) as string;

// compilation or runtime error
if (script.Error)   
{
	Console.WriteLine(script.ErrorMessage + " (" + script.ErrorType + ")");
    Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);
}
else 
{
	Console.WriteLine($"Result: {result}");
}
```

### ExecuteCodeAsync()
If your code snippet requires `await` calls or uses `Task` operations, you probably want to execute your code using `async` `await` functionality.

```cs
var script = new CSharpScriptExecution() {SaveGeneratedCode = true,};
script.AddDefaultReferencesAndNamespaces();

string code = @"
await Task.Run(async () => {
    {
        Console.WriteLine($""Time before: {DateTime.Now.ToString(""HH:mm:ss:fff"")}"");        
        await Task.Delay(20);
        Console.WriteLine($""Time after: {DateTime.Now.ToString(""HH:mm:ss:fff"")}"");        
    }
});

return $""Done at {DateTime.Now.ToString(""HH:mm:ss:fff"")}"";
";

string result = await script.ExecuteCodeAsync<string>(code, null);

if (script.Error)   // compile error
{
	Console.WriteLine(script.ErrorMessage);
    Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);
    return;
}

// all good!
Console.WriteLine($"Result: {result}");
```

Note also in this code I'm using the generic `ExecuteCodeAsync<TResult>()` method which allows me to explicitly specify what type to return, to avoid the `object` conversion from the first sample.

> From here on out I'm not going to show error handling in the samples except where relevant to keep samples brief

### More Control with ExecuteMethod()
If you need more control over your code execution, rather than having a method created for execution **you can provide a complete method** as a string instead. The method can include a method header and `return` value. This allows you to exactly specify what types to pass as parameters, what types to return etc.

> If your method has an `async` or `Task` or `Task<T>` signature you should likely use `ExecuteMethodAsync()` to call the method and `await` the call.


```cs
var script = new CSharpScriptExecution() { SaveGeneratedCode = true };
script.AddDefaultReferencesAndNamespaces();

string code = $@"
public string HelloWorld(string name)
{{
	string result = $""Hello {{name}}. Time is: {{DateTime.Now}}."";
	return result;
}}";

string result = script.ExecuteMethod(code, "HelloWorld", "Rick") as string;
```

As you can see I'm providing a full method signature with signature header, body and a return value. Because I'm writing the method explicitly I can strongly type the method inputs and result values explicitly.

### ExecuteMethodAsync()
The async version looks like this:

```cs
var script = new CSharpScriptExecution() { SaveGeneratedCode = true };
script.AddDefaultReferencesAndNamespaces();

string code = $@"
public async Task<string> HelloWorldAsync(string name)
{{
	await Task.Delay(10);  // some async task
	string result = $""Hello {{name}}. Time is: {{DateTime.Now}}."";
	return result;
}}";

string result = await script.ExecuteMethodAsync<string>(code, "HelloWorldAsync", "Rick");
```

### Evaluating an expression: EvaluateMethod()
If you want to evaluate a single expression, there's a shortcut `Evalute()` method that works pretty much the same:

```csharp
var script = new CSharpScriptExecution() { SaveGeneratedCode = true };
script.AddDefaultReferencesAndNamespaces();

// Numbered parameter syntax is easier
var result = script.Evaluate<decimal>("(decimal) @0 + (decimal) @1", 10M, 20M);

Console.WriteLine($"Result: {result}");  // 30
Console.WriteLine(script.ErrorMessage);
```

I'm using the generic version here, but there are overloads that return `object` more generically.

The async version works similar and allows you to evaluate expressions of methods or code that is async:

```csharp
var script = new CSharpScriptExecution() {SaveGeneratedCode = true,};
script.AddDefaultReferencesAndNamespaces();

string code = $@"
await Task.Run( async ()=> {{
	await Task.Delay(1);
	return (decimal) @0 + (decimal) @1;
}})";

// Numbered parameter syntax is easier
var result = await script.EvaluateAsync<decimal>(code, 10M, 20M);

Console.WriteLine($"Result: {result}");  // 30
Console.WriteLine($"Error: {script.Error}");
```

### Compiling and Executing Entire Classes
You can also generate an entire class, load it and then execute methods on it using the `CompileClass()` method. This method passes in a complete C# class as a string and returns back an instance of the class as a `dynamic` object. 

```csharp
var script = new CSharpScriptExecution() { SaveGeneratedCode = true };
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
			var result = $""{{num1}}  *  {{num2}} = {{ num1 * num2 }}"";
			Console.WriteLine(result);
			
			result = $""Take two: {{ result ?? ""No Result"" }}"";
			Console.WriteLine(result);
			
			return result;
		}}
	}}
}}";

// need dynamic since current app doesn't know about type
dynamic math = script.CompileClass(code);

Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);
Assert.IsFalse(script.Error,script.ErrorMessage);
Assert.IsNotNull(math);

string addResult = math.Add(10, 20);
string multiResult = math.Multiply(3 , 7);

Assert.IsTrue(addResult.Contains(" = 30"));
Assert.IsTrue(multiResult.Contains(" = 21"));

// if you need access to the assembly or save it you can
var assembly = script.Assembly; 
```

## Template Script Execution: ScriptParser
Template script execution allows you to transform a block of text with embedded C# expressions to make the text dynamic by using the `ScriptParser`class. It uses HandleBars like syntax with `{{ }}` expressions and `{{%  }}` code statements that allow for structured operations like `if` blocks or `for`/`while` loops.

You can embed C# expressions and code blocks to expand dynamic content that is generated at runtime. This class works by taking a template and turning it into executing code that produces a string output result.

This class has two operational methods:

* `ExecuteScript()`  
This is the highlevel execution method that you pass a template and a model to, and it processes the template, expanding the data and returns a string of the merged output.

* `ParseScriptToCode()`  
This method takes a template and parses it into a block of C# code that can be executed to produce a string result of merged content. This is a lower level method that can be used to customize how the code is eventually executed. For example, you might want to combine multiple snippets into a single class via multiple methods rather than executing individually.

### Automatic Script Processing with ScriptParser
The `ExecuteScript()` method is the all in one method that parses and executes the script and model passed to it.

Here's how this works:

```cs
var model = new TestModel {Name = "rick", DateTime = DateTime.Now.AddDays(-10)};

string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{Model.Name}}
{{% } }}

And we're done with this!
";

var scriptParser = new ScriptParser();

// add dependencies - sets on .ScriptEngine instance
scriptParser.AddAssembly(typeof(ScriptParserTests));
scriptParser.AddNamespace("Westwind.Scripting.Test");

// Execute the script
string result = scriptParser.ExecuteScript(script, model);

Console.WriteLine(result);

Console.WriteLine(scriptParser.ScriptEngine.GeneratedClassCodeWithLineNumbers);
Console.WriteLine(scriptParser.ErrorType);  // if there's an error
Assert.IsNotNull(result, scriptParser.ScriptEngine.ErrorMessage);
```

Notice that `ScriptParser()` mirrors most of the `CSharpScriptExecution` properties and methods. Behind the scenes there is a `ScriptEngine` property that holds the actual `CSharpScriptExecution` instance that will be used when the template is executed. You can optionally override the `ScriptEngine` instance although that should be rare.


### Manual Parsing
If you want direct access to the parsed code you can use `ParseScriptToCode()` to parse a template into C# code and return it as a string. We can then manually execute the code or create a custom execution strategy such as combining multiple templates into a single class.

Here's the basic functionality to parse a template and then **manually** execute as a method:

```csharp
var model = new TestModel {Name = "rick", DateTime = DateTime.Now.AddDays(-10)};

string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{Model.Name}}
{{% } }}

And we're done with this!
";


var scriptParser = new ScriptParser();

// Parse template into source code
var code = scriptParser.ParseScriptToCode(script);

Assert.IsNotNull(code, "Code should not be null or empty");

Console.WriteLine(code);

// ScriptEngine is a pre-configured CSharpScriptExecution instance
scriptParser.AddAssembly(typeof(ScriptParserTests));
scriptParser.AddNamespace("Westwind.Scripting.Test");

var method = @"public string HelloWorldScript(TestModel Model) { " +
             code + "}";

// Execute using the internal CSharpScriptExecution instance
var result = scriptParser.ScriptEngine.ExecuteMethod(method, "HelloWorldScript", model);

Console.WriteLine(scriptParser.GeneratedClassCodeWithLineNumbers);
Assert.IsNotNull(result, scriptParser.ErrorMessage);

Console.WriteLine(result);
```

This is a bit contrived since this in effect does the same thing that `ExecuteScript()` does implicicitly. However, it can be useful to retrieve the code and use in other situations, such as building a class with several generated template methods rather than compiling and running each template in it's own dedicated assembly.

Not a very common use case but it's there if you need it.

### ScriptParser Methods and Properties

**Main Execution** 

* `ExecuteScript()`
* `ExecuteScriptAsync()`

**Script Parsing** 

* `ParseScriptToCode()`

**C# Script Engine Configuration and Access**

* `ScriptEngine` 
* `AddAssembly()`
* `AddAssemblies()`
* `AddNamespace()`
* `AddNamespaces()`

The `ScriptEngine` property is initialized using default settings which use:

* `AddDefaultReferencesAndNamespaces()`
* `SaveGeneratedCode = true`

You can optionally replace `ScriptParser` instance with a custom instance that is configured exactly as you like:

```cs
var scriptParser = new ScriptParser();

var exec = new CSharpScriptExection();
exec.AddLoadedReferences();

scriptParser.ScriptEngine = exec;

string result = scriptParser.ExecuteScript(template, model);
```

**Error and Debug Properties**

* `ErrorMessage`
* `ErrorType`
* `GeneratedClassCode`
* `GeneratedClassCodeWithLineNumbers`

> The various `Addxxxx()` methods and error properties are directly forwarded from the `ScriptEngine` instance as readonly properties.

### Some Template Usage Examples
An example usage is for the [Markdown Monster Editor](https://markdownmonster.west-wind.com/) which uses this library to provide text snippet expansion into Markdown (or other) documents.

A simple usage scenario might be to expand a DateTime stamp into a document as a snippet via a hotkey or explicitly

```markdown
---
- created on {{ DateTime.Now.ToString("MMM dd, yyyy") }}
```

You can also use this to expand logic. For example, this is for a custom HTML expansion in a Markdown document by wrapping an existing selection into the template markup:

```markdown
### Breaking Changes

<div class="alert alert-warning">
{{ await Model.ActiveEditor.GetSelection() }}
</div>
```

Here's an example that uses script to retrieves some information from the Web parses out a version number and embeds a string with the version number into the document:

```markdown
{{%
var url = "https://west-wind.com/files/MarkdownMonster_version.xml";

var wc = new WebClient();
string xml = wc.DownloadString(url);
string version =  StringUtils.ExtractString(xml,"<Version>","</Version>");
}}
# Markdown Monster v{{version}}
```

This is a bit contrived but you can iterate over a list of open documents and display them in the template output:

```markdown
**Open Editor Documents**

{{% foreach(var doc in Model.OpenDocuments) { }}
* {{ doc.Filename }}
{{% } }}

```

## Usage Notes
Code snippets, methods and evaluations as well as templates are compiled into assemblies which are then loaded into the host process. Each script or snippet by default creates a new assembly.

### Cached Assemblies
Assemblies are cached based on the code that is used to run them so repeatedly running the **exact same template** uses the cached version automatically.

### No Unloading
Assemblies, once loaded, cannot be unloaded until the process shuts down. While overhead for loading a new assembly is not great it does add overhead with every unique instantiation of a code snippet or template.

## Change Log

### 1.1

* **Breaking Change: ScriptParser  Refactor to non-static Class**  
The original implementation of ScriptParser relied on several `static` methods to make it very easy to access and use. In this release ScriptParser is now a class that has be instantiated.

* **ScriptParser includes ScriptEngine Property**  
The Script execution engine is now an auto-initialized property on the ScriptParser class. You can customize this instance or replace it with a custom instance.

* **ScriptParser Simplification for ScriptEngine Access**  
Rather than requiring configuration directly on the `ScriptEngine` instance for dependencies and errors, the relevant properties have been forwarded to the `ScriptParser` class. You can now use `AddAssembly()`/`AddNameSpace()` and the various `Error`, `ErrorMessage` etc. properties directly on the `ScriptParser` instance which makes the code cleaner and exposes the relevant features only.


### 1.0.10

* **Add Stream Inputs for Class Compilation**  
The various `CompileClass()` methods now take stream inputs to allow directly reading from files or memory streams.

* **Clean up Default References and Namespaces for .NET Core**  
Modified the default reference imports to create a minimal but somewhat functional baseline that allows running a variety of code running against common BCL/FCL functionality.

### 1.0

* **Switch to Roslyn CodeAnalysis APIs**  
Switched from CodeDom compilation to Roslyn CodeAnalysis compilation which improves compiler startup, compilation performance and provides more detailed compilation information on errors.

* **Add Support for Async Execution**  
You can now use various `xxxAsync()` overloads to execute methods as `Task` based operations that can be `await`ed and can use `await` inside of scripts.

* **Add ScriptParser for C# Template Scripting**  
Added a very lightweight scripting engine that uses *Handlebars* style syntax for processing C# expressions and code blocks. Look at the `ScriptParser` class.

#### BREAKING CHANGES FOR v1.0
This version is a breaking change due to the changeover to the Roslyn APIs. While the APIs have stayed mostly the same, some of the dependent types have changed. Runtime requirements are also different with different libraries that are installed differently than the CodeDom dependencies. You may have to explicitly cleanup old application folders.

### Version 0.4.5

* **Last CodeDom Version**
This version is the last version that works with CodeDom and that is fixed to .NET Framework. All later versions support .NET Framework and .NET Core. 

### **Version 0.3**  

* **Updated to latest Microsoft CodeDom libraries**  
Updated to the latest Microsoft CodeDom compilation libraries which streamline the compiler process for Roslyn compilation with latest language features.

* **Remove support to pre .NET 4.72**  
In order to keep the runtime dependencies down switched the library target to `net472` which is .NET Standard compliant and so pulls in a much smaller set of dependencies. This is potentially a breaking change for older .NET applications, which will have to stick with the `0.2.x` versions.

* **Switch Projects to SDK Projects**  
Switched from classic .NET projects to the new simpler .NET SDK project format for the library and test projects.

## License
This library is published under **MIT license** terms.

**Copyright &copy; 2014-2022 Rick Strahl, West Wind Technologies**

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sub license, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Give back
If you find this library useful, consider making a small donation:

<a href="https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=KPJQRQQ9BFEBW" 
    title="Find this library useful? Consider making a small donation." alt="Make Donation" style="text-decoration: none;">
	<img src="https://weblog.west-wind.com/images/donation.png" />
</a>


