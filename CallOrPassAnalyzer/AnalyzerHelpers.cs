using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CallOrPassAnalyzer;

internal static class AnalyzerHelpers
{
    internal static bool IsInsideNameof(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is InvocationExpressionSyntax invocation)
                if (invocation.Expression is IdentifierNameSyntax name &&
                    name.Identifier.Text == "nameof")
                    return true;

            current = current.Parent;
        }

        return false;
    }
}
