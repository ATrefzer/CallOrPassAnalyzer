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

            // Skip methods without body (abstract, interface, extern)
            if (methodDeclaration.Body == null && methodDeclaration.ExpressionBody == null)
            {
                return;
            }

            // Get the method body (either block body or expression body)
            var body = (SyntaxNode)methodDeclaration.Body
                       ?? methodDeclaration.ExpressionBody;

            var semanticModel = context.SemanticModel;

            foreach (var parameter in methodDeclaration.ParameterList.Parameters)
            {
                AnalyzeParameter(context, parameter, body, semanticModel);
            }
        }

        private static void AnalyzeParameter(
            SyntaxNodeAnalysisContext context,
            ParameterSyntax parameter,
            SyntaxNode body,
            SemanticModel semanticModel)
        {
            // Get the symbol of the parameter
            var parameterSymbol = semanticModel.GetDeclaredSymbol(parameter);
            if (parameterSymbol == null)
            {
                return;
            }

            var hasMemberAccess = false;
            var isPassedAsArgument = false;

            // Find all usages of this identifier in body
            var identifiers = body.DescendantNodes()
                .OfType<IdentifierNameSyntax>();

            foreach (var identifier in identifiers)
            {
                // Prüfen: Verweist dieser Identifier auf unseren Parameter?
                var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;

                if (!SymbolEqualityComparer.Default.Equals(symbol, parameterSymbol))
                {
                    continue;
                }

                // Special case: ignore nameof(parameter)
                if (IsInsideNameof(identifier))
                {
                    continue;
                }

                if (IsMemberAccess(identifier))
                {
                    hasMemberAccess = true;
                }

                // Check: Is this passed as an argument? (e.g., "SaveItems(items)")
                if (IsPassedAsArgument(identifier))
                {
                    isPassedAsArgument = true;
                }

                if (hasMemberAccess && isPassedAsArgument)
                {
                    // Violation found
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        parameter.Identifier.GetLocation(),
                        parameterSymbol.Name);

                    context.ReportDiagnostic(diagnostic);
                    return;
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

            // Indexer-Zugriff: items[0]
            if (identifier.Parent is ElementAccessExpressionSyntax elementAccess)
            {
                return elementAccess.Expression == identifier;
            }

            return false;
        }

        private static bool IsPassedAsArgument(IdentifierNameSyntax identifier)
        {
            // i.e. Save(items)
            if (identifier.Parent is ArgumentSyntax)
            {
                return true;
            }

            return false;
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