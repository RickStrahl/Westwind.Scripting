# Westwind CSharp Scripting

**Dynamically compile and execute C# code at runtime, as well as a light weight, Handlebars style C# template script engine**

[![NuGet](https://img.shields.io/nuget/v/Westwind.Scripting.svg)](https://www.nuget.org/packages/Westwind.Scripting/)
[![](https://img.shields.io/nuget/dt/Westwind.Scripting.svg)](https://www.nuget.org/packages/Westwind.Scripting/)

This small library provides an easy way to compile and execute C# code from source code provided at runtime. It uses Roslyn to provide compilation services for string based code via the `CSharpScriptExecution` class.

There's also a lightweight, self contained C# [script template engine](ScriptAndTemplates.md) via the `ScriptParser` class that can evaluate expressions and structured C# statements using  Handlebars-like (`{{ expression }}` and `{{% code-block }}`) script templates. Unlike other script engines, this engine uses plain C# syntax for expressions and code block execution.

Get it from [Nuget](https://www.nuget.org/packages/Westwind.Scripting/):

```ps
dotnet add package Westwind.Scripting
```

It supports the following targets:

* .NET 9.0 (net9.0), .NET 8.0 (net8.0)
* Full .NET Framework (net472)
* .NET Standard 2.0 (netstandard2.0)


For more detailed information and a discussion of the concepts and code that runs this library, you can also check out this introductory blog post:

* [Runtime Code Compilation Revisited for Roslyn](https://weblog.west-wind.com/posts/2022/Jun/07/Runtime-C-Code-Compilation-Revisited-for-Roslyn)

* [Change Log](Changelog.md)

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
* [Template Scripting Engine using Handlebars-like C# syntax](ScriptAndTemplates.md)
    * Script Execution from strings
    * Script Execution from files
    * Support for Partials, Layout and Sections

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

These `CompileXXX()` methods provide compilation only without execution and create an instance, type or assembly respectively. You can cache these in your application for later re-use and **much faster execution**. 

Use these methods if you need to repeatedly execute the same code and when performance is important as using re-used cached instances is an order of magnitude faster than using the `ExecuteXXX()` methods repeatedly.

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

Once enabled you can embed references and references in script code like this:

```cs
#r MarkdownMonster.exe
using MarkdownMonser

var title = mmApp.Configuration.ApplicationName;
```

The assembly is reference and any namespaces are moved to the top of the class and removed from the execution code.


Assemblies are searched for in the application folder and in the runtime folder.

> #### #r only works with String Scripts/Classes
> Reference and Usings parsing only works with string code inputs. If you need reference syntax make sure you convert your stream to string first.

> #### #r only for classes, #r and using for Snippets and Methods
> Class compilation parses only #r references. Method and Code execution - (ie. non-self contained code files) parse both `#r` and `using`.

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

### Reusing Compiled Classes, Types and Assemblies for Better Performance
If you plan on **repeatedly calling the same C# code**, you want to avoid re-compiling or even reloading the code from string or even a cached assembly using the `ExecuteXXX()` methods. While these methods cache code after initial compilation, they still have to re-load the type to execute each time, and then execute using Reflection. Initial compilation is always very slow, but even cached code assembly and type loading has significant overhead, that is **much slower** than directly invoking code.

For multiple run code we recommend you use a lower level approach using the `CompileXXX()` methods to create an instance or type, and hang on to it in your application. Whenever you need to re-run the code you can then use the cached instance or type to execute your code. This removes assembly and type loading which add significant overhead.

Performance using these cached instances will be an order of magnitude faster than using `ExecuteMethod()` or `ExecuteCode()` (even with cached assemblies). Cached instances can simply make a `dynamic` or `Reflection` call to the relevant code without reloading or matching code to an assembly and type creation.

If speed is important this is the most efficient approach.

## Template Script Execution with Script Parser
Template and Script Execution is documented in a separate document:

* [Basic Template Script Execution](ScriptAndTemplates.md)
* [Extended File based Script Execution](ScriptTemplates.md) 


## Usage Notes
Code snippets, methods and evaluations as well as templates are compiled into assemblies which are then loaded into the host process. Each script or snippet by default creates a new assembly.

### Cached Assemblies
Assemblies are cached based on the code that is used to run them so repeatedly running the **exact same template** uses the cached version automatically.

You can disable this functionality with the `DisableAssemblyCaching` which can be a little more efficient and resource conscious if you know that scripts are either always recreated and never reused.

### No Unloading
Assemblies, once loaded, cannot be unloaded until the process shuts down or the AssemblyLoadContext is unloaded. In  .NET Framework there's no way to unload, but in .NET Core you can use an alternate AssemblyLoadContext.

### Alternate AssemblyLoadContext for  Unloading (.NET Core)
In .NET Core it's possible to unload assemblies using the `CSharpScriptExecution.AlternateAssemblyLoadContext` which if provided can be used to unload assemblies loaded in the context conditionally.

## Westwind.Scripting FAQ

### In Memory Types should only be used for top level Compilation
If you are creating multiple compilations that are dynamically compiled, and you need to reference one dynamic compilation in a second compilation, **you have to ensure that referenced type was compiled to disk, not into memory**. 

The reason for this revolves around the fact that `Activator.CreateInstance()` or other similar load operations can't resolve the dynamically compiled type at runtime **even if it was previously** loaded. (see [here](https://github.com/RickStrahl/Westwind.Scripting/issues/7) and [here](https://github.com/dotnet/roslyn/issues/65627)).

Bottom line: If you need a dynamically compiled type from another compilation **use to-disk compilation for the referenced type's code**. 

Said another way, you can only use in-memory compilation for top level execution, not for inclusion as a reference unless you build a custom assembly resolver (which I have not been able to figure out since there's no physical assembly to resolve from).

### Westwind.Scripting Performance
A number of people have raised issues commenting that startup performance is slow. Yes that's the case, because the first time this library is called it has to load  Roslyn which is a huge library and it takes time to load; it's slow. Depending on the type of machine you're running on this can take a couple of seconds for the first hit. So yes that overhead will happen and there's no way to avoid it.

There are couple of things to mitigate this issue:

* Pre-compile and Save your compiled assembly
* Try to pre-load Roslyn at App startup


#### Precompile your Code and Save Assembly
At the end of the day this library compiles code that ends up in an assembly, so rather than compiling your code every time you execute it, try to compile ahead of time and save your compiled assembly when you capture the code to be executed. You can store the assembly for later execution either on disk or some other stream based data store. 

This may allow you to avoid loading Roslyn at all in most runtime situations, and only load it when you add new code that needs to be compiled. For example, if you're adding code snippets that a user enters, you can compile and capture the code snippet when the user enters the code. Then when the application starts you can load the already compiled assembly to execute the code. 

Another related tip especially for snippet libraries that are user provided is to combine many snippets into a single class and map each snippet to a method. So rather than loading many types you can load up one type of code snippets that get executed as needed from an already loaded instance.

#### Pre-Load Roslyn on Startup
You can warm up Roslyn **in the background** during application startup, using `RoslynLifetimeManager.WarmupRoslyn()`. This method does a `Task.Run()` to create a very simple expression that is compiled into memory and executed to force Roslyn to load outside of the main application thread. 

To do this call:

```csharp
// at app startup - runs a background task, but don't await
_ = RoslynLifetimeManager.WarmupRoslyn();
```


### Performance Tips

#### Running Code in a Loop
If you are running code repetitively, you should avoid using the various `ExecuteXXX()` methods and instead use `CompileClass()` to create a type instance, then re-use that type instance for execution. Although this library caches assemblies for the exact same code and doesn't recompile it, `ExecuteXXX()` methods still have to load an instance of the type each time which adds a bit of overhead.

It's much more efficient using `CompileClass()` to create a type instance, and then calling a method on it. Better yet, cache the `MethodInfo` to execute or create a delegate that can be reused for the specific method.

## License
This library is published under **MIT license** terms.

**Copyright &copy; 2014-2022 Rick Strahl, West Wind Technologies**

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sub license, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Give back
This library is free to use and integrate with for both personal and commercial use.

If you find this library useful, consider [sponsoring the author](https://github.com/sponsors/RickStrahl), or making a small donation:

<a href="https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=KPJQRQQ9BFEBW" 
    title="Find this library useful? Consider making a small donation." alt="Make Donation" style="text-decoration: none;">
	<img src="https://weblog.west-wind.com/images/donation.png" />
</a>