using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace Lwx.MicroService.Generator.Processors;

internal class LwxServiceConfigTypeProcessor(
    FoundAttribute attr,
    SourceProductionContext ctx,
    Compilation compilation
)
{
    public void Execute()
    {
        // enforce file path and namespace matching for service config marker classes
        GeneratorHelpers.ValidateFilePathMatchesNamespace(attr.TargetSymbol, ctx);
        // Ensure the service config attribute is only used in a file explicitly named ServiceConfig.cs
        var loc = attr.TargetSymbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc != null)
        {
            var fp = loc.SourceTree?.FilePath;
            if (!string.IsNullOrEmpty(fp))
            {
                var fileName = System.IO.Path.GetFileName(fp);
                if (!string.Equals(fileName, "ServiceConfig.cs", StringComparison.OrdinalIgnoreCase))
                {
                    var descriptor = new DiagnosticDescriptor(
                        "LWX012",
                        "ServiceConfig must be declared in ServiceConfig.cs",
                        "[LwxServiceConfig] must be declared in a file named 'ServiceConfig.cs'. Found in '{0}'",
                        "Configuration",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true);

                    ctx.ReportDiagnostic(Diagnostic.Create(descriptor, loc, fileName));
                    return; // bail out - incorrect usage
                }
            }
        }

        var name = GeneratorHelpers.SafeIdentifier(attr.TargetSymbol.Name);
        var ns = attr.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? "Generated";

        string title = null;
        string description = null;
        string version = null;
        string publishLiteral = "Lwx.MicroService.Atributes.LwxStage.None";
        bool generateMain = false;

        if (attr.AttributeData != null)
        {
            var m1 = attr.AttributeData.NamedArguments.FirstOrDefault(kv => kv.Key == "Title");
            if (!m1.Equals(default(KeyValuePair<string, TypedConstant>)) && m1.Value.Value is string s1)
            {
                title = s1;
            }
            var m2 = attr.AttributeData.NamedArguments.FirstOrDefault(kv => kv.Key == "Description");
            if (!m2.Equals(default(KeyValuePair<string, TypedConstant>)) && m2.Value.Value is string s2)
            {
                description = s2;
            }
            var m3 = attr.AttributeData.NamedArguments.FirstOrDefault(kv => kv.Key == "Version");
            if (!m3.Equals(default(KeyValuePair<string, TypedConstant>)) && m3.Value.Value is string s3)
            {
                version = s3;
            }

            var m4 = attr.AttributeData.NamedArguments.FirstOrDefault(kv => kv.Key == "PublishSwagger");
            if (!m4.Equals(default(KeyValuePair<string, TypedConstant>)) && m4.Value.Value != null)
            {
                var raw = m4.Value.Value;
                if (raw is int iv)
                {
                    publishLiteral = iv switch
                    {
                        1 => "Lwx.MicroService.Atributes.LwxStage.Development",
                        2 => "Lwx.MicroService.Atributes.LwxStage.Production",
                        _ => "Lwx.MicroService.Atributes.LwxStage.None"
                    };
                }
                else
                {
                    var tmp = m4.Value.Value.ToString() ?? "Lwx.MicroService.Atributes.LwxStage.None";
                    publishLiteral = tmp.Contains('.') ? tmp : ("Lwx.MicroService.Atributes.LwxStage." + tmp);
                }
            }

            var m5 = attr.AttributeData.NamedArguments.FirstOrDefault(kv => kv.Key == "GenerateMain");
            if (!m5.Equals(default(KeyValuePair<string, TypedConstant>)) && m5.Value.Value is bool b)
            {
                generateMain = b;
            }
        }

        // If the publish stage is active (not None) make sure Swashbuckle is available
        if (publishLiteral != "Lwx.MicroService.Atributes.LwxStage.None")
        {
            var openApiInfoType = compilation.GetTypeByMetadataName("Microsoft.OpenApi.Models.OpenApiInfo");
            if (openApiInfoType == null)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "LWX003",
                        "Missing Swashbuckle package",
                        "LwxServiceConfig requires the Swashbuckle.AspNetCore package. Install it using: dotnet add package Swashbuckle.AspNetCore",
                        "Dependencies",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    attr.Location));
                return;
            }
        }

        // Validate ServiceConfig methods signatures and public surface
        var typeSymbol = attr.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol != null)
        {
            // The validation is now done globally in the generator for all ServiceConfig classes
        }

        var configureSource = $$"""
            // <auto-generated/>
            using Lwx.MicroService.Atributes;

            namespace {{ns}}
            {
                public static partial class {{name}}
                {
                    // Service configuration (swagger/openapi wiring) is handled in LwxConfigure extensions
                }
            }
            """;

        GeneratorHelpers.AddGeneratedFile(ctx, $"LwxServiceConfig_{name}.Configure.g.cs", configureSource);
    }
}
