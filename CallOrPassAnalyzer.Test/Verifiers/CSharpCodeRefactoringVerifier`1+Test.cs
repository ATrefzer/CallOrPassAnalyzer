using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace CallOrPassAnalyzer.Test.Verifiers;

public static partial class CSharpCodeRefactoringVerifier<TCodeRefactoring>
    where TCodeRefactoring : CodeRefactoringProvider, new()
{
    public class Test : CSharpCodeRefactoringTest<TCodeRefactoring, DefaultVerifier>
    {
        public Test()
        {
            // Multi-Targeting: .NET Standard 2.0 ist kompatibel mit .NET Framework 4.7.2 und .NET 10
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20;

            SolutionTransforms.Add((solution, projectId) =>
            {
                var compilationOptions = solution.GetProject(projectId).CompilationOptions;
                compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                    compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));
                solution = solution.WithProjectCompilationOptions(projectId, compilationOptions);

                return solution;
            });
        }
    }
}