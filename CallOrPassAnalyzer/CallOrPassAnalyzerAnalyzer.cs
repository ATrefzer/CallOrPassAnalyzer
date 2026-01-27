using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace CallOrPassAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CallOrPassAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "COP001";

        private static readonly LocalizableString Title =
            "Parameter is both called and passed";

        private static readonly LocalizableString MessageFormat =
            "Parameter '{0}' has both member access and is passed as argument - consider separating these concerns";

        private static readonly LocalizableString Description =
            "A method should either call methods on a parameter or pass it to other methods, not both. " +
            "This is known as the 'Either call or pass' rule from 'Five Lines of Code'.";

        private const string Category = "Design";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

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
                return;

            // Get the method body (either block body or expression body)
            SyntaxNode body = (SyntaxNode)methodDeclaration.Body
                              ?? methodDeclaration.ExpressionBody;

            // Check each parameter
            foreach (var parameter in methodDeclaration.ParameterList.Parameters)
            {
                AnalyzeParameter(context, parameter, body);
            }
        }

        private static void AnalyzeParameter(
            SyntaxNodeAnalysisContext context,
            ParameterSyntax parameter,
            SyntaxNode body)
        {
            var parameterName = parameter.Identifier.Text;

            bool hasMemberAccess = false;
            bool isPassedAsArgument = false;

            // Find all usages of this parameter name in the method body
            var identifiers = body.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(id => id.Identifier.Text == parameterName);

            foreach (var identifier in identifiers)
            {
                // Check: Is this a member access? (e.g., "items.Add", "items.Count")
                if (IsMemberAccess(identifier))
                {
                    hasMemberAccess = true;
                }

                // Check: Is this passed as an argument? (e.g., "SaveItems(items)")
                if (IsPassedAsArgument(identifier))
                {
                    isPassedAsArgument = true;
                }

                // Early exit if violation found
                if (hasMemberAccess && isPassedAsArgument)
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        parameter.Identifier.GetLocation(),  // ← Location only of the name
                        parameterName);

                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }
        }

        private static bool IsMemberAccess(IdentifierNameSyntax identifier)
        {
            // Check if parent is MemberAccessExpression and identifier is the left side
            // e.g., in "items.Add()", identifier "items" is Expression (left side)
            if (identifier.Parent is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Expression == identifier;
            }

            return false;
        }

        private static bool IsPassedAsArgument(IdentifierNameSyntax identifier)
        {
            // Check if the identifier is directly inside an Argument
            // e.g., in "SaveItems(items)", identifier "items" is inside ArgumentSyntax
            return identifier.Parent is ArgumentSyntax;
        }
    }
}