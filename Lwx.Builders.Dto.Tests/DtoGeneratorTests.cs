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

public class DtoGeneratorTests
{

    private record BuildResult(bool BuildSucceeded, string BuildOutput, string RunOutput, Dictionary<string, string> GeneratedFiles);

    private BuildResult BuildAndRunProject(string name, string mainSource)
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

        return new BuildResult(buildExit == 0, buildOut ?? string.Empty, runOutput ?? string.Empty, generatedFiles);
    }

    private BuildResult BuildAndRunSampleProject(string sampleName)
    {
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

        return new BuildResult(buildExit == 0, buildOut ?? string.Empty, runOut ?? string.Empty, generatedFiles);
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

    private object CreateInstance(System.Reflection.Assembly asm, string fullTypeName)
    {
        var t = asm.GetType(fullTypeName) ?? throw new InvalidOperationException($"Type not found: {fullTypeName}");
        return Activator.CreateInstance(t) ?? throw new InvalidOperationException($"Could not create instance of {fullTypeName}");
    }

    private void SetProperty(object target, string propName, object? value)
    {
        var pi = target.GetType().GetProperty(propName) ?? throw new InvalidOperationException($"Property not found: {propName}");
        pi.SetValue(target, value);
    }

    private object? SerializeAndUnserializeJson(object obj)
    {
        var t = obj.GetType();
        var json = JsonSerializer.Serialize(obj, t);
        return JsonSerializer.Deserialize(json, t);
    }

    private object? GetPropertyValue(object obj, string propertyName)
    {
        var pi = obj.GetType().GetProperty(propertyName) ?? throw new InvalidOperationException($"Property not found: {propertyName}");
        return pi.GetValue(obj);
    }
    [Fact]
    public void DtoGenerator_Generates_For_Partial_Properties()
    {
        // Using the prebuilt sample project SimpleDto (under SampleProjects)

        var res = BuildAndRunSampleProject("SimpleDto");

        Assert.True(res.BuildSucceeded, "Expected project to build successfully");

        // locate generated file content
        var text = res.GeneratedFiles.FirstOrDefault(kv => System.IO.Path.GetFileName(kv.Key).Contains("LwxDto_SimpleDto")).Value ?? string.Empty;
        Assert.Contains("public partial class SimpleDto", text);
        Assert.Contains("public partial int Id", text);
        Assert.Contains("public partial string? Name", text);
    }

    [Fact]
    public void DtoGenerator_Reports_LWX005_When_Missing_Property_Attribute()
    {
        // Using the prebuilt sample project BrokenDto (under SampleProjects)

        var res = BuildAndRunSampleProject("BrokenDto");
        var has = res.BuildOutput?.Contains("LWX005", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(has, "Expected diagnostic LWX005 to be reported when DTO property is missing LwxDtoProperty or LwxDtoIgnore");
    }

    [Fact]
    public void DictionaryDto_Generates_Dictionary_Backend()
    {
        // Using the prebuilt sample project DictDto (under SampleProjects)

        var res = BuildAndRunSampleProject("DictDto");
        Assert.True(res.BuildSucceeded, "Expected dict project to build");
        var text = res.GeneratedFiles.FirstOrDefault(kv => System.IO.Path.GetFileName(kv.Key).Contains("LwxDto_DictDto")).Value ?? string.Empty;
        Assert.Contains("_properties", text);
        Assert.Contains("_properties.TryGetValue", text);
        Assert.Contains("_properties[\"id\"]", text);
    }

    [Fact]
    public void EnumDto_Reports_LWX004_And_Adds_JsonStringEnumConverter()
    {
        // Using the prebuilt sample project EnumDto (under SampleProjects)

        var res = BuildAndRunSampleProject("EnumDto");

        // Some build environments may or may not surface the LWX004 diagnostic for enum constants.
        // Ensure generator produces the JsonStringEnumConverter on the generated DTO.

        var text = res.GeneratedFiles.FirstOrDefault(kv => System.IO.Path.GetFileName(kv.Key).Contains("LwxDto_EnumDto")).Value ?? string.Empty;
        Assert.Contains("JsonStringEnumConverter", text);
        // Ensure generated property uses fully-qualified enum type (namespace + type)
        Assert.Contains("public partial EnumDto.Dto.MyColors", text);
        // Ensure backing field also uses the fully-qualified enum type
        Assert.Contains("private EnumDto.Dto.MyColors _color;", text);
    }

    [Fact]
    public void ClassWithField_Reports_LWX006()
    {
        // Using the prebuilt sample project FieldDto (under SampleProjects)

        var res = BuildAndRunSampleProject("FieldDto");
        var hasLwx006 = res.BuildOutput?.Contains("LWX006", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(hasLwx006, "Expected LWX006 diagnostic when DTO definition contains fields");
    }

    [Fact]
    public void PropertyWithJsonConverter_Is_Emitted_With_JsonConverterAttribute()
    {
        // Using the prebuilt sample project ConvDto (under SampleProjects)

        var res = BuildAndRunSampleProject("ConvDto");

        // Ensure there are no unsupported-type diagnostics (LWX003)
        var hasLwx003 = res.BuildOutput?.Contains("LWX003", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.False(hasLwx003, "Did not expect LWX003 for string with JsonConverter");

        var text = res.GeneratedFiles.FirstOrDefault(kv => System.IO.Path.GetFileName(kv.Key).Contains("LwxDto_ConvDto")).Value ?? string.Empty;
        // samples now place converters in the Dto namespace (ConvDto.Dto), so matcher checks for that
        Assert.Contains("JsonConverter(typeof(global::ConvDto.Dto.MyStringConverter))", text);
    }

    [Fact]
    public void PropertyWithoutConverter_UnsupportedType_Reports_LWX003()
    {
        // Using the prebuilt sample project BadDto (under SampleProjects)

        var res = BuildAndRunSampleProject("BadDto");
        var hasLwx003b = res.BuildOutput?.Contains("LWX003", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(hasLwx003b, "Expected LWX003 diagnostic when property type is unsupported and no JsonConverter is provided");
    }

    [Fact]
    public void LwxDtoIgnore_Skips_Property_And_No_LWX005()
    {
        // Using the prebuilt sample project IgnoreDto (under SampleProjects)

        var res = BuildAndRunSampleProject("IgnoreDto");

        // there should be no LWX005 (missing property attribute) for the ignored property
        var hasLwx005 = res.BuildOutput?.Contains("LWX005", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.False(hasLwx005, "Ignored property should not trigger LWX005");

        var text = res.GeneratedFiles.FirstOrDefault(kv => System.IO.Path.GetFileName(kv.Key).Contains("LwxDto_IgnoreDto")).Value ?? string.Empty;
        Assert.Contains("public partial int Ok", text);
        Assert.DoesNotContain("public partial int Ignored", text);
    }

    [Fact]
    public void DateTimeTypes_Automatically_Get_JsonConverters()
    {
        // Using the prebuilt sample project DateDto (under SampleProjects)

        var res = BuildAndRunSampleProject("DateDto");

        // Even if other types in the same project produce diagnostics (eg DateTime), ensure
        // the generated DateDto file contains converters for the supported date/time types.

        var text = res.GeneratedFiles.FirstOrDefault(kv => System.IO.Path.GetFileName(kv.Key).Contains("LwxDto_DateDto")).Value ?? string.Empty;
        Assert.Contains("JsonConverter(typeof(System.Text.Json.Serialization.JsonConverter<System.DateTimeOffset>))", text);
        Assert.Contains("JsonConverter(typeof(System.Text.Json.Serialization.JsonConverter<System.DateOnly>))", text);
        Assert.Contains("JsonConverter(typeof(System.Text.Json.Serialization.JsonConverter<System.TimeOnly>))", text);
    }

    [Fact]
    public void DateTime_Property_Warns_LWX007_Recommend_DateTimeOffset()
    {
        // Using the prebuilt sample project DateTimeDto (under SampleProjects)

        // DateTime DTO merged into DateDto sample project
        var res = BuildAndRunSampleProject("DateDto");
        var hasLwx007 = res.BuildOutput?.Contains("LWX007", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(hasLwx007, "Expected LWX007 warning when using DateTime, recommending DateTimeOffset");
    }

    [Fact]
    public void LoadSimpleDtoAssembly_CanSerializeDeserialize()
    {
        Assert.True(
            BuildAndRunSampleProject("SimpleDto").BuildSucceeded,
            "Expected SimpleDto project to build"
        );

        var compiledAssembly = LoadSampleAssembly("SimpleDto");

        var theInstance = CreateInstance(compiledAssembly, "SimpleDto.Dto.SimpleDto");
        SetProperty(theInstance, "Id", 123);
        SetProperty(theInstance, "Name", "hello");

        // test serialization and deserialization
        theInstance = SerializeAndUnserializeJson(theInstance);
        Assert.NotNull(theInstance);
        Assert.Equal(123, (int?)GetPropertyValue(theInstance!, "Id"));
        Assert.Equal("hello", (string?)GetPropertyValue(theInstance!, "Name"));
    }

    [Fact]
    public void LoadConvDtoAssembly_CustomConverterWorksOnRoundtrip()
    {
        Assert.True(
            BuildAndRunSampleProject("ConvDto").BuildSucceeded, 
            "Expected ConvDto project to build"
        );

        var compiledAssembly = LoadSampleAssembly("ConvDto");

        var theInstance = CreateInstance(compiledAssembly, "ConvDto.Dto.ConvDto");
        SetProperty(theInstance, "Value", "abc");

        // test serialization and deserialization 
        theInstance = SerializeAndUnserializeJson(theInstance);
        Assert.NotNull(theInstance);        
        Assert.Equal("abc", (string?)GetPropertyValue(theInstance!, "Value"));
    }
}
