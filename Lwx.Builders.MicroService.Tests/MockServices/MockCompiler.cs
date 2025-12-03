using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Lwx.Builders.MicroService.Tests.MockServices;

/// <summary>
/// Thread-safe utility class that creates a CSharpGeneratorDriver-based test harness
/// for the Lwx.Builders.MicroService source generator. This allows tests to run
/// in-memory without invoking MSBuild.
/// </summary>
public static class MockCompiler
{
    private static readonly object s_lock = new();
    private static ImmutableArray<MetadataReference>? s_cachedReferences;

    /// <summary>
    /// Result of running the generator.
    /// </summary>
    public sealed record GeneratorRunResult(
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyDictionary<string, string> GeneratedSources,
        bool HasErrors);

    /// <summary>
    /// Runs the MicroService generator against the provided source files and returns diagnostics and generated sources.
    /// </summary>
    /// <param name="sources">Dictionary of file name to source code content.</param>
    /// <returns>A GeneratorRunResult containing diagnostics and generated sources.</returns>
    public static GeneratorRunResult RunGenerator(IReadOnlyDictionary<string, string> sources)
    {
        var references = GetMetadataReferences();

        var syntaxTrees = sources.Select(kv =>
            CSharpSyntaxTree.ParseText(kv.Value, path: kv.Key, encoding: System.Text.Encoding.UTF8));

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var generator = new Lwx.Builders.MicroService.Generator();

        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        // Collect all diagnostics: generator diagnostics + compilation diagnostics from outputCompilation
        var allDiagnostics = generatorDiagnostics.AddRange(outputCompilation.GetDiagnostics());

        // Filter to errors and warnings only (ignore hidden/info unless they have LWX prefix)
        var relevantDiagnostics = allDiagnostics
            .Where(d => d.Severity >= DiagnosticSeverity.Warning || d.Id.StartsWith("LWX", StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();

        // Collect generated sources
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

    /// <summary>
    /// Checks if the diagnostics contain a specific diagnostic ID.
    /// </summary>
    public static bool HasDiagnostic(GeneratorRunResult result, string diagnosticId)
    {
        return result.Diagnostics.Any(d => string.Equals(d.Id, diagnosticId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all diagnostic IDs from the result.
    /// </summary>
    public static IEnumerable<string> GetDiagnosticIds(GeneratorRunResult result)
    {
        return result.Diagnostics.Select(d => d.Id).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Formats all diagnostics as a string for debugging.
    /// </summary>
    public static string FormatDiagnostics(GeneratorRunResult result)
    {
        return string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString()));
    }

    /// <summary>
    /// Gets (and caches) the metadata references required to compile code that uses
    /// ASP.NET Core, Microsoft.Extensions.Hosting, and Lwx attributes.
    /// </summary>
    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        if (s_cachedReferences.HasValue)
            return s_cachedReferences.Value;

        lock (s_lock)
        {
            if (s_cachedReferences.HasValue)
                return s_cachedReferences.Value;

            var references = new List<MetadataReference>();

            // Add runtime assemblies
            var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")?.ToString()?.Split(System.IO.Path.PathSeparator) ?? [];

            foreach (var assemblyPath in trustedAssemblies)
            {
                if (System.IO.File.Exists(assemblyPath))
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(assemblyPath));
                    }
                    catch
                    {
                        // Skip assemblies that cannot be loaded
                    }
                }
            }

            // Add Microsoft.AspNetCore.* assemblies if available (for WebApplicationBuilder, WebApplication, etc.)
            AddAssemblyIfAvailable(references, "Microsoft.AspNetCore");
            AddAssemblyIfAvailable(references, "Microsoft.AspNetCore.Http.Abstractions");
            AddAssemblyIfAvailable(references, "Microsoft.AspNetCore.Routing");
            AddAssemblyIfAvailable(references, "Microsoft.AspNetCore.Routing.Abstractions");
            AddAssemblyIfAvailable(references, "Microsoft.Extensions.Hosting");
            AddAssemblyIfAvailable(references, "Microsoft.Extensions.Hosting.Abstractions");
            AddAssemblyIfAvailable(references, "Microsoft.Extensions.DependencyInjection");
            AddAssemblyIfAvailable(references, "Microsoft.Extensions.DependencyInjection.Abstractions");
            AddAssemblyIfAvailable(references, "Microsoft.Extensions.Logging");
            AddAssemblyIfAvailable(references, "Microsoft.Extensions.Logging.Abstractions");

            // Add the generator assembly itself so consumer source code referencing
            // generator-defined attributes (e.g., Lwx.Builders.MicroService.Atributtes) can compile.
            try
            {
                var genAsm = typeof(Lwx.Builders.MicroService.Generator).Assembly;
                if (!string.IsNullOrEmpty(genAsm.Location) && System.IO.File.Exists(genAsm.Location))
                {
                    references.Add(MetadataReference.CreateFromFile(genAsm.Location));
                }
            }
            catch
            {
                // Ignore if generator assembly cannot be located. The generator may still run via syntax analysis.
            }

            s_cachedReferences = references.ToImmutableArray();
            return s_cachedReferences.Value;
        }
    }

    private static void AddAssemblyIfAvailable(List<MetadataReference> references, string assemblyName)
    {
        try
        {
            var asm = Assembly.Load(assemblyName);
            if (!string.IsNullOrEmpty(asm.Location) && System.IO.File.Exists(asm.Location))
            {
                references.Add(MetadataReference.CreateFromFile(asm.Location));
            }
        }
        catch
        {
            // Assembly not available — skip
        }
    }
}
