using Xunit;
using VerifyCS =
    CallOrPassAnalyzer.Test.Verifiers.CSharpAnalyzerVerifier<CallOrPassAnalyzer.CallOrPassAnalyzerAnalyzer>;
using VerifyEnumCS =
    CallOrPassAnalyzer.Test.Verifiers.CSharpAnalyzerVerifier<CallOrPassAnalyzer.RawEnumPassAnalyzer>;

namespace CallOrPassAnalyzer.Test;

public class CallOrPassAnalyzerUnitTest
{
    // ── COP001: Call or pass ───────────────────────────────────────────────

    [Fact]
    public async Task EmptyCode_NoDiagnostic()
    {
        await VerifyCS.VerifyAnalyzerAsync(@"");
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
        // Local variables are fine. Only arguments are checked.
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
        // nameof() does not count as member access
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
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Identifier_Wrapped_In_Brackets_Or_Cast_Diagnostic()
    {
        var test = @"
using System;
class TestClass : IDisposable
{
    public void Dispose() {}
    public void TestMethod(TestClass {|#0:param|})
    {
        SomeOtherMethod(param); // Pass
        ((IDisposable)param).Dispose();
    }

    private void SomeOtherMethod(TestClass myClass)
    {
    }
};";
        var expected = VerifyCS.Diagnostic("COP001")
            .WithLocation(0)
            .WithArguments("param");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    // ── COP002: Raw enum value passed as argument ──────────────────────────

    [Fact]
    public async Task RawEnumPassedAsArgument_Diagnostic()
    {
        var test = @"
enum Status { Active, Inactive }

class TestClass
{
    void Method(Status status)
    {
        Process({|#0:Status.Active|});
    }

    void Process(Status s) { }
}";
        var expected = VerifyEnumCS.Diagnostic("COP002")
            .WithLocation(0)
            .WithArguments("Status.Active");

        await VerifyEnumCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RawEnumUsedInComparison_NoDiagnostic()
    {
        var test = @"
enum Status { Active, Inactive }

class TestClass
{
    void Method(Status status)
    {
        if (status == Status.Active) { }
        if (status != Status.Inactive) { }
    }
}";
        await VerifyEnumCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RawEnumUsedInSwitchCase_NoDiagnostic()
    {
        var test = @"
enum Status { Active, Inactive }

class TestClass
{
    void Method(Status status)
    {
        switch (status)
        {
            case Status.Active: break;
            case Status.Inactive: break;
        }
    }
}";
        await VerifyEnumCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ParameterItselfPassed_NoDiagnostic()
    {
        var test = @"
enum Status { Active, Inactive }

class TestClass
{
    void Method(Status status)
    {
        Process(status);
    }

    void Process(Status s) { }
}";
        await VerifyEnumCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NoEnumParameter_RawEnumPassed_NoDiagnostic()
    {
        var test = @"
enum Status { Active, Inactive }

class TestClass
{
    void Method(int count)
    {
        Process(Status.Active);
    }

    void Process(Status s) { }
}";
        await VerifyEnumCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task DifferentEnumType_NoDiagnostic()
    {
        var test = @"
enum Status { Active, Inactive }
enum Priority { Low, High }

class TestClass
{
    void Method(Status status)
    {
        Process(Priority.High);
    }

    void Process(Priority p) { }
}";
        await VerifyEnumCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RawEnumInNameof_NoDiagnostic()
    {
        var test = @"
enum Status { Active, Inactive }

class TestClass
{
    void Method(Status status)
    {
        Log(nameof(Status.Active));
    }

    void Log(string s) { }
}";
        await VerifyEnumCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultipleRawEnumsPassed_MultipleDiagnostics()
    {
        var test = @"
enum Status { Active, Inactive }

class TestClass
{
    void Method(Status status)
    {
        Process({|#0:Status.Active|});
        Process({|#1:Status.Inactive|});
    }

    void Process(Status s) { }
}";
        var expected0 = VerifyEnumCS.Diagnostic("COP002")
            .WithLocation(0)
            .WithArguments("Status.Active");
        var expected1 = VerifyEnumCS.Diagnostic("COP002")
            .WithLocation(1)
            .WithArguments("Status.Inactive");

        await VerifyEnumCS.VerifyAnalyzerAsync(test, expected0, expected1);
    }
}
