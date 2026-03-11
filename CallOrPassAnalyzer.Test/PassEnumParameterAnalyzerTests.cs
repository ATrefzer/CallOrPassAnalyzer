using Xunit;
using VerifyCS =
    CallOrPassAnalyzer.Test.Verifiers.CSharpAnalyzerVerifier<CallOrPassAnalyzer.PassEnumParameterAnalyzer>;

namespace CallOrPassAnalyzer.Test;

public class PassEnumParameterAnalyzerTests
{
    [Fact]
    public async Task EnumLiteralPassedAsArgument_Diagnostic()
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
        var expected = VerifyCS.Diagnostic("COP002")
            .WithLocation(0)
            .WithArguments("Status.Active");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task EnumLiteralUsedInComparison_NoDiagnostic()
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
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task EnumLiteralUsedInSwitchCase_NoDiagnostic()
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
        await VerifyCS.VerifyAnalyzerAsync(test);
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
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NoEnumParameter_EnumLiteralPassed_NoDiagnostic()
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
        await VerifyCS.VerifyAnalyzerAsync(test);
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
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task EnumLiteralInNameof_NoDiagnostic()
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
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultipleEnumLiteralsPassed_MultipleDiagnostics()
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
        var expected0 = VerifyCS.Diagnostic("COP002")
            .WithLocation(0)
            .WithArguments("Status.Active");
        var expected1 = VerifyCS.Diagnostic("COP002")
            .WithLocation(1)
            .WithArguments("Status.Inactive");

        await VerifyCS.VerifyAnalyzerAsync(test, expected0, expected1);
    }
}
