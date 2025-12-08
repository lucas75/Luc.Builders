using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Lwx.Builders.MicroService;

/// <summary>
/// Holds registration data for endpoints and workers belonging to a specific service namespace.
/// </summary>
internal sealed class ServiceRegistration
{
    public string ServiceNamespacePrefix { get; init; } = string.Empty;
    public List<string> EndpointNames { get; } = new();
    public List<string> WorkerNames { get; } = new();
    public List<string> MessageEndpointNames { get; } = new();
    public List<string> TimerNames { get; } = new();
    public List<(string TypeName, string HttpMethod, string? Path)> EndpointInfos { get; } = new();
    public List<(string TypeName, int Threads)> WorkerInfos { get; } = new();
    public List<(string TypeName, int QueueReaders, string QueueConfigSection, string HttpUri)> MessageEndpointInfos { get; } = new();
    public List<(string TypeName, string CronExpression)> TimerInfos { get; } = new();
    public List<Processors.SettingInfo> Settings { get; } = new();
}

[Generator(LanguageNames.CSharp)]
public class Generator : IIncrementalGenerator
{
    /// <summary>
    /// Dictionary of service registrations keyed by the service namespace prefix.
    /// For example, "ExampleOrg.Product.Abc" for a service at ExampleOrg.Product.Abc.Service.
    /// </summary>
    internal readonly Dictionary<string, ServiceRegistration> ServiceRegistrations = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or creates a ServiceRegistration for the given namespace prefix.
    /// </summary>
    internal ServiceRegistration GetOrCreateRegistration(string serviceNamespacePrefix)
    {
        if (!ServiceRegistrations.TryGetValue(serviceNamespacePrefix, out var reg))
        {
            reg = new ServiceRegistration { ServiceNamespacePrefix = serviceNamespacePrefix };
            ServiceRegistrations[serviceNamespacePrefix] = reg;
        }
        return reg;
    }

    /// <summary>
    /// Computes the service namespace prefix from an endpoint or worker namespace.
    /// For "Assembly.Abc.Endpoints.Sub" returns "Assembly.Abc".
    /// For "Assembly.Abc.Workers.Sub" returns "Assembly.Abc".
    /// </summary>
    internal static string ComputeServicePrefix(string ns)
    {
        // Find .Endpoints or .Workers segment and return everything before it
        var endpointsIdx = ns.IndexOf(".Endpoints", StringComparison.Ordinal);
        if (endpointsIdx > 0)
        {
            return ns.Substring(0, endpointsIdx);
        }

        var workersIdx = ns.IndexOf(".Workers", StringComparison.Ordinal);
        if (workersIdx > 0)
        {
            return ns.Substring(0, workersIdx);
        }

        // Fallback: return the namespace as-is (shouldn't happen for valid endpoints/workers)
        return ns;
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pass 0: Generate static files
        context.RegisterPostInitializationOutput(ctx =>
        {
            new Processors.LwxEndpointPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxWorkerPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxTimerPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxEndpointMetadataPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxEndpointExtensionsPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxServicePostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxSettingPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxMessageEndpointPostInitializationProcessor(this, ctx).Execute();
        });

        var attrProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, ct) => GeneratorUtils.IsPotentialAttribute(node),
                transform: static (ctx, ct) => GeneratorUtils.ResolveAttributeInstance(ctx))
            .Where(x => x is not null)
            .Select(static (x, ct) => x!);

        // First pass: process all attributes except Service

        var lsPass001Attrs = attrProvider.Where(x => x.AttributeName != Processors.LwxConstants.LwxService).Collect();

        context.RegisterSourceOutput
        (
            context.CompilationProvider.Combine(lsPass001Attrs),
            (spc, tuple) =>
            {
                var (compilation, attrs) = tuple;

                foreach (var attr in attrs)
                {
                    if (attr == null) continue;
                    var rp = new Processors.RootProcessor(this, attr, spc, compilation);
                    rp.Execute();
                }
            }
        );

        // Second pass: process Service attributes

        // Validate classes under Endpoints and Workers namespaces are properly annotated.
        context.RegisterSourceOutput(context.CompilationProvider, (spc, compilation) =>
        {
            foreach (var st in compilation.SyntaxTrees)
            {
                var root = st.GetRoot();
                var model = compilation.GetSemanticModel(st);
                var classDecls = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>();
                foreach (var cd in classDecls)
                {
                    var sym = model.GetDeclaredSymbol(cd);
                    if (sym == null) continue;

                    // Only inspect source symbols
                    if (!sym.Locations.Any(l => l.IsInSource)) continue;

                    // Skip nested types and private types — rules only apply to top-level public/internal classes
                    if (sym.ContainingType != null) continue;
                    if (sym.DeclaredAccessibility == Accessibility.Private) continue;

                    var ns = sym.ContainingNamespace?.ToDisplayString() ?? string.Empty;

                    if (ns.Contains(".Endpoints", StringComparison.Ordinal))
                    {
                        // Must have LwxEndpoint or LwxMessageEndpoint attribute
                        var hasEndpoint = sym.GetAttributes().Any(a => a.AttributeClass?.Name == "LwxEndpointAttribute");
                        var hasMessageEndpoint = sym.GetAttributes().Any(a => a.AttributeClass?.Name == "LwxMessageEndpointAttribute");
                        var hasTimer = sym.GetAttributes().Any(a => a.AttributeClass?.Name == "LwxTimerAttribute");
                        
                        // Allow helper types that implement queue-related interfaces
                        var namedSym = sym as INamedTypeSymbol;
                        var isHelperType = namedSym?.AllInterfaces.Any(i => 
                            i.Name == "ILwxQueueProvider" || 
                            i.Name == "ILwxErrorPolicy" || 
                            i.Name == "ILwxProviderErrorPolicy" ||
                            i.Name == "ILwxQueueMessage") ?? false;
                        
                        if (!hasEndpoint && !hasMessageEndpoint && !hasTimer && !isHelperType)
                        {
                            var descriptor = new DiagnosticDescriptor(
                                "LWX018",
                                "Class in Endpoints namespace must be annotated",
                                "Classes declared in namespaces containing '.Endpoints' must be annotated with [LwxEndpoint], [LwxMessageEndpoint], or [LwxTimer]. Found: '{0}'",
                                "Usage",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true);

                            var loc = cd.Identifier.GetLocation();
                            spc.ReportDiagnostic(Diagnostic.Create(descriptor, loc, sym.ToDisplayString()));
                        }
                    }

                    if (ns.Contains(".Workers", StringComparison.Ordinal))
                    {
                        var has = sym.GetAttributes().Any(a => a.AttributeClass?.Name == "LwxWorkerAttribute");
                        if (!has)
                        {
                            var descriptor = new DiagnosticDescriptor(
                                "LWX019",
                                "Class in Workers namespace must be annotated",
                                "Classes declared in namespaces containing '.Workers' must be annotated with [LwxWorker]. Found: '{0}'",
                                "Usage",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true);

                            var loc = cd.Identifier.GetLocation();
                            spc.ReportDiagnostic(Diagnostic.Create(descriptor, loc, sym.ToDisplayString()));
                        }
                    }


                }
            }
        });

        var lsPass002Attrs = attrProvider.Where(x => x.AttributeName == Processors.LwxConstants.LwxService).Collect();

        context.RegisterSourceOutput
        (
            context.CompilationProvider.Combine(lsPass002Attrs),
            (spc, tuple) =>
            {
                var (compilation, attrs) = tuple;

                var scList = attrs.ToArray();
                if (scList.Length == 0)
                {
                    Processors.LwxServiceTypeProcessor.ReportMissingService(spc, compilation);
                }
                else
                {
                    // Build a dictionary of service namespace prefixes for validation
                    var serviceNamespacePrefixes = new HashSet<string>(StringComparer.Ordinal);

                    foreach (var sc in scList)
                    {
                        if (sc == null) continue;

                        // Extract the service namespace prefix (namespace without trailing segment if it matches type name)
                        var serviceNs = sc.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                        var servicePrefix = serviceNs;

                        // Validate and register
                        if (!serviceNamespacePrefixes.Add(servicePrefix))
                        {
                            // Duplicate service prefix
                            Processors.LwxServiceTypeProcessor.ReportDuplicateServicePrefix(spc, sc.Location, servicePrefix);
                            continue;
                        }

                        // Process the service
                        var rp = new Processors.RootProcessor(this, sc, spc, compilation);
                        rp.Execute();
                    }

                    // After processing all services, check for orphan endpoints/workers/messagehandlers
                    foreach (var kvp in ServiceRegistrations)
                    {
                        if (!serviceNamespacePrefixes.Contains(kvp.Key))
                        {
                            // Report orphan endpoints
                            foreach (var ep in kvp.Value.EndpointInfos)
                            {
                                Processors.LwxServiceTypeProcessor.ReportOrphanEndpoint(spc, kvp.Key, ep.TypeName);
                            }
                            // Report orphan workers
                            foreach (var w in kvp.Value.WorkerInfos)
                            {
                                Processors.LwxServiceTypeProcessor.ReportOrphanWorker(spc, kvp.Key, w.TypeName);
                            }
                            // Report orphan message endpoints
                            foreach (var mh in kvp.Value.MessageEndpointInfos)
                            {
                                Processors.LwxServiceTypeProcessor.ReportOrphanMessageEndpoint(spc, kvp.Key, mh.TypeName);
                            }
                        }
                    }
                }
            }
        );
    }
}

