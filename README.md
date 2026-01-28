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

## Configuration

Disable the rule via `.editorconfig`:
```ini
[*.cs]
dotnet_diagnostic.COP001.severity = none
```

