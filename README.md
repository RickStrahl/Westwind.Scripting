# Westwind CSharp Scripting
### Dynamically compile and execute CSharp code at runtime

This small `CSharpScripting` class provides an easy way to compile and execute C# on the fly at runtime using the .NET compiler services. This is ideal to provide support for addin's and application automation tasks that are user configurable.

The library supports both the latest Roslyn compiler and classic CSharp compilation.

> #### Requires Roslyn Code Providers for your Project
> If you want to use Roslyn compilation you have to make sure you add the `Microsoft.CodeDom.CompilerServices` NuGet Package to your project to provide the required compiler binaries for your application. This should be added to the application's start project.
>
> Note that this adds a sizable chunk of files to your application's output folder in the `roslyn` folder. If you don't want this you can use the classic compiler, at the cost of not having access to C# 6+ features.

## Usage
Using the `CSharpScriptExecution` class is very easy. It works by letting you provide either a simple code snippet that can optionally `return` a value, or a complete method signature with a method header and return statement. You can also provide multiple method that can be called explicitly using the `InvokeMethod()` operation.

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

int num1 = (int)parameters[0];
int num2 = (int)parameters[1];

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

### Executing a Method
Another way to execute code is to provide a full method body which is a little more explicit and makes it easier to reference parameters passed in. 

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