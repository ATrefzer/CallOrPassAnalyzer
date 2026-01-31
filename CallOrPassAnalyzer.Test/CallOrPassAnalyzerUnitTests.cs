using Xunit;
using VerifyCS = CallOrPassAnalyzer.Test.Verifiers.CSharpAnalyzerVerifier<CallOrPassAnalyzer.CallOrPassAnalyzerAnalyzer>;

namespace CallOrPassAnalyzer.Test;

public class CallOrPassAnalyzerUnitTest
{
    [Fact]
    public async Task EmptyCode_NoDiagnostic()
    {
        var test = @"";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task OnlyMemberAccess_NoDiagnostic()
    {
        var test = @"
using System.Collections.Generic;

class TestClass
{
    void Method(List<int> items)
    {
        items.Add(42);
        items.Clear();
        var x = items.Count;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task OnlyPassAsArgument_NoDiagnostic()
    {
        var test = @"
using System.Collections.Generic;

class TestClass
{
    void Method(List<int> items)
    {
        Save(items);
        Process(items);
    }

    void Save(List<int> x) { }
    void Process(List<int> x) { }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task BothCallAndPass_Diagnostic()
    {
        var test = @"
using System.Collections.Generic;

class TestClass
{
    void Method(List<int> {|#0:items|})
    {
        items.Add(42);
        Save(items);
    }

    void Save(List<int> x) { }
}";

        var expected = VerifyCS.Diagnostic("COP001")
            .WithLocation(0)
            .WithArguments("items");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task LocalVariable_NoDiagnostic()
    {
        var test = @"
using System.Collections.Generic;

class TestClass
{
    void Method()
    {
        var items = new List<int>();
        items.Add(42);
        Save(items);
    }

    void Save(List<int> x) { }
}";
        // Lokale Variablen sind OK - nur Parameter werden geprüft
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullConditionalAccess_AndPass_Diagnostic()
    {
        var test = @"
using System.Collections.Generic;

class TestClass
{
    void Method(List<int> {|#0:items|})
    {
        items?.Add(42);
        Save(items);
    }

    void Save(List<int> x) { }
}";

        var expected = VerifyCS.Diagnostic("COP001")
            .WithLocation(0)
            .WithArguments("items");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task IndexerAccess_AndPass_Diagnostic()
    {
        var test = @"
using System.Collections.Generic;

class TestClass
{
    void Method(List<int> {|#0:items|})
    {
        var first = items[0];
        Save(items);
    }

    void Save(List<int> x) { }
}";

        var expected = VerifyCS.Diagnostic("COP001")
            .WithLocation(0)
            .WithArguments("items");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NameofUsage_NoDiagnostic()
    {
        var test = @"
using System.Collections.Generic;

class TestClass
{
    void Method(List<int> items)
    {
        var name = nameof(items);  // Not a real usage
        Save(items);               // Only pass
    }

    void Save(List<int> x) { }
}";
        // nameof() zählt nicht als Member-Zugriff
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task OnlyIndexerAccess_NoDiagnostic()
    {
        var test = @"
using System.Collections.Generic;

class TestClass
{
    void Method(List<int> items)
    {
        var first = items[0];
        items[1] = 42;
    }
}";
        // Nur Member-Zugriff (Indexer), kein Pass → OK
        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}