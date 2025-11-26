using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Shared helpers used across the Dto tests.
// NOTE for AI agents: keep helpers minimal and deterministic. Positive tests should not use reflection or sample-project builds.

internal static class SharedTestHelpers
{
    internal record BuildResult(bool BuildSucceeded, string BuildOutput, string RunOutput, Dictionary<string, string> GeneratedFiles);

    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BuildResult> s_sampleBuildCache
        = new(System.StringComparer.OrdinalIgnoreCase);

    // We only need to build committed sample projects for negative (failing) tests. The
    // transient BuildAndRunProject helper and its per-content temporary caches were
    // originally used to dynamically create ephemeral test projects. At the moment these
    // helpers are unused — remove them to simplify the test helpers surface area.

    internal static BuildResult BuildAndRunSampleProject(string sampleName)
    {
        if (s_sampleBuildCache.TryGetValue(sampleName, out var cached))
            return cached;

        var dir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
        var cur = new DirectoryInfo(dir);
        DirectoryInfo? repoRoot = null;
        while (cur != null)
        {
            if (cur.GetFiles("Lwx.Builders.sln").Any())
            {
                repoRoot = cur;
                break;
            }
            cur = cur.Parent;
        }

        if (repoRoot == null) throw new InvalidOperationException("Could not locate repository root (expected to find Lwx.Builders.sln)");

        var sampleDir = Path.Combine(repoRoot.FullName, "Lwx.Builders.Dto.Tests", "SampleProjects", sampleName);
        if (!Directory.Exists(sampleDir)) throw new InvalidOperationException($"Sample project not found: {sampleDir}");

        static (int? exitCode, string output) RunProcess(string filename, string args, string workingDirectory)
        {
            var psi = new ProcessStartInfo(filename, args)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi)!;
            var sb = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();
            return (proc.ExitCode, sb.ToString());
        }

        var (buildExit, buildOut) = RunProcess("dotnet", "build -v:m", sampleDir);

        var generatedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var objDir = Path.Combine(sampleDir, "obj");
        if (Directory.Exists(objDir))
        {
            var files = Directory.EnumerateFiles(objDir, "*LwxDto*.*", SearchOption.AllDirectories).ToList();
            foreach (var f in files)
            {
                try { generatedFiles[f] = File.ReadAllText(f, Encoding.UTF8); } catch { }
            }
        }

        string runOut = string.Empty;
        if (buildExit == 0)
        {
            var (runExit, output) = RunProcess("dotnet", "run --no-build --no-launch-profile -v:minimal", sampleDir);
            runOut = output ?? string.Empty;
        }

        var result = new BuildResult(buildExit == 0, buildOut ?? string.Empty, runOut ?? string.Empty, generatedFiles);
        s_sampleBuildCache.TryAdd(sampleName, result);
        return result;
    }

    internal static DirectoryInfo FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
        var cur = new DirectoryInfo(dir);
        while (cur != null)
        {
            if (cur.GetFiles("Lwx.Builders.sln").Any()) return cur;
            cur = cur.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root (expected to find Lwx.Builders.sln)");
    }

    internal static System.Reflection.Assembly LoadSampleAssembly(string sampleName)
    {
        var repoRoot = FindRepoRoot();
        var dllPath = Path.Combine(repoRoot.FullName, "Lwx.Builders.Dto.Tests", "SampleProjects", sampleName, "bin", "Debug", "net9.0", sampleName + ".dll");
        if (!File.Exists(dllPath)) throw new FileNotFoundException("Built sample assembly not found", dllPath);
        return System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
    }

    internal static object? SerializeAndUnserializeJson(object obj)
    {
        var t = obj.GetType();
        var json = JsonSerializer.Serialize(obj, t);
        return JsonSerializer.Deserialize(json, t);
    }

    // Compile sample project sources in-memory and run the incremental source generators
    // loaded from the Lwx.Builders.Dto compiled assembly. Returns compilation+generator diagnostics
    // RunGeneratorsInMemoryOnSample was removed because the test environment does not have
    // a stable IncrementalGeneratorDriver type across all platform/package combos. We prefer
    // using the committed sample-project builds via BuildAndRunSampleProject for negative-path tests
    // which provides consistent diagnostics and generated sources from real MSBuild runs.
}
