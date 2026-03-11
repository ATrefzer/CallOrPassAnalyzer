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
        "Enum value passed as argument",
        "Enum value '{0}' is passed as argument, but a enum literal is passed to another called method",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "When a method receives an enum parameter, literal values of the same type should not be passed to " +
        "other methods. Pass the parameter instead. The enum values may still be used in comparisons.");

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

        // Scan for enum values passed directly as arguments
        foreach (var memberAccess in body.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Must be directly passed as an argument — comparisons (==, switch/case) are not ArgumentSyntax
            if (memberAccess.Parent is not ArgumentSyntax) continue;

            // Skip nameof(MyEnum.Value)
            if (AnalyzerHelpers.IsInsideNameof(memberAccess)) continue;

            // Resolve the symbol — must be an enum field

            /*
            A MemberAccessExpressionSyntax syntactically represents an expression of the form X.Y.
            The symbol is the semantic resolution of this expression — i.e., what .Y actually points to in the type system.
            semanticModel.GetSymbolInfo(memberAccess).Symbol returns an ISymbol that can vary depending on the context:

            Expression           Symbol type
            -----------------------------------------
            MyEnum.Value         IFieldSymbol
            obj.MyProperty       IPropertySymbol
            obj.MyMethod         IMethodSymbol
            MyClass.StaticField  IFieldSymbol

               GetSymbolInfo resolves .Value and returns an IFieldSymbol, because enum members are modeled as fields in Roslyn.
            */
            var symbol = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;

            // Symbol resolves to an enum literal
            if (symbol is not IFieldSymbol field || field.ContainingType?.TypeKind != TypeKind.Enum)
                continue;

            // Must match the enum type of one of the method's parameters
            if (!enumParamTypes.Contains(field.ContainingType)) continue;

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, memberAccess.GetLocation(), memberAccess.ToString()));
        }
    }
}