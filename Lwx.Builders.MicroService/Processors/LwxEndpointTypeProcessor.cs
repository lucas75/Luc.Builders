using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Text;
using Lwx.Builders.MicroService;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxEndpointTypeProcessor(
    FoundAttribute attr,
    SourceProductionContext ctx,
    Compilation compilation
)
{
    public void Execute()
    {
        // enforce file path and namespace matching for classes marked with Lwx attributes
        GeneratorHelpers.ValidateFilePathMatchesNamespace(attr.TargetSymbol, ctx);

        var name = GeneratorHelpers.SafeIdentifier(attr.TargetSymbol.Name);
        var ns = attr.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? "Generated";

        var uriArg = ExtractUriArgument();

        if (!ValidateEndpointNaming(uriArg, name, ns))
        {
            return; // Don't generate code for invalid endpoints
        }

        var (rootNs, endpointClassName, optionalFirstSegment) = ComputeEndpointNames(uriArg, name, ns);

        var (securityProfile, summary, description, publishLiteral) = ExtractAttributeMetadata();

        GenerateSourceFiles(name, ns, rootNs, endpointClassName, optionalFirstSegment, uriArg, securityProfile, summary, description, publishLiteral);
    }

    private string? ExtractUriArgument()
    {
        if (attr.AttributeData == null)
        {
            return null;
        }

        var named = attr.AttributeData.ToNamedArgumentMap();
        if (named.TryGetValue("Uri", out var uriTc) && uriTc.Value is string s)
        {
            return s;
        }

            
        if (attr.AttributeData.ConstructorArguments.Length > 0 && attr.AttributeData.ConstructorArguments[0].Value is string cs)
        {
            return cs;
        }

        return null;
    }

    private bool ValidateEndpointNaming(string? uriArg, string name, string ns)
    {
        if (string.IsNullOrEmpty(uriArg))
        {
            return true; // No URI to validate
        }

        var verbParts = uriArg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var path = verbParts.Length > 1 ? verbParts[1] : verbParts[0];
        var segs = path.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var nameParts = new List<string>();
        foreach (var seg in segs)
        {
            if (seg.StartsWith("{") && seg.EndsWith("}"))
            {
                var pname = seg.Substring(1, seg.Length - 2);
                nameParts.Add("Param" + GeneratorHelpers.PascalSafe(pname));
            }
            else
            {
                nameParts.Add(GeneratorHelpers.PascalSafe(seg));
            }
        }

        // Build expected names: full name and some accepted variants using HTTP verb
        var expectedFull = "Endpoint" + string.Join(string.Empty, nameParts);

        // Build prefix without last segment (if more than 1 part)
        var expectedPrefix = nameParts.Count > 1 ? "Endpoint" + string.Join(string.Empty, nameParts.Take(nameParts.Count - 1)) : expectedFull;

        // derive HTTP verb suffix (e.g., GET -> Get)
        var actualVerb = "GET";
        if (verbParts.Length > 0) actualVerb = verbParts[0].ToUpperInvariant();
        string HttpSuffix(string v) => v switch
        {
            "GET" => "Get",
            "POST" => "Post",
            "PUT" => "Put",
            "DELETE" => "Delete",
            "PATCH" => "Patch",
            _ => GeneratorHelpers.PascalSafe(v.ToLowerInvariant())
        };

        var suffix = HttpSuffix(actualVerb);

        var acceptableNames = new[]
        {
            expectedFull,
            expectedFull + suffix,
            expectedPrefix + suffix
        };

        if (!acceptableNames.Contains(attr.TargetSymbol.Name, StringComparer.Ordinal))
        {
            // Check for a naming exception provided by the user on the attribute
            string? namingException = null;
            if (attr.AttributeData != null)
            {
                var named = attr.AttributeData.ToNamedArgumentMap();
                if (named.TryGetValue("NamingExceptionJustification", out var exc) && exc.Value is string txt)
                {
                    namingException = txt?.Trim();
                }
            }

            if (!string.IsNullOrEmpty(namingException))
            {
                // Informational diagnostic: the generator will accept the non-standard name because developer provided a justification
                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "LWX008",
                        "Endpoint naming exception accepted",
                        "Endpoint class '{0}' does not follow naming rules for URI '{2}', but a naming exception was provided: {1}",
                        "Naming",
                        DiagnosticSeverity.Info,
                        isEnabledByDefault: true),
                    attr.Location,
                    attr.TargetSymbol.Name,
                    namingException,
                    uriArg));

                // Accept as valid because an exception was supplied
                return true;
            }

            // No exception provided — report the standard error
            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX001",
                    "Invalid endpoint class name",
                    "Endpoint class '{0}' does not match the expected name '{1}' for URI '{2}'. Endpoints must follow the naming scheme: <solutionnamespace>.Endpoints.EndpointAbc for path /abc",
                    "Naming",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location,
                    attr.TargetSymbol.Name,
                    string.Join(" or ", acceptableNames),
                uriArg));
            return false;
        }

        // Also validate namespace
        if (!ns.Contains(".Endpoints", StringComparison.Ordinal))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX002",
                    "Invalid endpoint namespace",
                    "Endpoint class must be in a namespace containing '.Endpoints'",
                    "Naming",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location));
            return false;
        }

        return true;
    }

    private (string rootNs, string endpointClassName, string optionalFirstSegment) ComputeEndpointNames(string? uriArg, string name, string ns)
    {
        const string endpointsNamespace = "Endpoints";

        // compute the root namespace: remove trailing '.Endpoints' if it is the last segment
        var rootNs = ns;
        var suffix = "." + endpointsNamespace;
        var idx = rootNs.IndexOf(suffix + ".", StringComparison.Ordinal);
        if (idx >= 0)
        {
            // namespace contains .Endpoints.<segment> - strip from the .Endpoints part
            rootNs = rootNs.Substring(0, idx);
        }
        else if (rootNs.EndsWith(suffix, StringComparison.Ordinal))
        {
            // namespace ends with .Endpoints - strip it
            rootNs = rootNs.Substring(0, rootNs.Length - suffix.Length);
        }

        string? endpointClassName = null;
        string optionalFirstSegment = string.Empty;

        if (!string.IsNullOrEmpty(uriArg))
        {
            // uri: "GET /abc/{cde}/efg"
            var parts = uriArg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var path = parts.Length > 1 ? parts[1] : parts[0];
            var segs = path.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder("Endpoint");
            if (segs.Length > 0)
            {
                optionalFirstSegment = GeneratorHelpers.PascalSafe(segs[0]);
            }
            foreach (var seg in segs)
            {
                if (seg.StartsWith("{") && seg.EndsWith("}"))
                {
                    var pname = seg.Substring(1, seg.Length - 2);
                    sb.Append("Param");
                    sb.Append(GeneratorHelpers.PascalSafe(pname));
                }
                else
                {
                    var s2 = seg;
                    sb.Append(GeneratorHelpers.PascalSafe(s2));
                }
            }
            endpointClassName = sb.ToString();
        }

        endpointClassName ??= $"Endpoint{GeneratorHelpers.SafeIdentifier(attr.TargetSymbol.Name)}";

        return (rootNs, endpointClassName, optionalFirstSegment);
    }

    private (string? securityProfile, string? summary, string? description, string? publishLiteral) ExtractAttributeMetadata()
    {
        string? securityProfile = null;
        string? summary = null;
        string? description = null;
        string publishLiteral = "Lwx.Builders.MicroService.Atributes.LwxStage.None";

            if (attr.AttributeData != null)
            {
                var named = attr.AttributeData.ToNamedArgumentMap();
                if (named.TryGetValue("SecurityProfile", out var sp) && sp.Value is string s)
                {
                    securityProfile = s;
                }

                if (named.TryGetValue("Summary", out var sum) && sum.Value is string s2)
                {
                    summary = s2;
                }

                if (named.TryGetValue("Description", out var d) && d.Value is string s3)
                {
                    description = s3;
                }

                if (named.TryGetValue("Publish", out var p) && p.Value != null)
                {
                    var raw = p.Value;
                    if (raw is int iv)
                    {
                        publishLiteral = iv switch
                        {
                            1 => "Lwx.Builders.MicroService.Atributes.LwxStage.Development",
                            2 => "Lwx.Builders.MicroService.Atributes.LwxStage.Production",
                            _ => "Lwx.Builders.MicroService.Atributes.LwxStage.None"
                        };
                    }
                    else
                    {
                        var tmp = p.Value.ToString() ?? "Lwx.Builders.MicroService.Atributes.LwxStage.None";
                        publishLiteral = tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService." + tmp);
                    }
                }
            }

        return (securityProfile, summary, description, publishLiteral);
    }

    private void GenerateSourceFiles(string name, string ns, string rootNs, string endpointClassName, string? optionalFirstSegment, string? uriArg, string? securityProfile, string? summary, string? description, string? publishLiteral)
    {
        // NOTE: prior versions generated separate placeholder/marker classes and
        // additional endpoint files. We now emit a single generated file per endpoint
        // that matches the endpoint class naming rules (for example: EndpointAbcCde.g.cs).

        // Generate a mapping extension method in the consumer namespace. This produces iterative
        // minimal-API mapping calls (e.g., app.MapGet / app.MapPost) and applies simple stage
        // filtering and authorization wiring based on attribute named arguments.
        var httpVerb = "GET";
        if (!string.IsNullOrEmpty(uriArg))
        {
            var parts = uriArg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                httpVerb = parts[0].ToUpperInvariant();
            }
        }

        var pathPart = string.IsNullOrEmpty(uriArg) ? string.Empty : (uriArg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 1 ? uriArg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1] : uriArg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0]);
        var mapMethod = httpVerb switch
        {
            "GET" => "MapGet",
            "POST" => "MapPost",
            "PUT" => "MapPut",
            "DELETE" => "MapDelete",
            "PATCH" => "MapMethods", // PATCH not a dedicated overload
            _ => "MapMethods"
        };

        // Only use the first path segment as a nested namespace if the target symbol namespace actually has this segment.
        var targetNs = attr.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        var nestedNs = !string.IsNullOrEmpty(optionalFirstSegment) && targetNs.IndexOf($".Endpoints.{optionalFirstSegment}", StringComparison.OrdinalIgnoreCase) >= 0
            ? optionalFirstSegment : null;

        // No longer generate separate placeholder or mapping files — this method emits a
        // single generated file per endpoint containing the `Configure(WebApplication app)` helper.

        // Compute a short publish token for human-readable comment output.
        var shortPublish = publishLiteral != null && publishLiteral.Contains('.')
            ? string.Join('.', publishLiteral.Split('.').Skip(Math.Max(0, publishLiteral.Split('.').Length - 2)))
            : publishLiteral;

        // Build a single consolidated generated file containing the
        // endpoint partial class with the Configure method (in the Endpoints namespace).
        var headerUsings = new[]
        {
            "Microsoft.AspNetCore.Builder",
            "Microsoft.AspNetCore.Routing",
            "Microsoft.AspNetCore.Authorization",
            "Microsoft.Extensions.Hosting",
            "Lwx.Builders.MicroService.Atributes"
        };

        var usingsBlock = string.Join("\n", headerUsings.Select(u => $"using {u};"));

        // Decide where the endpoint class should live (rootNs.Endpoints or nested sub-namespace)
        // (kept as informative metadata only — the generated class is placed in `ns` below)

        // Build the Configure method body depending on publish setting.
        string configureMethod;
        if (publishLiteral != null && publishLiteral.EndsWith(".None", StringComparison.Ordinal))
        {
            configureMethod = $$"""
                    public static void Configure(WebApplication app)
                    {
                        // Publish={{shortPublish}}
                    }
                """;
        }
        else
        {
            var condExpr = publishLiteral != null && publishLiteral.EndsWith(".Development", StringComparison.Ordinal)
                ? "app.Environment.IsDevelopment()"
                : "app.Environment.IsDevelopment() || app.Environment.IsProduction()";

            configureMethod = $$"""
                public static void Configure(WebApplication app)
                {
                    // Publish={{shortPublish}}
                    if ({{condExpr}})
                    {
                        var endpoint = {{(mapMethod == "MapMethods" ? "app.MapMethods(\"" + (pathPart ?? string.Empty) + "\", new[] { \"" + httpVerb + "\" }, Execute)" : "app." + mapMethod + "(\"" + (pathPart ?? string.Empty) + "\", Execute)")}};
                        endpoint = endpoint.WithName("{{endpointClassName}}");
                        {{(securityProfile is not null ? "endpoint.RequireAuthorization(\"" + securityProfile + "\");" : string.Empty)}}
                        {{(summary is not null ? "endpoint.WithDisplayName(\"" + summary + "\");" : string.Empty)}}
                        endpoint = endpoint.WithMetadata(new LwxEndpointMetadata());
                    }
                }
                """;
        }

        // Build file content using raw string templates and FixIndent to ensure consistent
        // indentation and easier template maintenance.
        var source = $$"""
        // <auto-generated/>
        using System;
        {{usingsBlock}}

        namespace {{ns}};
        
        public static partial class {{name}}
        {
            {{configureMethod.FixIndent(1, indentFirstLine: false)}}
        }
        
        """;

        // Decide file name: if the attribute included a NamingExceptionJustification we include the full
        // namespace in the generated file name to avoid accidental collisions caused by legacy names.
        string? namingException = null;
        if (attr.AttributeData != null)
        {
            var named = attr.AttributeData.ToNamedArgumentMap();
            if (named.TryGetValue("NamingExceptionJustification", out var exc) && exc.Value is string txt)
            {
                namingException = txt?.Trim();
            }
        }

        var generatedFileName = !string.IsNullOrEmpty(namingException)
            ? $"{ns}.{name}.g.cs"
            : $"{name}.g.cs";

        GeneratorHelpers.AddGeneratedFile(ctx, generatedFileName, source);
    }
}
