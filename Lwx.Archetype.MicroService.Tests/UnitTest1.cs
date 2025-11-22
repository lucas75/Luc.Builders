namespace Lwx.Archetype.MicroService.Tests;

using System.Diagnostics;
using System.IO;
using Xunit;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {

    }

    [Fact]
    public void ServiceConfig_InvalidConfigureSignature_EmitsLWX014()
    {
        using var dir = new TempProject();

        // Create a ServiceConfig with invalid Configure signature
        File.WriteAllText(Path.Combine(dir.Path, "ServiceConfig.cs"), @"namespace TempProject;
using Lwx.Archetype.MicroService.Atributes;
using Microsoft.AspNetCore.Builder;

[LwxServiceConfig(GenerateMain = false)]
public static class ServiceConfig
{
    // wrong parameter type
    public static void Configure(string s) { }
}
");

        var (exit, output) = dir.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("LWX014", output);
    }

    [Fact]
    public void ServiceConfig_UnexpectedPublicMethod_EmitsLWX015()
    {
        using var dir = new TempProject();

        File.WriteAllText(Path.Combine(dir.Path, "ServiceConfig.cs"), @"namespace TempProject;
using Lwx.Archetype.MicroService.Atributes;
using Microsoft.AspNetCore.Builder;

[LwxServiceConfig(GenerateMain = false)]
public static class ServiceConfig
{
    public static void Configure(WebApplicationBuilder b) { }

    public static void Foo() { }
}
");

        var (exit, output) = dir.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("LWX015", output);
    }

    private sealed class TempProject : IDisposable
    {
        public string Path { get; }

        public TempProject()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
            Directory.CreateDirectory(Path);

            // create minimal web project which references the generator as an analyzer
                        var asmDir = System.IO.Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location)!;
                        var repoRoot = asmDir;
                        // walk up until we find the repository solution file
                        while (!System.IO.File.Exists(System.IO.Path.Combine(repoRoot, "Luc.Util.Web.sln")))
                        {
                            var parent = System.IO.Path.GetFullPath(System.IO.Path.Combine(repoRoot, ".."));
                            if (parent == repoRoot) break; // reached root
                            repoRoot = parent;
                        }

                        var generatorPath = System.IO.Path.Combine(repoRoot, "Lwx.Archetype.MicroService", "Lwx.Archetype.MicroService.csproj");
                        var csproj = $"""
                                <Project Sdk="Microsoft.NET.Sdk.Web">
                                    <PropertyGroup>
                                        <TargetFramework>net9.0</TargetFramework>
                                        <ImplicitUsings>enable</ImplicitUsings>
                                        <Nullable>enable</Nullable>
                                    </PropertyGroup>

                                    <ItemGroup>
                                        <ProjectReference Include="{generatorPath}" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                                    </ItemGroup>
                                </Project>
                                """;

            File.WriteAllText(System.IO.Path.Combine(Path, "TestProj.csproj"), csproj);
            // make a minimal Program.cs so build can proceed until generator emits diagnostic
            File.WriteAllText(System.IO.Path.Combine(Path, "Program.cs"), "var x = 1; Console.WriteLine(x);");
        }

        public (int exitCode, string output) Build()
        {
            var psi = new ProcessStartInfo("dotnet", "build --nologo")
            {
                WorkingDirectory = Path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var p = Process.Start(psi) ?? throw new InvalidOperationException("failed to start dotnet");
            var outp = p.StandardOutput!.ReadToEnd() + p.StandardError!.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, outp);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}
