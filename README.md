# Call Or Pass Analyzer

Diagnostic extension for the .NET Compiler Platform ("Roslyn").

## The Rule

**COP001:** An object passed to a method is either used or passed to another method, but not both.

This is based on the "Either call or pass" principle from [Five Lines of Code](https://www.manning.com/books/five-lines-of-code) by Christian Clausen.

## Example
```csharp
// ❌ Violation: items is both called AND passed
void Process(List<int> items)
{
    items.Add(42);      // Member access (call)
    Save(items);        // Passed as argument
}

// ✅ OK: items is only called
void Process(List<int> items)
{
    items.Add(42);
    items.Clear();
}

// ✅ OK: items is only passed
void Process(List<int> items)
{
    Validate(items);
    Save(items);
}
```

## Why This Rule? 

I stumbled upon this principle in the Book "Five Lines of Code" and immediately liked it. It is a very simple principle that can increase the readability of your code. It has similarities with the [IOSP (Integration, Operation, Segregation)](https://ralfwestphal.substack.com/p/integration-operation-segregation?utm_source=substack&utm_campaign=post_embed&utm_medium=web) principle.

When a method both uses a parameter directly and additionally passes it to other methods, it likely operates on two different abstraction levels.

```csharp
// ❌ Violation: items is both called AND passed
void Process(List<int> items)
{
    items.Add(42);      // Low-level: direct manipulation
    Save(items);        // High-level: delegation
}
```

Reading this code forces you to switch between "detail thinking" and "big picture thinking". This makes the code unnecessarily harder to understand. 

An improvement is to split the method into focused units

```csharp

✅ OK: items is only passed
// High-level: Coordinates the workflow (Integration)
void Process(List<int> items)
{
    AddDefaults(items);
    Save(items);
}

// ✅ OK: items is only called
// Low-level: Does the actual work (Operation)
void AddDefaults(List<int> items)
{
    items.Add(42);
}
```

## Installation

### Option 1: NuGet Package (recommended)

The analyzer runs during every build and integrates into all editors that support Roslyn (Visual Studio, Rider, VS Code with C# Dev Kit).

Add the package to your project:

```
dotnet add package CallOrPassAnalyzer
```

Or via the Package Manager Console in Visual Studio:

```
Install-Package CallOrPassAnalyzer
```

Because the package is marked as `DevelopmentDependency`, it does not become a runtime dependency of your project and will not appear in the published output.

### Option 2: VSIX Extension

The VSIX installs the analyzer globally into Visual Studio. It is active for every solution you open without any per-project configuration.

1. Build the `CallOrPassAnalyzer.Vsix` project.
2. Close Visual Studio
3. Double-click the generated `.vsix` file and follow the installer
4. Reopen Visual Studio — the analyzer is active immediately

Supported editions: Community, Professional, Enterprise (Visual Studio 2026, versions 18.x, 64-bit).

> **Note:** Diagnostics from a VSIX analyzer are shown in the editor and Error List while you work, but are **not reported during a command-line build** (`dotnet build` / MSBuild). Use the NuGet package if you need build-time diagnostics in CI.

### Option 3: F5 Debug Instance (for development)

Use this when you want to test or develop the analyzer itself. Pressing F5 on the `CallOrPassAnalyzer.Vsix` project launches an experimental instance of Visual Studio with the extension loaded.

1. Set `CallOrPassAnalyzer.Vsix` as the startup project
2. Press **F5**
3. In the experimental instance, open any C# solution — the analyzer is active and breakpoints in the analyzer code are hit

The experimental instance has its own separate settings and extension registry, so it does not interfere with your main Visual Studio installation.

## Configuration

Disable the rule via `.editorconfig`:
```ini
[*.cs]
dotnet_diagnostic.COP001.severity = none
```

