using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Lwx.Builders.Dto.Tests.MockServices;

/// <summary>
/// Lightweight test harness to run the `Lwx.Builders.Dto` incremental generator
/// in-memory using `CSharpGeneratorDriver`. Mirrors the helper used by
/// `Lwx.Builders.MicroService.Tests` so Dto tests can run without MSBuild.
/// </summary>
public static class MockCompiler
{
    private static readonly object s_lock = new();
    private static ImmutableArray<MetadataReference>? s_cachedReferences;

    public sealed record GeneratorRunResult(
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyDictionary<string, string> GeneratedSources,
        bool HasErrors);

    public static GeneratorRunResult RunGenerator(IReadOnlyDictionary<string, string> sources)
    {
        var references = GetMetadataReferences();

        var syntaxTrees = sources.Select(kv =>
            CSharpSyntaxTree.ParseText(kv.Value, path: kv.Key, encoding: System.Text.Encoding.UTF8));

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        object? generator = null;
        // Try to use the generator type directly if available at compile time
        try
        {
            generator = Activator.CreateInstance(Type.GetType("Lwx.Builders.Dto.DtoGenerator, Lwx.Builders.Dto")!);
        }
        catch { }

        // If direct creation failed (the generator assembly is not a referenced runtime assembly),
        // attempt to locate and load the compiled generator assembly from the repo output path.
        if (generator == null)
        {
            var repoRoot = FindRepoRoot();
            var candidate = System.IO.Path.Combine(repoRoot.FullName, "Lwx.Builders.Dto", "bin", "Debug", "net9.0", "Lwx.Builders.Dto.dll");
            if (System.IO.File.Exists(candidate))
            {
                try
                {
                    var asm = Assembly.LoadFrom(candidate);
                    var t = asm.GetType("Lwx.Builders.Dto.DtoGenerator");
                    if (t != null) generator = Activator.CreateInstance(t);
                }
                catch
                {
                    // ignore; we'll fallback to attempting dynamic creation below
                }
            }
        }

        if (generator == null)
            throw new InvalidOperationException("Could not create an instance of Lwx.Builders.Dto.DtoGenerator. Ensure the generator project is built.");

        var driver = CSharpGeneratorDriver.Create((dynamic)generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);

        var allDiagnostics = generatorDiagnostics.AddRange(outputCompilation.GetDiagnostics());

        var relevantDiagnostics = allDiagnostics
            .Where(d => d.Severity >= DiagnosticSeverity.Warning || d.Id.StartsWith("LWX", StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();

        var generatedSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var runResult = driver.GetRunResult();
        foreach (var tree in runResult.GeneratedTrees)
        {
            var name = System.IO.Path.GetFileName(tree.FilePath);
            generatedSources[name] = tree.GetText().ToString();
        }

        var hasErrors = relevantDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

        return new GeneratorRunResult(relevantDiagnostics, generatedSources, hasErrors);
    }

    public static bool HasDiagnostic(GeneratorRunResult result, string diagnosticId)
    {
        return result.Diagnostics.Any(d => string.Equals(d.Id, diagnosticId, StringComparison.OrdinalIgnoreCase));
    }

    public static string FormatDiagnostics(GeneratorRunResult result)
    {
        return string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString()));
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        if (s_cachedReferences.HasValue) return s_cachedReferences.Value;

        lock (s_lock)
        {
            if (s_cachedReferences.HasValue) return s_cachedReferences.Value;

            var references = new List<MetadataReference>();

            var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")?.ToString()?.Split(System.IO.Path.PathSeparator) ?? Array.Empty<string>();
            foreach (var assemblyPath in trustedAssemblies)
            {
                if (System.IO.File.Exists(assemblyPath))
                {
                    try { references.Add(MetadataReference.CreateFromFile(assemblyPath)); } catch { }
                }
            }

                // Do NOT add the generator assembly as a metadata reference here: the generator
                // will emit attribute sources via post-initialization and those generated
                // sources are added to the output compilation by RunGeneratorsAndUpdateCompilation.

            s_cachedReferences = references.ToImmutableArray();
            return s_cachedReferences.Value;
        }
    }

    private static System.IO.DirectoryInfo FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
        var cur = new System.IO.DirectoryInfo(dir);
        while (cur != null)
        {
            if (cur.GetFiles("Lwx.Builders.sln").Any()) return cur;
            cur = cur.Parent!;
        }
        throw new InvalidOperationException("Could not locate repository root (expected to find Lwx.Builders.sln)");
    }
}
