# Westwind Csharp Scripting

**Dynamically compile and execute CSharp code at runtime**

[![NuGet](https://img.shields.io/nuget/v/Westwind.Scripting.svg)](https://www.nuget.org/packages/Westwind.Scripting/)
[![](https://img.shields.io/nuget/dt/Westwind.Scripting.svg)](https://www.nuget.org/packages/Westwind.Scripting/)

> Note: Version 1.0 is a major version update that might break existing code due to dependency changes. Version 1.0 switches to native Roslyn APIs from CodeDom, which results in different assembly imports and runtime distribution requirements!

Get it from [Nuget](https://www.nuget.org/packages/Westwind.Scripting/):

```text
Install-Package Westwind.Scripting
```
<small>(currently you need to use the `-IncludePreRelease` flag for v1.0 that supports .NET Core and .NET Standard)</small>

It supports the following targets:

* Full .NET Framework (net462)
* .NET 6.0 (net60)
* .NET Standard 2.0 (netstandard2.0)
 
The small library provides an easy way to compile and execute C# code from source code provided at runtime. This library uses Roslyn to provide compilation services for string based code via the `CSharpScriptExecution` class and script templates via the `ScriptParser` class.

This execution class makes is very easy to integrate simple scripting or text merging features into applications with minimal effort.

## Features
* Easy C# code compilation and execution for:
	* Code blocks 
	* Full methods (method header/result value)
	* Full classes (compile and load)
	* Expressions  (evaluate expressions)
* Caching of already compiled code
* Ability to compile entire classes and load, execute them
* Automatic Assembly Cleanup at shutdown
* Error Handling
	* Intercept compilation and execution errors
	* Detailed compiler error messages
	* Access to compiled output w/ line numbers
* Roslyn Warmup 
* Template Scripting Engine using Handlebars-like with C# syntax

### Runtime Compilation and Execution
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

### Script Parser
Script Templating using a Handlebars like syntax that can expand **C# expressions** and **C# structured code** in text templates that produce transformed text output, can be achieved using the `ScriptParser` class.

Methods:

* `ParseScriptToCode()`
* `ExecuteScript()`     
* `ExecuteScriptAsync()`

The Expansion syntax used is:

* `{{ expression }}`
* `{{% openBlock }}` other text or expressions   `{{% endblock }}`

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

```csharp
var script = new CSharpScriptExecution() {SaveGeneratedCode = true,};
script.AddDefaultReferencesAndNamespaces();

// Numbered parameter syntax is easier
var result = script.Evaluate<decimal>("(decimal) @0 + (decimal) @1", 10M, 20M);
	
// Full syntax
//var result = script.Evaluate<decimal>("(decimal) parameters[0] + (decimal) parameters[1]", 10M, 20M);
```

### Parse and Execute a Script Template

```csharp
var model = new TestModel { Name = "rick", DateTime = DateTime.Now.AddDays(-10) };

string script = @"
Hello World. Date is: {{ Model.DateTime.ToString(""d"") }}!
{{% for(int x=1; x<3; x++) {
}}
{{ x }}. Hello World {{Model.Name}}
{{% } }}

And we're done with this!
";

// Optional - if you want access to config, code and error info
var exec = new CSharpScriptExecution() { SaveGeneratedCode = true }
exec.AddDefaultReferencesAndNamespaces();

exec.AddAssembly(typeof(ScriptParserTests));
exec.AddNamespace("Westwind.Scripting.Test");

string result = ScriptParser.ExecuteScript(script, model, exec);
```

which produces:

```txt
Hello World. Date is: 5/6/2022!

1. Hello World rick

2. Hello World rick

And we're done with this!
```

## Usage
Using the `CSharpScriptExecution` class is very easy. It works with code passed as strings for either a Code block, expression, one or more methods or even as a full C# class that can be turned into an instance.

There are methods for:

* Generic code execution
* Complete method execution
* Expression Evaluation
* In-memory class compilation and loading

For all these tasks there are a number of common operations that involve setting up the compilation environment which involves adding dependencies: Assemblies and Namespaces that are required to compile the code.

> #### Important: Large Roslyn Dependencies
> If you choose to use this library, please realize that you will pull in a very large dependency on the `Microsoft.CodeAnalysis` Roslyn libraries which account for  10+mb of runtime files that have to be distributed with your application.

### Setting up for Compilation: CSharpScriptExecution
Compiling code is easy - setting up for compilation and ensuring that all your dependencies are available is a little more complicated and also depends on whether you're using full .NET Framework or .NET Core or .NET Standard.

In order to compile code the compiler requires that all dependencies are referenced both for assemblies that need to be compiled against as well as any namespaces that you plan to access in your code and don't want to explicitly mention.

#### Adding Assemblies and Namespaces
There are a number of methods that help with this:

* AddDefaultReferencesAndNamespaces()
* AddAssembly()
* AddAssemblies()
* AddNamespace()
* AddNamespaces()

Initial setup for code execution then looks like this:

```cs
var script = new CSharpScriptExecution()
{
    SaveGeneratedCode = true  // errors and trouble shooting
};

// Add default references - by default host app assemblies are loaded
// so script inherits host application refs
// You can reduce load time by loading only explicit assemblies you need
script.AddDefaultReferencesAndNamespaces(loadLoadedAssemblies: false);

// Add any additional dependencies
script.AddAssembly(typeof(MyApplication));       // by type
script.AddAssembly("Westwind.Utilitiies.dll");   // by assembly file

// Add additional namespaces you might use in your code snippets
script.AddNamespace("MyApplication");
script.AddNamespace("MyApplication.Utilities");
script.AddNamespace("Westwind.Utilities");
script.AddNamespace("Westwind.Utilities.Data");
```

### Configuration
The `CSharpScriptExecution` has only a few configuration options available:

* **SaveGeneratedCode**  
If `true` captures the generated class code for the compilation that is used to execute your code. This will include the class and method wrappers around the code. You can use the `GeneratedCode` or `GeneratedCodeWithLineNumbers` properties to retrieve the code. The line numbers will match up with compilation errors return in the `ErrorMessage` so you can display an error message with compiler errors along with the code to optionally review the code in failure. 

* **OutputAssembly**  
You can optionally specify a filename to which the assembly is compiled. If this value is not set the assembly is generated in-memory which is the default.

* **GeneratedClassName**  
By default the class generated from any of the code methods generates a random class name. You can override the class name so you can load any generated types explicitly. Generally it doesn't matter what the class name is as the dynamic methods find the single class generated in the assembly.

* **ThrowExceptions**  
If `true` compiler errors will throw runtime execution exceptions rather than silently failing after setting error properties. 

The default is `false` and the recommended approach is to explicitly check for errors after compilation and execution, by checking `Error`, `ErrorMessage` and `LastException` properties which we highly recommend.

*Note: Compiler errors don't throw - only runtime errors do. Compiler errors set properties of the object as do execution errors when `ThrowExecptions =  false`.*

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

### Error Properties
`CSharpScriptExecution` has two error modes:

* Compilation Errors
* Runtime Errors

The latter are not handled and need to be captured by your hosting application. You'll likely want to wrap a `try/catch` around any code you dynamically execute, either in the executing code itself, or the script execution code. You can capture the exception and treat it just like any other code in your host application.

Compilation errors are trapped and reported by the class

### Executing Generic Code: ExecuteCode()
Let's start with the most generic functionality which is `ExecuteCode()` and `ExecuteCodeAsync()` which lets you execute a block of code, optionally pass in parameters and return a result value.

The code you pass can use a `object[] parameters` array, to access any parameters you pass and can `return` a result value that you can pick up when executing the code.

#### ExecuteCode()
The following is a simple example of a code snippet that performs a calculation by adding to values and returning a string:

```cs
var script = new CSharpScriptExecution()
{
    SaveGeneratedCode = true,
};
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

#### Basic Error Handling
If an error occurs during compilation the error is handled and the `Error` and `ErrorMessage` properties are set. If a runtime error occurs the code fires an exception in your code. You can also access the generated source code that is actually executed using `GeneratedClassCode` or `GeneratedClassCodeWithLineNumbers` - if the `SaveGeneratedCode` property is `true`.

```cs
var script = new CSharpScriptExecution() 
{
	SaveGeneratedCode = true,
};
script.AddDefaultReferencesAndNamespaces();

string result = null;
try 
{
	result = script.ExecuteCode(code,10,20) as string;
}
catch(Exception ex)
{
	// runtime error or script engine unexpected error
	Console.WriteLine("Runtime Error: " + ex.Message);
	return
}

// compilation error
if (script.Error)   
{
	Console.WriteLine(script.ErrorMessage);
    Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);
}
else 
{
	Console.WriteLine($"Result: {result}");
}
```

#### ExcecuteCodeAsync()
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

string result = null;
try {
	result = await script.ExecuteCodeAsync<string>(code, null);
}
catch(Exception ex)
{
	Console.WriteLine("Runtime Error: " + ex.Message);
	return;
}

if (script.Error)   // compile error
{
	Console.WriteLine(script.ErrorMessage);
    Console.WriteLine(script.GeneratedClassCodeWithLineNumbers);
    return;
}

// all good!
Console.WriteLine($"Result: {result}");
```

Note also in this code I'm using the generic `ExecuteCodeAsync<TResult>()` method which allows me to explicitly specify what type to return to avoid the `object` conversion from the first sample.

> From here on out I'm not going to show error handling in the samples except where relevant to keep samples brief

### Need More Control: ExecuteMethod()
If you need more control over your code execution, rather than having a method created for execution **you can provide a complete method** as a string instead. The method can include a method header and `return` value. This allows you to exactly specify what types to pass as parameters, what types to return etc.

> If your method has an `async` or `Task` or `Task<T>` signature you should likely use `ExecuteMethodAsync()` to call the method and `await` the call.


```cs
var script = new CSharpScriptExecution()
{
    SaveGeneratedCode = true
};
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

#### ExecuteMethodAsync()
The async version looks like this:


```cs
var script = new CSharpScriptExecution()
{
    SaveGeneratedCode = true
};
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

#### CompileClass()
You can also generate an entire class, load it and then execute methods on it using the `CompileClass()` method. This method passes in a complete C# class as a string and returns back an instance of the class as a `dynamic` object. 

```csharp
var script = new CSharpScriptExecution()
{
    SaveGeneratedCode = true,
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
			var result = $""{{num1}}  *  {{num2}} = {{ num1 * num2 }}"";
			Console.WriteLine(result);
			
			result = $""Take two: {{ result ?? ""No Result"" }}"";
			Console.WriteLine(result);
			
			return result;
		}}
	}}
}}";

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
Updated to the latest Microsoft CodeDom compilation libraries which strealine the compiler process for Roslyn compilation with latest language features.

* **Remove support to pre .NET 4.72**  
In order to keep the runtime dependencies down switched the library target to `net472` which is .NET Standard compliant and so pulls in a much smaller set of dependencies. This is potentially a breaking change for older .NET applications, which will have to stick with the `0.2.x` versions.

* **Switch Projects to SDK Projects**  
Switched from classic .NET projects to the new simpler .NET SDK project format for the library and test projects.

## License
This library is published under **MIT license** terms.

**Copyright &copy; 2014-2021 Rick Strahl, West Wind Technologies**

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sub license, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Give back
If you find this library useful, consider making a small donation:

<a href="https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=KPJQRQQ9BFEBW" 
    title="Find this library useful? Consider making a small donation." alt="Make Donation" style="text-decoration: none;">
	<img src="https://weblog.west-wind.com/images/donation.png" />
</a>


