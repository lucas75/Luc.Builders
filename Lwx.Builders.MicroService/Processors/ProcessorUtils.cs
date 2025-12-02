using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Lwx.Builders.MicroService.Processors;

internal static class LwxConstants
{
    public const string LwxEndpoint = "LwxEndpoint";
    public const string LwxEndpointAttribute = "LwxEndpointAttribute";

    public const string LwxWorker = "LwxWorker";
    public const string LwxWorkerAttribute = "LwxWorkerAttribute";

    public const string LwxServiceBusConsumer = "LwxServiceBusConsumer";
    public const string LwxServiceBusConsumerAttribute = "LwxServiceBusConsumerAttribute";

    public const string LwxEventHubConsumer = "LwxEventHubConsumer";
    public const string LwxEventHubConsumerAttribute = "LwxEventHubConsumerAttribute";

    public const string LwxTimer = "LwxTimer";
    public const string LwxTimerAttribute = "LwxTimerAttribute";

    public const string LwxServiceBusProducer = "LwxServiceBusProducer";
    public const string LwxServiceBusProducerAttribute = "LwxServiceBusProducerAttribute";

    public const string LwxService = "LwxService";
    public const string LwxServiceAttribute = "LwxServiceAttribute";


    public static readonly string[] AttributeNames = [
        LwxEndpoint,
        LwxWorker,
        LwxServiceBusConsumer,
        LwxEventHubConsumer,
        LwxTimer,
        LwxServiceBusProducer,
        LwxService
    ];
}



internal static class ProcessorUtils
{
    private static readonly Regex _sanitizer = new("[^a-zA-Z0-9_]+", RegexOptions.Compiled);

    internal static string SafeIdentifier(string? value)
        => string.IsNullOrEmpty(value) ? "_" : _sanitizer.Replace(value, "_");

    internal static string PascalSafe(string value)
    {
        var id = SafeIdentifier(value);
        var parts = id.Split(['_'], StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var p in parts)
        {
            sb.Append(char.ToUpperInvariant(p[0]));
            if (p.Length > 1) sb.Append(p[1..]);
        }
        return sb.Length == 0 ? "_" : sb.ToString();
    }

    internal static void AddGeneratedFile(SourceProductionContext ctx, string fileName, string source)
    {
        ctx.AddSource(fileName, SourceText.From(source, System.Text.Encoding.UTF8));
    }

    internal static void AddEmbeddedSource(IncrementalGeneratorPostInitializationContext ctx, string fileName, string generatedName)
    {
        var asm = typeof(Generator).Assembly;
        var expectedName = "Lwx.Builders.MicroService." + fileName.Replace('/', '.').Replace('\\', '.');
        var rname = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n == expectedName);
        if (rname != null)
        {
            using var s = asm.GetManifestResourceStream(rname)!;
            using var sr = new System.IO.StreamReader(s);
            var src = sr.ReadToEnd();
            ctx.AddSource(generatedName, SourceText.From(src, System.Text.Encoding.UTF8));
        }
        else
        {
            throw new InvalidOperationException(
                $"Programming error in source generator: Embedded resource '{expectedName}' not found. " +
                "The Attributes folder contains templates that must be embedded as source. " +
                "Templates contains templates that must be embedded. " +
                "Ensure the file paths and LogicalName in the project file match the internal names."
            );
        }
    }

    internal static IReadOnlyDictionary<string, TypedConstant> ToNamedArgumentMap(this AttributeData? attributeData)
    {
        if (attributeData == null) return new Dictionary<string, TypedConstant>(StringComparer.Ordinal);
        // Convert the ImmutableArray<KeyValuePair<string, TypedConstant>> into a dictionary for easy lookup
        return attributeData.NamedArguments.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }

    internal static void ValidateFilePathMatchesNamespace(ISymbol symbol, SourceProductionContext ctx)
    {
        // If missing source location, skip validation (could be metadata or generated)
        var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc == null) return;

        var filePath = loc.SourceTree?.FilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        var fullNs = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        var assemblyRoot = symbol.ContainingAssembly?.Name ?? string.Empty;

        // Compute remaining namespace segments after assembly root
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

        var segments = remaining.Split(['.'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return;

        // Expected relative path: If the last namespace segment equals the type name then the file path
        // should be <...previousSegments>/<typeName>.cs (e.g. namespace MyProj.Abc.Cde -> Abc/Cde.cs).
        // Otherwise the file path should be <...segments>/<TypeName>.cs (e.g. namespace MyProj.Endpoints.ExampleProc -> Endpoints/ExampleProc/TypeName.cs).
        string expectedRelative;
        var last = segments[^1];
        if (string.Equals(last, symbol.Name, StringComparison.Ordinal))
        {
            // use previous segments as folder(s), final file is last (type name)
            var dirSegments = segments.Length > 1 ? segments.Take(segments.Length - 1).ToArray() : Array.Empty<string>();
            expectedRelative = dirSegments.Length > 0 ? System.IO.Path.Combine(dirSegments) + System.IO.Path.DirectorySeparatorChar + symbol.Name + ".cs" : symbol.Name + ".cs";
        }
        else
        {
            expectedRelative = System.IO.Path.Combine(segments) + System.IO.Path.DirectorySeparatorChar + symbol.Name + ".cs";
        }

        // Normalize separators
        var normalizedFile = filePath.Replace('\\', '/');
        var normalizedExpected = expectedRelative.Replace('\\', '/');

        if (!normalizedFile.EndsWith(normalizedExpected, StringComparison.OrdinalIgnoreCase))
        {
            // Propose message with expected suffix to help developer
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

    /// <summary>
    /// Extract a type reference suitable for use in generated call-sites. This returns the
    /// fully-qualified type name (namespace + '.' + type name) for a symbol that has a
    /// source namespace. If the symbol has no containing namespace, the simple type name
    /// is returned.
    /// </summary>
    internal static string ExtractRelativeTypeName(ISymbol symbol, Compilation compilation)
    {
        if (symbol == null) return string.Empty;
        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        var name = symbol.Name;
        if (string.IsNullOrEmpty(ns)) return name;
        return ns + "." + name;
    }
}
