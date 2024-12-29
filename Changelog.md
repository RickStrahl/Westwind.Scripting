## Change Log

### 1.6

* **ScriptParser: New ScriptingDelimiters Property to replace passed Parameters**  
The script parser has a new property that contains all the scripting parameters, to simplify script delimiters and not forcing to pass it into each of the execute methods. This change removes all the optional delimiter parameters on the various `ExecuteXXX()` methods. There's a `ScriptingDelimtiers.Default` property.  
*This is a breaking change*

* **ScriptParser: Allow escaping of `{{` and `}}` with `\{\{` and `\}\}`**  
The script parser now allows to escapte `{{` and `}}` should they occur in your text. Since these are script delimiters they would otherwise not be usable.

* **ScriptParser: Added `{{: expression }}` Syntax for HtmlEncoding**  
To help with usage in HTML scenarios you can now use `{{: }}` to explicitly add HtmlEncoding to expression tags. Script **code** can now also use `ScriptParser.HtmlEncode(value)` to explicitly encode values written out in code.

### 1.2.7

* **CSharpScriptExecution.ExecuteMethodAsyncVoid for Async Void Methods**  
Due to the way tasks are handled in .NET it's not possible to cast a  void `Task` to a `Task<T>` result. For this reason separate methods are needed for the two versions of `ExecuteMethodAsync()` and `ExecuteMethodAsync<TResult>()` as `ExecuteMethodAsyncVoid()` and `ExecuteMethodAsyncVoid<TResult>()` respectively. Use these methods if you explicitly don't want to return a value.

### 1.2.5

* **Add CSharpScriptExecution.AlternateAssemblyLoadContext which allows for Assembly Unloading**  
In .NET Core you can now assign an alternate AssemblyLoadContext to load assemblies into via the `AlternateAssemblyLoadContext`, which allows for unloading of assemblies. [PR #19](https://github.com/RickStrahl/Westwind.Scripting/pull/19)

* **CSharpScriptExecution.DisableAssemblyCaching**  
By default this library caches generated assemblies based on the code that is passed in to execute. The `CSharpScriptExecution.DisableAssemblyCaching` property disables this caching in scenarios where you know code is never re-executed. [PR #19](https://github.com/RickStrahl/Westwind.Scripting/pull/19)

### 1.4

* **Add .NET 8.0 Target** 
Added explicit target for .NET 8.0.

### 1.3

* **Add .NET 7.0 Target**  
Added explicit target for .NET 7.0.

* **Updated Roslyn Libraries with support for C# 11**  
Updated to latest Roslyn compiler libraries that support C# 11 syntax.

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