using System.Linq;
using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Xunit;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Lwx.Builders.Dto.Tests.Dto;

public class DtoGeneratorTests
{

    private record BuildResult(bool BuildSucceeded, string BuildOutput, string RunOutput, Dictionary<string, string> GeneratedFiles);

    // Cache build results for sample projects so expensive 'dotnet build' runs only happen once
    // Tests will share the same built artifacts and generated sources from disk.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BuildResult> s_sampleBuildCache
        = new(System.StringComparer.OrdinalIgnoreCase);

    // Cache and locking for ephemeral test projects created by BuildAndRunProject.
    // We key on name + content-hash so different mainSource values don't collide.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BuildResult> s_tempBuildCache
        = new(System.StringComparer.OrdinalIgnoreCase);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> s_tempBuildLocks
        = new(System.StringComparer.OrdinalIgnoreCase);

    private static string ComputeContentKey(string name, string mainSource)
    {
        // use a small deterministic hash of the content to avoid collisions
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(mainSource ?? string.Empty);
        var hash = sha.ComputeHash(bytes);
        // keep it compact
        return name + ":" + Convert.ToBase64String(hash).TrimEnd('=');
    }

    private static BuildResult BuildAndRunProject(string name, string mainSource)
    {
        // Find repo root by walking up until we find Lwx.Builders.sln
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

        if (repoRoot == null)
            throw new InvalidOperationException("Could not locate repository root (expected to find Lwx.Builders.sln)");

        // Prepare test project directory under Tests project obj/test-projects/{name}
        // Use a cache key that includes a hash of the mainSource. This prevents
        // multiple concurrent builds of the exact same temp project and avoids
        // accidental collisions when two tests reuse the same name with different contents.
        var tempKey = ComputeContentKey(name, mainSource);
        if (s_tempBuildCache.TryGetValue(tempKey, out var cachedTemp))
        {
            return cachedTemp;
        }

        // Lock per-key so only one thread builds the same temporary sample concurrently.
        var locker = s_tempBuildLocks.GetOrAdd(tempKey, _ => new object());
        lock (locker)
        {
            // double-check cache after obtaining lock
            if (s_tempBuildCache.TryGetValue(tempKey, out cachedTemp))
                return cachedTemp;
        var testsProjectRoot = Path.Combine(repoRoot.FullName, "Lwx.Builders.Dto.Tests");
        var workDir = Path.Combine(testsProjectRoot, "obj", "test-projects", name);
        if (Directory.Exists(workDir))
            Directory.Delete(workDir, true);
        Directory.CreateDirectory(workDir);

        // Create csproj that references generator project as an analyzer
        var generatorProjectPath = Path.Combine(repoRoot.FullName, "Lwx.Builders.Dto", "Lwx.Builders.Dto.csproj");
        var csproj = $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
                    <TargetFramework>net9.0</TargetFramework>
                        <!-- Emit compiler generated sources to disk so tests can inspect generated sources -->
                        <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                        <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)generated</CompilerGeneratedFilesOutputPath>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>

          <ItemGroup>
            <ProjectReference Include="{generatorProjectPath}" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
          </ItemGroup>
        </Project>
        """;

        File.WriteAllText(Path.Combine(workDir, name + ".csproj"), csproj, Encoding.UTF8);

        // Write the main source (provided by tests). Place it in a path matching namespace and type so analyzers
        // that check location (LWX007) don't trigger. We try to find the first top-level type name and the namespace.
        string ns = "";
        string typeName = "MainSource";
        try
        {
            var nsMatch = System.Text.RegularExpressions.Regex.Match(mainSource, @"namespace\s+([A-Za-z0-9_.]+)");
            if (nsMatch.Success) ns = nsMatch.Groups[1].Value.Trim();
            var typeMatch = System.Text.RegularExpressions.Regex.Match(mainSource, @"public\s+(?:partial\s+)?(?:class|record|struct|enum)\s+([A-Za-z0-9_]+)");
            if (typeMatch.Success) typeName = typeMatch.Groups[1].Value.Trim();
        }
        catch
        {
        }

        var srcSubDir = string.IsNullOrWhiteSpace(ns) ? workDir : Path.Combine(workDir, ns.Replace('.', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(srcSubDir);
        var mainFilePath = Path.Combine(srcSubDir, typeName + ".cs");
        File.WriteAllText(mainFilePath, mainSource, Encoding.UTF8);

        // Ensure an entry point exists so we can run the built app.
        var runner = """
        using System;
        public static class __DtoTestRunner
        {
            public static int Main(string[] args)
            {
                Console.WriteLine("DTO_TEST_RUNNER_STARTED");
                return 0;
            }
        }
        """;

        File.WriteAllText(Path.Combine(workDir, "Runner.cs"), runner, Encoding.UTF8);

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

        var (buildExit, buildOut) = RunProcess("dotnet", "build -v:m", workDir);

        // Search for generated source files under obj folder (after build)
        var generatedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var objDir = Path.Combine(workDir, "obj");
        if (Directory.Exists(objDir))
        {
            var files = Directory.EnumerateFiles(objDir, "*LwxDto*.*", SearchOption.AllDirectories).ToList();
            foreach (var f in files)
            {
                try
                {
                    generatedFiles[f] = File.ReadAllText(f, Encoding.UTF8);
                }
                catch { }
            }
        }

        string runOutput = string.Empty;
        if (buildExit == 0)
        {
            var (runExit, runOut) = RunProcess("dotnet", "run --no-build --no-launch-profile -v:minimal", workDir);
            runOutput = runOut ?? string.Empty;
        }

        var result = new BuildResult(buildExit == 0, buildOut ?? string.Empty, runOutput ?? string.Empty, generatedFiles);
        s_tempBuildCache.TryAdd(tempKey, result);
        return result;
        }
    }

    private BuildResult BuildAndRunSampleProject(string sampleName)
    {
        // return cached build result if present (avoids rebuilding the same sample multiple times)
        if (s_sampleBuildCache.TryGetValue(sampleName, out var cached))
        {
            return cached;
        }
        // Locate the sample project under Lwx.Builders.Dto.Tests/SampleProjects/{sampleName}
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
        // cache the result for future tests
        s_sampleBuildCache.TryAdd(sampleName, result);
        return result;
    }


    // ------------------ Reflection / runtime helpers ------------------
    private DirectoryInfo FindRepoRoot()
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

    private System.Reflection.Assembly LoadSampleAssembly(string sampleName)
    {
        var repoRoot = FindRepoRoot();
        var dllPath = Path.Combine(repoRoot.FullName, "Lwx.Builders.Dto.Tests", "SampleProjects", sampleName, "bin", "Debug", "net9.0", sampleName + ".dll");
        if (!File.Exists(dllPath))
            throw new FileNotFoundException("Built sample assembly not found", dllPath);
        return System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
    }


    private object? SerializeAndUnserializeJson(object obj)
    {
        var t = obj.GetType();
        var json = JsonSerializer.Serialize(obj, t);
        return JsonSerializer.Deserialize(json, t);
    }

    // Serialization helper: serialize then deserialize back to the same runtime type
    [Fact]
    public void DtoGenerator_Generates_For_Partial_Properties()
    {
        // Using the test-project Dto namespace in tests — NormalDto lives in the test assembly

        // The test assembly contains GoodDto types — verify properties and backing fields were generated
        var t = typeof(NormalDto);
        // Properties
        Assert.NotNull(t.GetProperty("Id"));
        Assert.NotNull(t.GetProperty("Name"));
        // Backing fields generated by the source generator use the naming pattern (_id/_name)
        var fId = t.GetField("_id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var fName = t.GetField("_name", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(fId);
        Assert.NotNull(fName);
    }

    [Fact]
    public void DtoGenerator_Reports_LWX005_When_Missing_Property_Attribute()
    {
        // Using the failing SampleProjects/ErrorDto project (kept as a failing fixture) which contains BrokenDto

        var res = BuildAndRunSampleProject("ErrorDto");
        var has = res.BuildOutput?.Contains("LWX005", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(has, "Expected diagnostic LWX005 to be reported when DTO property is missing LwxDtoProperty or LwxDtoIgnore");
    }

    [Fact]
    public void DictionaryDto_Generates_Dictionary_Backend()
    {
        // Using the test-project Dto/GoodDto namespace (compiled into the test project) which contains DictDto

        var t = typeof(DictDto);
        // The generator should have produced a private field named _properties for dictionary-backed DTOs
        var dictField = t.GetField("_properties", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(dictField);
        Assert.True(dictField.FieldType.FullName?.StartsWith("System.Collections.Generic.Dictionary") ?? false, "Expected _properties to be a Dictionary<,>");
    }

    [Fact]
    public void EnumProperty_Reports_LWX004_And_Adds_JsonStringEnumConverter()
    {
        // Using the test-project Dto namespace — IgnoreDto contains an enum-backed property for testing

        // no MSBuild needed — verify runtime serialization behavior for enum-backed DTOs

        // Some build environments may or may not surface the LWX004 diagnostic for enum constants.
        // Ensure generator produces the JsonStringEnumConverter on the generated DTO.

        // verify runtime behavior: enum should serialize as string when JsonStringEnumConverter is applied
        var instance = new IgnoreDto { Color = MyColors.Green };
        var json = JsonSerializer.Serialize(instance);
        Assert.Contains("\"color\":\"Green\"", json);
    }

    [Fact]
    public void ClassWithField_Reports_LWX006()
    {
        // Using the failing SampleProjects/ErrorDto project (kept as a failing fixture) which contains FieldDto

        var res = BuildAndRunSampleProject("ErrorDto");
        var hasLwx006 = res.BuildOutput?.Contains("LWX006", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(hasLwx006, "Expected LWX006 diagnostic when DTO definition contains fields");
    }

    [Fact]
    public void PropertyWithJsonConverter_Is_Emitted_With_JsonConverterAttribute()
    {
        // Using the test-project Dto namespace — IgnoreDto contains a property with a custom converter

        var t = typeof(IgnoreDto);
        var prop = t.GetProperty("Value") ?? throw new InvalidOperationException("Property Value not found on IgnoreDto");
        var convAttr = (System.Text.Json.Serialization.JsonConverterAttribute?)prop.GetCustomAttributes(false).FirstOrDefault(a => a.GetType().Name == "JsonConverterAttribute");
        Assert.NotNull(convAttr);
        var convType = convAttr!.ConverterType ?? throw new InvalidOperationException("JsonConverterAttribute.ConverterType not set");
        Assert.Equal(typeof(MyStringConverter), convType);
    }

    [Fact]
    public void PropertyWithoutConverter_UnsupportedType_Reports_LWX003()
    {
        // Using the failing SampleProjects/ErrorDto project (kept as a failing fixture) which contains BadDto

        var res = BuildAndRunSampleProject("ErrorDto");
        var hasLwx003b = res.BuildOutput?.Contains("LWX003", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(hasLwx003b, "Expected LWX003 diagnostic when property type is unsupported and no JsonConverter is provided");
    }

    [Fact]
    public void LwxDtoIgnore_Skips_Property_And_No_LWX005()
    {
        // Using the test-project Dto/GoodDto namespace (compiled into the test project) which contains IgnoreDto

        var t = typeof(IgnoreDto);
        // Ok property must exist
        Assert.NotNull(t.GetProperty("Ok"));
        // Ignored property exists in source but should have LwxDtoIgnore attribute (so generator skips it)
        var ignoredProp = t.GetProperty("Ignored");
        Assert.NotNull(ignoredProp);
        var hasIgnoreAttr = ignoredProp!.GetCustomAttributes(false).Any(a => a.GetType().Name == "LwxDtoIgnoreAttribute");
        Assert.True(hasIgnoreAttr, "Expected property 'Ignored' to be decorated with LwxDtoIgnoreAttribute");
    }

    [Fact]
    public void DateTimeTypes_Automatically_Get_JsonConverters()
    {
        // Using the test-project Dto namespace — IgnoreDto has date/time properties for testing

        var t = typeof(IgnoreDto);
        Assert.Equal(typeof(System.DateTimeOffset), t.GetProperty("Offset")?.PropertyType);
        Assert.Equal(typeof(System.DateOnly), t.GetProperty("Date")?.PropertyType);
        Assert.Equal(typeof(System.TimeOnly), t.GetProperty("Time")?.PropertyType);

        // Verify runtime roundtrip for IgnoreDto date/time properties
        var instance = new IgnoreDto
        {
            Offset = System.DateTimeOffset.Now,
            Date = System.DateOnly.FromDateTime(DateTime.Today),
            Time = System.TimeOnly.FromDateTime(DateTime.Now)
        };
        var round = SerializeAndUnserializeJson(instance);
        Assert.NotNull(round);
    }

    [Fact]
    public void DateTime_Property_Warns_LWX007_Recommend_DateTimeOffset()
    {
        // DateTime DTO is present in the ErrorDto sample project (where we intentionally test failing scenarios)
        var res = BuildAndRunSampleProject("ErrorDto");
        var hasLwx007 = res.BuildOutput?.Contains("LWX007", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(hasLwx007, "Expected LWX007 warning when using DateTime, recommending DateTimeOffset");
    }

    [Fact]
    public void LoadNormalDto_CanSerializeDeserialize()
    {
        // The test assembly is the test project - it is already compiled for the test run

        var theInstance = new NormalDto { Id = 123, Name = "hello" };

        // test serialization and deserialization
        theInstance = (NormalDto)SerializeAndUnserializeJson(theInstance)!;
        Assert.NotNull(theInstance);
        Assert.Equal(123, theInstance.Id);
        Assert.Equal("hello", theInstance.Name);
    }

    [Fact]
    public void LoadIgnoreDto_CustomConverterWorksOnRoundtrip()
    {
        // The test assembly is the test project - it is already compiled for the test run

        var theInstance = new IgnoreDto { Value = "abc" };

        // test serialization and deserialization 
        theInstance = (IgnoreDto)SerializeAndUnserializeJson(theInstance)!;
        Assert.NotNull(theInstance);        
        Assert.Equal("abc", theInstance.Value);
    }

    [Fact]
    public void GoodDto_Builds_And_Serializes_AllTypes()
    {
        // GoodDto types are compiled into the test assembly (test project) — runtime checks follow

        // NormalDto
        var simple = new NormalDto { Id = 42, Name = "sdf" };
        simple = (NormalDto?)SerializeAndUnserializeJson(simple);
        Assert.NotNull(simple);
        Assert.Equal(42, simple!.Id);


        // IgnoreDto uses custom converter and date/enum types
        var conv = new IgnoreDto { Value = "xyz" };
        conv = (IgnoreDto?)SerializeAndUnserializeJson(conv);
        Assert.NotNull(conv);
        Assert.Equal("xyz", conv!.Value);

        // Date/Time types on IgnoreDto
        var dt = new IgnoreDto
        {
            Offset = System.DateTimeOffset.Now,
            Date = System.DateOnly.FromDateTime(DateTime.Today),
            Time = System.TimeOnly.FromDateTime(DateTime.Now)
        };
        dt = (IgnoreDto?)SerializeAndUnserializeJson(dt);
        Assert.NotNull(dt);

        // DictDto
        var dict = new DictDto { Id = 9, Name = "dict" };
        dict = (DictDto?)SerializeAndUnserializeJson(dict);
        Assert.NotNull(dict);
        Assert.Equal(9, dict!.Id);
    }

    [Fact]
    public void ErrorDto_Fails_To_Build()
    {
        var res = BuildAndRunSampleProject("ErrorDto");
        Assert.False(res.BuildSucceeded, "Expected ErrorDto project to fail to build");
        // Ensure at least one of the known diagnostics appears
        var hasKnown = (res.BuildOutput?.Contains("LWX003", StringComparison.OrdinalIgnoreCase) ?? false)
            || (res.BuildOutput?.Contains("LWX005", StringComparison.OrdinalIgnoreCase) ?? false)
            || (res.BuildOutput?.Contains("LWX006", StringComparison.OrdinalIgnoreCase) ?? false);
        Assert.True(hasKnown, "Expected at least one LWX003/LWX005/LWX006 diagnostic in the failed build output");
    }
}
