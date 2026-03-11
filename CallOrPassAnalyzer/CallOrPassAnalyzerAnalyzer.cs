using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CallOrPassAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CallOrPassAnalyzerAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "COP001";

    private const string Category = "Design";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Parameter is both called and passed",
        "Parameter '{0}' has both member access and is passed as argument - consider separating these concerns",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        "A method should either call methods on a parameter or pass it to other methods, not both. " +
        "This is known as the 'Either call or pass' rule from 'Five Lines of Code'.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        var parameters = methodDeclaration.ParameterList.Parameters;
        if (parameters.Count == 0) return;

        if (methodDeclaration.Body == null && methodDeclaration.ExpressionBody == null) return;

        var body = (SyntaxNode)methodDeclaration.Body
                   ?? methodDeclaration.ExpressionBody;

        var semanticModel = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        // Resolve parameter symbols upfront (one GetDeclaredSymbol per parameter)
        var parameterNames = new HashSet<string>();
        var parameterMap = new Dictionary<ISymbol, ParameterSyntax>(SymbolEqualityComparer.Default);

        foreach (var param in parameters)
        {
            var symbol = semanticModel.GetDeclaredSymbol(param, cancellationToken);
            if (symbol != null)
            {
                parameterNames.Add(param.Identifier.Text);
                parameterMap[symbol] = param;
            }
        }

        if (parameterMap.Count == 0) return;

        var hasMemberAccess = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var isPassedAsArg = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        // Single pass over all identifiers in the body
        foreach (var identifier in body.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Fast text pre-filter — pure string compare, no semantic work
            if (!parameterNames.Contains(identifier.Identifier.Text)) continue;

            // 2. Cheap syntax checks
            var isMember = IsMemberAccess(identifier);
            var isArg = identifier.Parent is ArgumentSyntax;
            if (!isMember && !isArg) continue;

            // 3. Skip nameof(parameter) expressions
            if (AnalyzerHelpers.IsInsideNameof(identifier)) continue;

            // 4. Expensive semantic check
            var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (symbol == null || !parameterMap.TryGetValue(symbol, out var paramSyntax)) continue;

            if (isMember) hasMemberAccess.Add(symbol);
            if (isArg) isPassedAsArg.Add(symbol);

            if (hasMemberAccess.Contains(symbol) && isPassedAsArg.Contains(symbol))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, paramSyntax.Identifier.GetLocation(), symbol.Name));

                parameterMap.Remove(symbol);
                if (parameterMap.Count == 0) return;
            }
        }
    }

    private static bool IsMemberAccess(IdentifierNameSyntax identifier)
    {
        // Walk upwards past parentheses and casts: ((IDisposable)param).Dispose()
        SyntaxNode current = identifier;
        while (current.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
            current = current.Parent;

        return current.Parent switch
        {
            MemberAccessExpressionSyntax m      => m.Expression == current,
            ConditionalAccessExpressionSyntax c => c.Expression == current,
            ElementAccessExpressionSyntax e     => e.Expression == current,
            _                                   => false
        };
    }
}
