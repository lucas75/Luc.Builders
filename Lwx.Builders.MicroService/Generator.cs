using System;
#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Lwx.Builders.MicroService.Processors;

namespace Lwx.Builders.MicroService;

// FoundAttribute and LwxConstants moved into the Processors namespace so
// processors and helper classes can reference them from `Lwx.Builders.MicroService.Processors`.

[Generator(LanguageNames.CSharp)]
public class Generator : IIncrementalGenerator
{

    internal readonly List<string> EndpointNames = new();
    internal readonly List<string> WorkerNames = new();
    internal Location ServiceConfigLocation = Location.None;
    internal INamedTypeSymbol? ServiceConfigSymbol = null;
    internal bool GenerateMainFlag = false;
    internal AttributeData? LwxServiceConfigAttributeData = null;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Generate static files
        context.RegisterPostInitializationOutput(ctx =>
        {
            new Processors.LwxEndpointPostInitializationProcessor(ctx).Execute();
            new Processors.LwxWorkerPostInitializationProcessor(ctx).Execute();
            new Processors.LwxServiceBusConsumerPostInitializationProcessor(ctx).Execute();
            new Processors.LwxEventHubConsumerPostInitializationProcessor(ctx).Execute();
            new Processors.LwxTimerPostInitializationProcessor(ctx).Execute();
            new Processors.LwxServiceBusProducerPostInitializationProcessor(ctx).Execute();
            new Processors.LwxEndpointMetadataPostInitializationProcessor(ctx).Execute();
            new Processors.LwxEndpointExtensionsPostInitializationProcessor(ctx).Execute();
            new Processors.LwxServiceConfigPostInitializationProcessor(ctx).Execute();
        });
        
        var attrProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, ct) => IsPotentialAttribute(node),
                transform: static (ctx, ct) => Transform(ctx))
            .Where(x => x is not null);
       
        var allAttributes = context.CompilationProvider.Combine(attrProvider.Collect());

        // First pass: process all attributes except ServiceConfig
        context.RegisterSourceOutput(
            allAttributes, 
            (spc, tuple) =>
            {
                var (compilation, attrs) = tuple;            

                // Reset lists for this compilation run
                EndpointNames.Clear();
                WorkerNames.Clear();

                foreach (var attr in attrs)
                {
                    if (attr == null) continue;
                    if (attr.AttributeName == LwxConstants.LwxServiceConfig) continue;
                    var rp = new RootProcessor(this, attr, spc, compilation);
                    rp.Execute();
                }
            }
        );

        // Second pass: process ServiceConfig attributes
        context.RegisterSourceOutput(
            allAttributes, 
            (spc, tuple) =>
            {
                var (compilation, attrs) = tuple; 

                ServiceConfigLocation = Location.None;
                ServiceConfigSymbol = null;
                GenerateMainFlag = false;
                LwxServiceConfigAttributeData = null;

                foreach (var attr in attrs)
                {
                    if (attr == null) continue;
                    if (attr.AttributeName != LwxConstants.LwxServiceConfig) continue;
                    var rp = new RootProcessor(this, attr, spc, compilation);
                    rp.Execute();
                }

                // After processing service config attributes, ensure at least one exists
                if (LwxServiceConfigAttributeData == null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "LWX011",
                            "Missing ServiceConfig",
                            "Projects using Lwx generator must declare a [LwxServiceConfig] class (ServiceConfig.cs) with service metadata.",
                            "Configuration",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        Location.None));
                }
            }
        );


        // No global validation pass needed — validation occurs in LwxServiceConfigTypeProcessor.Execute when
        // an actual [LwxServiceConfig] attribute is present on the type.
    }

    private static FoundAttribute? Transform(GeneratorSyntaxContext ctx)
    {
        var attributeSyntax = (AttributeSyntax)ctx.Node;

        // Use the semantic model to determine the attribute type correctly
        var info = ctx.SemanticModel.GetSymbolInfo(attributeSyntax, CancellationToken.None);
        var attrType = info.Symbol switch
        {
            IMethodSymbol ms => ms.ContainingType,
            INamedTypeSymbol nts => nts,
            _ => null
        };

        if (attrType == null) return default(FoundAttribute);

        var attrName = attrType.Name;
        if (attrName.EndsWith("Attribute")) attrName = attrName.Substring(0, attrName.Length - "Attribute".Length);
        if (!LwxConstants.AttributeNames.Contains(attrName, StringComparer.Ordinal)) return default(FoundAttribute);

        var parent = attributeSyntax.Parent?.Parent;
        if (parent == null) return default(FoundAttribute);

        var declaredSymbol = ctx.SemanticModel.GetDeclaredSymbol(parent, CancellationToken.None);
        if (declaredSymbol == null) return default(FoundAttribute);

        // Find the corresponding AttributeData for the declared symbol (if any)
        var attrData = declaredSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass != null && ad.AttributeClass.ToDisplayString() == attrType.ToDisplayString());

        return new FoundAttribute(attrName, declaredSymbol, parent.GetLocation(), attrData);
    }

    private static bool IsPotentialAttribute(SyntaxNode node)
    {
        if (node is not AttributeSyntax attribute) return false;
        // Name may be IdentifierName or QualifiedName (e.g. Namespace.LwxEndpoint)
        var name = attribute.Name.ToString();
        var simple = name.Contains('.') ? name.Substring(name.LastIndexOf('.') + 1) : name;
        if (simple.EndsWith("Attribute")) simple = simple.Substring(0, simple.Length - "Attribute".Length);
        return LwxConstants.AttributeNames.Contains(simple, StringComparer.Ordinal);
    }

    private static void ValidateServiceConfigConfigureMethods(INamedTypeSymbol typeSymbol, SourceProductionContext spc, Compilation compilation)
    {
        // Resolve expected parameter types
        var webAppBuilderType = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.WebApplicationBuilder");
        var webAppType = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.WebApplication");

        foreach (var member in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.IsImplicitlyDeclared) continue;
            // Only inspect ordinary methods (skip constructors, accessors, etc.)
            if (member.MethodKind != MethodKind.Ordinary) continue;

            // Only consider declared public methods
            if (member.DeclaredAccessibility == Accessibility.Public)
            {
                // Allowed public methods are:
                // public static void Configure(WebApplicationBuilder)
                // public static void Configure(WebApplication)
                if (member.Name == "Configure")
                {
                    var valid = member.IsStatic && member.Parameters.Length == 1 && member.ReturnsVoid;
                    var param = member.Parameters.Length == 1 ? member.Parameters[0].Type : null;
                    var paramMatches = param != null && (SymbolEqualityComparer.Default.Equals(param, webAppBuilderType) || SymbolEqualityComparer.Default.Equals(param, webAppType));
                    if (!valid || !paramMatches)
                    {
                        var descriptor = new DiagnosticDescriptor(
                            "LWX014",
                            "Invalid ServiceConfig.Configure signature",
                            "ServiceConfig.Configure must be declared as a public static void Configure(WebApplicationBuilder) or public static void Configure(WebApplication). Found: '{0}'",
                            "Configuration",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true);

                        var locMember = member.Locations.FirstOrDefault() ?? Location.None;
                        spc.ReportDiagnostic(Diagnostic.Create(descriptor, locMember, member.ToDisplayString()));
                    }
                }
                else
                {
                    // Any other public method is disallowed
                    var descriptor = new DiagnosticDescriptor(
                        "LWX015",
                        "Unexpected public method in ServiceConfig",
                        "Public method '{0}' is not allowed in ServiceConfig. Only public static Configure(WebApplicationBuilder) and Configure(WebApplication) are permitted for customization when using the generator.",
                        "Configuration",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true);

                    var locMember = member.Locations.FirstOrDefault() ?? Location.None;
                    spc.ReportDiagnostic(Diagnostic.Create(descriptor, locMember, member.Name));
                }
            }
        }
    }
}

