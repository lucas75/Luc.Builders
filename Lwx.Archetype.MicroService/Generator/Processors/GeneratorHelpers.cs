using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Lwx.Archetype.MicroService.Generator.Processors;

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
        var asm = typeof(LwxArchetypeGenerator).Assembly;
        var expectedName = "Lwx.Archetype.MicroService." + fileName.Replace('/', '.').Replace('\\', '.');
        var rname = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n == expectedName);
        if (rname != null)
        {
            using var s = asm.GetManifestResourceStream(rname);
            using var sr = new System.IO.StreamReader(s);
            var src = sr.ReadToEnd();
            ctx.AddSource(generatedName, SourceText.From(src, System.Text.Encoding.UTF8));
        }
        else
        {
            throw new InvalidOperationException(
                $"Programming error in source generator: Embedded resource '{expectedName}' not found. " +
                "The Attributes folder contains templates that must be embedded as source. " +
                "Generator/Templates contains templates that must be embedded. " +
                "Ensure the file paths and LogicalName in the project file match the internal names."
            );
        }
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

        var segments = remaining.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
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
}
