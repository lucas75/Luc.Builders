using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Linq;
using Lwx.Builders.MicroService;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxEndpointTypeProcessor(
    FoundAttribute attr,
    SourceProductionContext ctx,
    Compilation _
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

    private string ExtractUriArgument()
    {
        if (attr.AttributeData == null)
        {
            return null;
        }

        var uriNamed = attr.AttributeData.NamedArguments.FirstOrDefault(kv => kv.Key == "Uri");
        if (!uriNamed.Equals(default(KeyValuePair<string, TypedConstant>)) && uriNamed.Value.Value is string s)
        {
            return s;
        }

            
        if (attr.AttributeData.ConstructorArguments.Length > 0 && attr.AttributeData.ConstructorArguments[0].Value is string cs)
        {
            return cs;
        }

        return null;
    }

    private bool ValidateEndpointNaming(string uriArg, string name, string ns)
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
            string namingException = null;
            if (attr.AttributeData != null)
            {
                var exc = attr.AttributeData.NamedArguments.FirstOrDefault(kv => kv.Key == "NamingExceptionJustification");
                if (!exc.Equals(default(KeyValuePair<string, TypedConstant>)) && exc.Value.Value is string txt)
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

    private (string rootNs, string endpointClassName, string optionalFirstSegment) ComputeEndpointNames(string uriArg, string name, string ns)
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

        string endpointClassName = null;
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

    private (string securityProfile, string summary, string description, string publishLiteral) ExtractAttributeMetadata()
    {
        string securityProfile = null;
        string summary = null;
        string description = null;
        string publishLiteral = "Lwx.Builders.MicroService.Atributes.LwxStage.None";

        if (attr.AttributeData != null)
        {
            var m = attr.AttributeData.NamedArguments.FirstOrDefault(kv => kv.Key == "SecurityProfile");
            if (!m.Equals(default(KeyValuePair<string, TypedConstant>)) && m.Value.Value is string s)
            {
                securityProfile = s;
            }

            var m2 = attr.AttributeData.NamedArguments.FirstOrDefault(kv => kv.Key == "Summary");
            if (!m2.Equals(default(KeyValuePair<string, TypedConstant>)) && m2.Value.Value is string s2)
            {
                summary = s2;
            }

            var m3 = attr.AttributeData.NamedArguments.FirstOrDefault(kv => kv.Key == "Description");
            if (!m3.Equals(default(KeyValuePair<string, TypedConstant>)) && m3.Value.Value is string s3)
            {
                description = s3;
            }

            var m4 = attr.AttributeData.NamedArguments.FirstOrDefault(kv => kv.Key == "Publish");
            if (!m4.Equals(default(KeyValuePair<string, TypedConstant>)) && m4.Value.Value != null)
            {
                // typed constant for enum returns the underlying numeric value; map to literal
                var raw = m4.Value.Value;
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
                    // fallback use ToString
                    var tmp = m4.Value.Value.ToString() ?? "Lwx.Builders.MicroService.Atributes.LwxStage.None";
                    publishLiteral = tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService." + tmp);
                }
            }
        }

        return (securityProfile, summary, description, publishLiteral);
    }

    private void GenerateSourceFiles(string name, string ns, string rootNs, string endpointClassName, string optionalFirstSegment, string uriArg, string securityProfile, string summary, string description, string publishLiteral)
    {
        var source = $$"""
            // <auto-generated/>
            using System;
            namespace {{ns}}
            {
                public static partial class LwxEndpoint_Generated_{{name}}
                {
                    // Detected attribute: {{attr.AttributeName}} on {{attr.TargetSymbol.Kind}} {{attr.TargetSymbol.Name}}
                }
            }
            """;

        // Also generate the canonical Endpoints namespace file
        var endpointsNsSource = $$"""
            // <auto-generated/>
                namespace {{rootNs}}.Endpoints
            {
                public static partial class {{endpointClassName}}
                {
                    // Detected attribute: {{attr.AttributeName}} on {{attr.TargetSymbol.Kind}} {{attr.TargetSymbol.Name}}
                }
            }
            """;

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

        var mapMethodName = $"MapLwxEndpoints_{GeneratorHelpers.SafeIdentifier(name)}";
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

        // Use the actual declared type (safe and correct) for mapping. In most cases this equals
        // the generated endpoint class name, but when a naming exception is used the declared type
        // will be different — so prefer the actual declared type symbol.
        var declaredHandlerQName = $"global::{attr.TargetSymbol.ToDisplayString()}";
        var endpointQName = !string.IsNullOrEmpty(nestedNs)
            ? $"global::{rootNs}.Endpoints.{GeneratorHelpers.SafeIdentifier(optionalFirstSegment)}.{GeneratorHelpers.PascalSafe(endpointClassName)}"
            : $"global::{rootNs}.Endpoints.{GeneratorHelpers.PascalSafe(endpointClassName)}";

        var configureSource = $$"""
            // <auto-generated/>
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.Extensions.Hosting;
            using Lwx.Builders.MicroService.Atributes;

            namespace {{ns}}
            {
                public static partial class {{name}}
                {
                    public static void Configure(WebApplication app)
                    {
                        // attribute detected on: {{attr.TargetSymbol.Name}}
                        var __lwxPublish = {{publishLiteral}};
                        if (__lwxPublish != Lwx.Builders.MicroService.Atributes.LwxStage.None)
                        {
                                var shouldMap = app.Environment.IsDevelopment() ?
                                (__lwxPublish == Lwx.Builders.MicroService.Atributes.LwxStage.Development || __lwxPublish == Lwx.Builders.MicroService.Atributes.LwxStage.Production) :
                                (__lwxPublish == Lwx.Builders.MicroService.Atributes.LwxStage.Production);

                            if (shouldMap)
                            {
                                var endpoint = {{(mapMethod == "MapMethods" ? "app.MapMethods(\"" + (pathPart ?? string.Empty) + "\", new[] { \"" + httpVerb + "\" }, " + declaredHandlerQName + ".Execute)" : "app." + mapMethod + "(\"" + (pathPart ?? string.Empty) + "\", " + declaredHandlerQName + ".Execute)")}};
                                endpoint = endpoint.WithName("{{endpointClassName}}");
                                {{(securityProfile is not null ? "endpoint.RequireAuthorization(\"" + securityProfile + "\");" : string.Empty)}}
                                {{(summary is not null ? "endpoint.WithDisplayName(\"" + summary + "\");" : string.Empty)}}
                                endpoint = endpoint.WithMetadata(new LwxEndpointMetadata());
                            }
                        }
                    }
                }
            }
            """;

        GeneratorHelpers.AddGeneratedFile(ctx, $"LwxEndpoint_{name}.Configure.g.cs", configureSource);

        var nestedNsSource = !string.IsNullOrEmpty(optionalFirstSegment)
            ? $$"""
            // <auto-generated/>
            namespace {{rootNs}}.Endpoints.{{GeneratorHelpers.SafeIdentifier(optionalFirstSegment)}}
            {
                public static partial class {{endpointClassName}}
                {
                    // Detected attribute: {{attr.AttributeName}} on {{attr.TargetSymbol.Kind}} {{attr.TargetSymbol.Name}}
                }
            }
            """
            : null;

        GeneratorHelpers.AddGeneratedFile(ctx, $"LwxEndpoint_{name}.g.cs", source);
        if (string.IsNullOrEmpty(nestedNs))
        {
            GeneratorHelpers.AddGeneratedFile(ctx, $"LwxEndpoint_{name}.Endpoints.g.cs", endpointsNsSource);
        }
        if (!string.IsNullOrEmpty(optionalFirstSegment) && nestedNsSource != null)
        {
            GeneratorHelpers.AddGeneratedFile(ctx, $"LwxEndpoint_{name}.Endpoints_{GeneratorHelpers.SafeIdentifier(optionalFirstSegment)}.g.cs", nestedNsSource);
        }
    }
}
