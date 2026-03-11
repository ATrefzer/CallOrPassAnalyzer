using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CallOrPassAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PassEnumParameterAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "COP002";

    private const string Category = "Design";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Raw enum value passed as argument",
        "Raw enum value '{0}' is passed as argument, but a parameter of that enum type is already available - pass the parameter instead",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        "When a method receives an enum parameter, raw enum values of the same type should not be passed to " +
        "other methods. Pass the parameter instead. Raw enum values may still be used in comparisons.");

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

        // Collect enum types from parameters
        var enumParamTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var param in parameters)
        {
            var paramSymbol = semanticModel.GetDeclaredSymbol(param, cancellationToken);
            if (paramSymbol?.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
                enumParamTypes.Add(enumType);
        }

        if (enumParamTypes.Count == 0) return;

        // Scan for raw enum values passed directly as arguments
        foreach (var memberAccess in body.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Must be directly passed as an argument — comparisons (==, switch/case) are not ArgumentSyntax
            if (memberAccess.Parent is not ArgumentSyntax) continue;

            // Skip nameof(MyEnum.Value)
            if (AnalyzerHelpers.IsInsideNameof(memberAccess)) continue;

            // Resolve the symbol — must be an enum field
            var symbol = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;
            if (symbol is not IFieldSymbol field || field.ContainingType?.TypeKind != TypeKind.Enum)
                continue;

            // Must match the enum type of one of the method's parameters
            if (!enumParamTypes.Contains(field.ContainingType)) continue;

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, memberAccess.GetLocation(), memberAccess.ToString()));
        }
    }
}
