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
        var rname = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (rname != null)
        {
            using var s = asm.GetManifestResourceStream(rname);
            using var sr = new System.IO.StreamReader(s);
            var src = sr.ReadToEnd();
            ctx.AddSource(generatedName, SourceText.From(src, System.Text.Encoding.UTF8));
        }
    }
}
