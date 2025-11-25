using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

public class UnitTest1
{
    [Fact]
    public void DtoAttribute_Embedding_GeneratesAttributes()
    {
        using var dir = new TempProject();

        // Create a DTO class using the embedded attributes (generator should add attributes as sources)
        var nsDir = Path.Combine(dir.Path, "TempDto");
        Directory.CreateDirectory(nsDir);
        File.WriteAllText(Path.Combine(nsDir, "SimpleDto.cs"),
            """
            namespace TempDto;
            using Lwx.Builders.Dto.Atributes;

            [LwxDto(Type = DtoType.Normal)]
            public partial class SimpleDto
            {
                [LwxDtoProperty(JsonName = "id")]
                public required int Id { get; set; }

                [LwxDtoProperty(JsonName = "name")]
                public string? Name { get; set; }
            }
            """
        );

        var (exit, output) = dir.Build();
        Assert.Equal(0, exit);

        var genRoot = Directory.EnumerateFiles(dir.Path, "LwxDto_SimpleDto.g.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(genRoot), "LwxDto_SimpleDto.g.cs should be emitted to disk");

        var generatedContent = File.ReadAllText(genRoot);
        Assert.Contains("public partial class SimpleDto", generatedContent);
        Assert.Contains("public partial int Id", generatedContent);
        Assert.Contains("public partial string? Name", generatedContent);
    }

    [Fact]
    public void DtoProperty_MissingAttribute_EmitsLWX005()
    {
        using var dir = new TempProject();

        var nsDir = Path.Combine(dir.Path, "TempDto2");
        Directory.CreateDirectory(nsDir);
        File.WriteAllText(Path.Combine(nsDir, "BrokenDto.cs"),
            """
            namespace TempDto2;
            using Lwx.Builders.Dto.Atributes;

            [LwxDto(Type = DtoType.Normal)]
            public partial class BrokenDto
            {
                // missing LwxDtoProperty or LwxDtoIgnore
                public int Bad { get; set; }
            }
            """
        );

        var (exit, output) = dir.Build();
        Assert.NotEqual(0, exit);
        Assert.Contains("LWX005", output);
    }

    private sealed class TempProject : IDisposable
    {
        public string Path { get; }

        public TempProject()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
            Directory.CreateDirectory(Path);

            // create minimal project which references the dto generator as an analyzer
            var repoRoot = System.IO.Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location)!;
            while (!System.IO.File.Exists(System.IO.Path.Combine(repoRoot, "Luc.Util.Web.sln")))
            {
                var parent = System.IO.Path.GetFullPath(System.IO.Path.Combine(repoRoot, ".."));
                if (parent == repoRoot) break;
                repoRoot = parent;
            }

            var dtoPath = System.IO.Path.Combine(repoRoot, "Lwx.Builders.Dto", "Lwx.Builders.Dto.csproj");
            var csproj = $"""
                            <Project Sdk=\"Microsoft.NET.Sdk\">
                                <PropertyGroup>
                                    <TargetFramework>net9.0</TargetFramework>
                                    <ImplicitUsings>enable</ImplicitUsings>
                                    <Nullable>enable</Nullable>
                                </PropertyGroup>

                                <ItemGroup>
                                    <ProjectReference Include=\"{dtoPath}\" OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\" />
                                </ItemGroup>
                            </Project>
                            """;

            File.WriteAllText(System.IO.Path.Combine(Path, "TestProj.csproj"), csproj);
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
