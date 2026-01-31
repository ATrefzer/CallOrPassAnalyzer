using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CallOrPassAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CallOrPassAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "COP001";

        private const string Category = "Design";

        private static readonly LocalizableString Title =
            "Parameter is both called and passed";

        private static readonly LocalizableString MessageFormat =
            "Parameter '{0}' has both member access and is passed as argument - consider separating these concerns";

        private static readonly LocalizableString Description =
            "A method should either call methods on a parameter or pass it to other methods, not both. " +
            "This is known as the 'Either call or pass' rule from 'Five Lines of Code'.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            true,
            Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            // Ignore generated code (designer files, etc.)
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // Allow parallel execution for better performance
            context.EnableConcurrentExecution();

            // Register for method declarations
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Skip methods without parameters — nothing to analyze
            var parameters = methodDeclaration.ParameterList.Parameters;
            if (parameters.Count == 0)
            {
                return;
            }

            // Skip methods without body (abstract, interface, extern)
            if (methodDeclaration.Body == null && methodDeclaration.ExpressionBody == null)
            {
                return;
            }

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

            if (parameterMap.Count == 0)
            {
                return;
            }

            // Track findings per parameter symbol
            var hasMemberAccess = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var isPassedAsArg = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            // Single pass over all identifiers in the body
            foreach (var identifier in body.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 1. Fast text pre-filter — pure string compare, no semantic work
                if (!parameterNames.Contains(identifier.Identifier.Text))
                {
                    continue;
                }

                // 2. Cheap syntax checks — skip identifiers used in non-interesting ways
                //    (assignments, returns, arithmetic, etc.)
                var isMember = IsMemberAccess(identifier);
                var isArg = IsPassedAsArgument(identifier);
                if (!isMember && !isArg)
                {
                    continue;
                }

                // 3. Skip nameof(parameter) expressions
                if (IsInsideNameof(identifier))
                {
                    continue;
                }

                // 4. Expensive semantic check — only reached for identifiers that:
                //    - match a parameter name AND are a member access or argument
                var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
                if (symbol == null || !parameterMap.TryGetValue(symbol, out var paramSyntax))
                {
                    continue;
                }

                // 5. Track and check for violation
                if (isMember)
                {
                    hasMemberAccess.Add(symbol);
                }

                if (isArg)
                {
                    isPassedAsArg.Add(symbol);
                }

                if (hasMemberAccess.Contains(symbol) && isPassedAsArg.Contains(symbol))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, paramSyntax.Identifier.GetLocation(), symbol.Name));

                    // Stop tracking this parameter — already reported
                    parameterMap.Remove(symbol);

                    // All parameters reported — done with this method
                    if (parameterMap.Count == 0)
                    {
                        return;
                    }
                }
            }
        }

        private static bool IsMemberAccess(IdentifierNameSyntax identifier)
        {
            // Direct member access: items.Add(), items.Count
            if (identifier.Parent is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Expression == identifier;
            }

            // Null-conditional: items?.Add(), items?.Count
            if (identifier.Parent is ConditionalAccessExpressionSyntax conditionalAccess)
            {
                return conditionalAccess.Expression == identifier;
            }

            // Indexer access: items[0]
            if (identifier.Parent is ElementAccessExpressionSyntax elementAccess)
            {
                return elementAccess.Expression == identifier;
            }

            return false;
        }

        private static bool IsPassedAsArgument(IdentifierNameSyntax identifier)
        {
            return identifier.Parent is ArgumentSyntax;
        }

        private static bool IsInsideNameof(IdentifierNameSyntax identifier)
        {
            // Check if the identifier is inside a nameof(...) expression
            var current = identifier.Parent;
            while (current != null)
            {
                if (current is InvocationExpressionSyntax invocation)
                {
                    if (invocation.Expression is IdentifierNameSyntax name &&
                        name.Identifier.Text == "nameof")
                    {
                        return true;
                    }
                }

                current = current.Parent;
            }

            return false;
        }
    }
}