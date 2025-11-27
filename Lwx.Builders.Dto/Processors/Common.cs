#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Lwx.Builders.Dto.Processors;

internal static class GeneratorHelpers
{
    private static readonly Regex _sanitizer = new("[^a-zA-Z0-9_]+", RegexOptions.Compiled);

    internal static string SafeIdentifier(string value)
        => string.IsNullOrEmpty(value) ? "_" : _sanitizer.Replace(value, "_");

    internal static string PascalSafe(string value)
    {
        var id = SafeIdentifier(value);
        var parts = id.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var p in parts)
        {
            sb.Append(char.ToUpperInvariant(p[0]));
            if (p.Length > 1) sb.Append(p.Substring(1));
        }
        return sb.Length == 0 ? "_" : sb.ToString();
    }

    internal static void AddGeneratedFile(SourceProductionContext ctx, string fileName, string source)
    {
        ctx.AddSource(fileName, SourceText.From(source, System.Text.Encoding.UTF8));
    }

    internal static void AddEmbeddedSource(IncrementalGeneratorPostInitializationContext ctx, string fileName, string generatedName)
    {
        var asm = typeof(object).Assembly; // placeholder to get assembly is not used; we'll obtain our assembly below
        var ourAsm = typeof(GeneratorHelpers).Assembly;
        var expectedName = "Lwx.Builders.Dto." + fileName.Replace('/', '.').Replace('\\', '.');
        var rname = ourAsm.GetManifestResourceNames()
            .FirstOrDefault(n => n == expectedName);
        if (rname != null)
        {
            using var s = ourAsm.GetManifestResourceStream(rname)!;
            using var sr = new System.IO.StreamReader(s);
            var src = sr.ReadToEnd();
            ctx.AddSource(generatedName, SourceText.From(src, System.Text.Encoding.UTF8));
        }
        else
        {
            throw new InvalidOperationException(
                $"Programming error in source generator: Embedded resource '{expectedName}' not found. The Attributes folder must contain embedded source files.");
        }
    }

    internal static void ValidateFilePathMatchesNamespace(ISymbol symbol, SourceProductionContext ctx)
    {
        var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc == null) return;

        var filePath = loc.SourceTree?.FilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        var fullNs = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        var assemblyRoot = symbol.ContainingAssembly?.Name ?? string.Empty;

        string remaining;
        if (string.Equals(fullNs, assemblyRoot, StringComparison.Ordinal))
        {
            remaining = string.Empty;
        }
        else
        {
            remaining = fullNs.StartsWith(assemblyRoot + ".") ? fullNs.Substring(assemblyRoot.Length + 1) : fullNs;
        }
        if (string.IsNullOrEmpty(remaining)) return;

        var segments = remaining.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return;

        string expectedRelative;
        var last = segments[^1];
        if (string.Equals(last, symbol.Name, StringComparison.Ordinal))
        {
            var dirSegments = segments.Length > 1 ? segments.Take(segments.Length - 1).ToArray() : Array.Empty<string>();
            expectedRelative = dirSegments.Length > 0 ? System.IO.Path.Combine(dirSegments) + System.IO.Path.DirectorySeparatorChar + symbol.Name + ".cs" : symbol.Name + ".cs";
        }
        else
        {
            expectedRelative = System.IO.Path.Combine(segments) + System.IO.Path.DirectorySeparatorChar + symbol.Name + ".cs";
        }

        var normalizedFile = filePath.Replace('\\', '/');
        var normalizedExpected = expectedRelative.Replace('\\', '/');

        if (!normalizedFile.EndsWith(normalizedExpected, StringComparison.OrdinalIgnoreCase))
        {
            var descriptor = new DiagnosticDescriptor(
                "LWX007",
                "Source file path does not match namespace",
                "Type '{0}' declared in namespace '{1}' should be located at path ending with '{2}'.",
                "Naming",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            ctx.ReportDiagnostic(Diagnostic.Create(descriptor, loc, symbol.Name, fullNs, expectedRelative));
        }
    }

    internal static IReadOnlyDictionary<string, TypedConstant> ToNamedArgumentMap(this AttributeData? attributeData)
    {
        if (attributeData == null) return new Dictionary<string, TypedConstant>(StringComparer.Ordinal);
        return attributeData.NamedArguments.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }
}

internal static class LwxConstants
{
    public const string LwxDtoAttribute = nameof(Atributes.LwxDtoAttribute);
    public const string LwxDtoPropertyAttribute = nameof(Atributes.LwxDtoPropertyAttribute);
    public const string LwxDtoIgnoreAttribute = nameof(Atributes.LwxDtoIgnoreAttribute);

    public static readonly string LwxDto = LwxDtoAttribute.Replace("Attribute", "");
    public static readonly string LwxDtoProperty = LwxDtoPropertyAttribute.Replace("Attribute", "");
    public static readonly string LwxDtoIgnore = LwxDtoIgnoreAttribute.Replace("Attribute", "");

    public static readonly string[] AttributeNames = new[] { LwxDto, LwxDtoProperty, LwxDtoIgnore };
}

internal sealed class FoundAttribute(string attributeName, ISymbol targetSymbol, Location location, AttributeData? attributeData)
{
    public string AttributeName { get; } = attributeName;
    public ISymbol TargetSymbol { get; } = targetSymbol;
    public Location Location { get; } = location;
    public AttributeData? AttributeData { get; } = attributeData;
}
