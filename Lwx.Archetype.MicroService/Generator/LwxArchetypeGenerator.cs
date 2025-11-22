using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Lwx.Archetype.MicroService.Generator.Processors;

namespace Lwx.Archetype.MicroService.Generator;

internal sealed class FoundAttribute(string attributeName, ISymbol targetSymbol, Location location, AttributeData attributeData)
{
  public string AttributeName { get; } = attributeName;
  public ISymbol TargetSymbol { get; } = targetSymbol;
  public Location Location { get; } = location;
  public AttributeData AttributeData { get; } = attributeData;
}

[Generator(LanguageNames.CSharp)]
public class LwxArchetypeGenerator : IIncrementalGenerator
{

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Generate attribute definitions so consumer projects don't need to declare them.
        context.RegisterPostInitializationOutput(ctx =>
        {
            // Use each processor's GenerateAttribute method
            new LwxEndpointPostInitializationProcessor(ctx).Execute();
            new LwxDtoPostInitializationProcessor(ctx).Execute();
            new LwxWorkerPostInitializationProcessor(ctx).Execute();
            new LwxServiceBusConsumerPostInitializationProcessor(ctx).Execute();
            new LwxEventHubConsumerPostInitializationProcessor(ctx).Execute();
            new LwxTimerPostInitializationProcessor(ctx).Execute();
            new LwxServiceBusProducerPostInitializationProcessor(ctx).Execute();
            new LwxEndpointMetadataPostInitializationProcessor(ctx).Execute();
            new LwxEndpointExtensionsPostInitializationProcessor(ctx).Execute();
            new LwxSwaggerPostInitializationProcessor(ctx).Execute();
        });

        // Find attributes whose simple name matches one of the attribute names in our list
        var attrProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, ct) => IsPotentialAttribute(node),
                transform: static (ctx, ct) => Transform(ctx))
            .Where(x => x is not null);

        // Collect all found attributes and process them together
        context.RegisterSourceOutput(context.CompilationProvider.Combine(attrProvider.Collect()), (spc, tuple) =>
        {
            var compilation = tuple.Left;
            var found = tuple.Right;
            var hasSwagger = false;
            foreach (var f in found)
            {
                if (f.AttributeName == LwxConstants.LwxEndpoint)
                {
                    new LwxEndpointTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxDto)
                {
                    new LwxDtoTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxWorker)
                {
                    new LwxWorkerTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxServiceBusConsumer)
                {
                    new LwxServiceBusConsumerTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxEventHubConsumer)
                {
                    new LwxEventHubConsumerTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxTimer)
                {
                    new LwxTimerTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxServiceBusProducer)
                {
                    new LwxServiceBusProducerTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxSwagger)
                {
                    hasSwagger = true;
                    new LwxSwaggerTypeProcessor(f, spc, compilation).Execute();
                }
            }

            var swaggerAttr = found.FirstOrDefault(f => f.AttributeName == LwxConstants.LwxSwagger)?.AttributeData;
            var fullNs = found.FirstOrDefault()?.TargetSymbol?.ContainingNamespace?.ToDisplayString() ?? "Generated";
            var ns = fullNs.Contains('.') ? fullNs.Substring(0, fullNs.LastIndexOf('.')) : fullNs;
            GenerateLwxConfigure(spc, swaggerAttr, ns);
        });
    }

    private static void GenerateLwxConfigure(SourceProductionContext spc, AttributeData? swaggerAttr, string ns)
    {
        string title = "API";
        string description = "API Description";
        string version = "v1";
        string publishLiteral = "Lwx.Archetype.MicroService.Atributes.LwxStage.None";

        if (swaggerAttr != null)
        {
            var m1 = swaggerAttr.NamedArguments.FirstOrDefault(kv => kv.Key == "Title");
            if (!m1.Equals(default(KeyValuePair<string, TypedConstant>)) && m1.Value.Value is string s1)
            {
                title = s1;
            }
            var m2 = swaggerAttr.NamedArguments.FirstOrDefault(kv => kv.Key == "Description");
            if (!m2.Equals(default(KeyValuePair<string, TypedConstant>)) && m2.Value.Value is string s2)
            {
                description = s2;
            }
            var m3 = swaggerAttr.NamedArguments.FirstOrDefault(kv => kv.Key == "Version");
            if (!m3.Equals(default(KeyValuePair<string, TypedConstant>)) && m3.Value.Value is string s3)
            {
                version = s3;
            }

            var m4 = swaggerAttr.NamedArguments.FirstOrDefault(kv => kv.Key == "Publish");
            if (!m4.Equals(default(KeyValuePair<string, TypedConstant>)) && m4.Value.Value != null)
            {
                var raw = m4.Value.Value;
                if (raw is int iv)
                {
                    publishLiteral = iv switch
                    {
                        1 => "Lwx.Archetype.MicroService.Atributes.LwxStage.Development",
                        2 => "Lwx.Archetype.MicroService.Atributes.LwxStage.Production",
                        _ => "Lwx.Archetype.MicroService.Atributes.LwxStage.None"
                    };
                }
                else
                {
                    var tmp = m4.Value.Value.ToString() ?? "Lwx.Archetype.MicroService.Atributes.LwxStage.None";
                    publishLiteral = tmp.Contains('.') ? tmp : ("Lwx.Archetype.MicroService.Atributes.LwxStage." + tmp);
                }
            }
        }

        var hasSwagger = swaggerAttr != null;
        var swaggerServicesCode = hasSwagger ? $$"""
            var __lwxPublish = {{publishLiteral}};
            if (__lwxPublish != Lwx.Archetype.MicroService.Atributes.LwxStage.None)
            {
                var shouldAdd = builder.Environment.IsDevelopment() ?
                    (__lwxPublish == Lwx.Archetype.MicroService.Atributes.LwxStage.Development || __lwxPublish == Lwx.Archetype.MicroService.Atributes.LwxStage.Production) :
                    (__lwxPublish == Lwx.Archetype.MicroService.Atributes.LwxStage.Production);

                if (shouldAdd)
                {
                    builder.Services.AddSwaggerGen(options =>
                    {
                        options.SwaggerDoc("{{version}}", new Microsoft.OpenApi.Models.OpenApiInfo
                        {
                            Title = "{{title}}",
                            Description = "{{description}}",
                            Version = "{{version}}"
                        });
                    });
                }
            }
        """ : "";

        var swaggerAppCode = hasSwagger ? $$"""
            var __lwxPublish = {{publishLiteral}};
            if (__lwxPublish != Lwx.Archetype.MicroService.Atributes.LwxStage.None)
            {
                var shouldAdd = app.Environment.IsDevelopment() ?
                    (__lwxPublish == Lwx.Archetype.MicroService.Atributes.LwxStage.Development || __lwxPublish == Lwx.Archetype.MicroService.Atributes.LwxStage.Production) :
                    (__lwxPublish == Lwx.Archetype.MicroService.Atributes.LwxStage.Production);

                if (shouldAdd)
                {
                    app.UseSwagger();
                    app.UseSwaggerUI(options =>
                    {
                        options.DocumentTitle = "{{title}}";
                    });
                }
            }
        """ : "";

        var source = $$"""
            // <auto-generated/>
            using System;
            using System.Linq;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Routing;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;
            using Lwx.Archetype.MicroService.Atributes;

            namespace Lwx.Archetype.MicroService.Atributes
            {
                /// <summary>
                /// Provides extension methods for configuring Lwx in ASP.NET Core applications.
                /// </summary>
                public static class LwxEndpointExtensions
                {
                    /// <summary>
                    /// Configures Lwx services, including Swagger if enabled.
                    /// </summary>
                    /// <param name="builder">The web application builder.</param>
                    public static void LwxConfigure(this WebApplicationBuilder builder)
                    {
                        {{swaggerServicesCode}}
                    }

                    /// <summary>
                    /// Configures Lwx app and registers validation to ensure all registered endpoints are mapped through the Lwx mechanism.
                    /// </summary>
                    /// <param name="app">The web application instance.</param>
                    public static void LwxConfigure(this WebApplication app)
                    {
                        {{swaggerAppCode}}

                        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
                        lifetime.ApplicationStarted.Register(() =>
                        {
                            ValidateLwxEndpoints(app);
                        });
                    }

                    private static void ValidateLwxEndpoints(WebApplication app)
                    {
                        var exceptions = new[] { "health", "ready", "swagger" };
                        var endpointDataSources = app.Services.GetServices<EndpointDataSource>();
                        foreach (var dataSource in endpointDataSources)
                        {
                            foreach (var endpoint in dataSource.Endpoints)
                            {
                                if (endpoint.Metadata.GetMetadata<LwxEndpointMetadata>() == null)
                                {
                                    var name = endpoint.DisplayName ?? endpoint.ToString();
                                    if (!exceptions.Any(e => name.Contains(e, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        throw new InvalidOperationException($"Endpoint {name} is not mapped by Lwx mechanism");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            """;

        spc.AddSource("LwxEndpointExtensions.g.cs", SourceText.From(source, Encoding.UTF8));
    }    private static FoundAttribute Transform(GeneratorSyntaxContext ctx)
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
}
