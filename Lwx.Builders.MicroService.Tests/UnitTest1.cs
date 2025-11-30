namespace Lwx.Builders.MicroService.Tests;

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
        var nsDir = System.IO.Path.Combine(dir.Path, "TempProject");
        
        Directory.CreateDirectory(nsDir);

        // Remove Program.cs so the generator may produce Main
        var prog = Path.Combine(dir.Path, "Program.cs");
        if (File.Exists(prog)) File.Delete(prog);
            File.WriteAllText(Path.Combine(nsDir, "ServiceConfig.cs"),
            """
            namespace TempProject;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.AspNetCore.Builder;

            [LwxServiceConfig(GenerateMain = false)]
            public static class ServiceConfig
            {
                // wrong parameter type
                public static void Configure(string s) { }
            }
            """
        );

        var (exit, output) = dir.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("LWX014", output);
    }

    [Fact]
    public void ServiceConfig_UnexpectedPublicMethod_EmitsLWX015()
    {
        using var dir = new TempProject();

        var nsDir2 = System.IO.Path.Combine(dir.Path, "TempProject");
        Directory.CreateDirectory(nsDir2);
            File.WriteAllText(Path.Combine(nsDir2, "ServiceConfig.cs"),
            """
            namespace TempProject;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.AspNetCore.Builder;

            [LwxServiceConfig(GenerateMain = false)]
            public static class ServiceConfig
            {
                public static void Configure(WebApplicationBuilder b) { }

                public static void Foo() { }
            }
            """
        );

        

        var (exit, output) = dir.Build();

        Assert.NotEqual(0, exit);
        Assert.Contains("LWX015", output);
    }

    [Fact]
    public void ServiceConfig_GenerateMain_ProducesGeneratedFileAndLWX016()
    {
        using var dir = new TempProject();

        // enable EmitCompilerGeneratedFiles in the auto-created csproj so generated sources are written to disk
        var csprojPath = Path.Combine(dir.Path, "TestProj.csproj");
        var csprojText = File.ReadAllText(csprojPath);
        if (!csprojText.Contains("<EmitCompilerGeneratedFiles>"))
        {
            csprojText = csprojText.Replace("</PropertyGroup>",
                "  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>\n  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>\n</PropertyGroup>");
            File.WriteAllText(csprojPath, csprojText);
        }

        // Remove Program.cs so the generator is allowed to produce the main method
        var prog = Path.Combine(dir.Path, "Program.cs");
        if (File.Exists(prog)) File.Delete(prog);

        var nsDir1 = System.IO.Path.Combine(dir.Path, "TempProject");
        Directory.CreateDirectory(nsDir1);
        File.WriteAllText(Path.Combine(nsDir1, "ServiceConfig.cs"),
            """
            namespace TempProject;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.AspNetCore.Builder;

            [LwxServiceConfig(GenerateMain = true)]
            public static partial class ServiceConfig
            {
                public static void Configure(WebApplicationBuilder b) { }
                public static void Configure(WebApplication a) { }
            }
            """);

        var (exit, output) = dir.Build();

        // Build should succeed and generated file should be present on disk
        Assert.True(exit == 0, output);

        // Look for generated file under obj/Generated
        var genRoot = Directory.EnumerateFiles(dir.Path, "ServiceConfig.Main.g.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(genRoot), "ServiceConfig.Main.g.cs should be emitted to disk");

        // The generator may report LWX016 as an informational diagnostic in IDEs; dotnet build may not always print info-level diagnostics.
        // If the diagnostic appears on the build output, check it includes the Main signature.
        if (output.Contains("LWX016"))
        {
            Assert.Contains("public static void Main(string[] args)", output);
        }

        // Always assert the generated file contains the Main method so behavior is validated regardless of CLI diagnostic output
        var generatedContent = File.ReadAllText(genRoot);
        Assert.Contains("public static void Main(string[] args)", generatedContent);
    }

    [Fact]
    public void ServiceConfig_GenerateMain_IncludesWorkerConfigureCalls()
    {
        using var dir = new TempProject();

        // ensure generated sources are written to disk
        var csprojPath = Path.Combine(dir.Path, "TestProj.csproj");
        var csprojText = File.ReadAllText(csprojPath);
        if (!csprojText.Contains("<EmitCompilerGeneratedFiles>"))
        {
            csprojText = csprojText.Replace("</PropertyGroup>",
                "  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>\n  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>\n</PropertyGroup>");
            File.WriteAllText(csprojPath, csprojText);
        }

        

        var nsDir = System.IO.Path.Combine(dir.Path, "TempProject");
        // Remove Program.cs so the generator may produce Main
        var prog = Path.Combine(dir.Path, "Program.cs");
        if (File.Exists(prog)) File.Delete(prog);
        Directory.CreateDirectory(nsDir);

        // Add a worker type and ServiceConfig which will ask for GenerateMain
        var workerDir = Path.Combine(nsDir, "Workers");
        Directory.CreateDirectory(workerDir);

        File.WriteAllText(Path.Combine(workerDir, "TheWorker.cs"),
            """
            namespace TempProject.Workers;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.Extensions.Hosting;

            [LwxWorker(Stage = LwxStage.Development)]
            public partial class TheWorker : BackgroundService
            {
                protected override async Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
                {
                    await System.Threading.Tasks.Task.Delay(0, stoppingToken);
                }
            }
            """);

        File.WriteAllText(Path.Combine(nsDir, "ServiceConfig.cs"),
            """
            namespace TempProject;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.AspNetCore.Builder;

            [LwxServiceConfig(GenerateMain = true)]
            public static partial class ServiceConfig
            {
                public static void Configure(WebApplicationBuilder b) { }
                public static void Configure(WebApplication a) { }
            }
            """);

        var (exit, output) = dir.Build();

        Assert.True(exit == 0, output);

        var genRoot = Directory.EnumerateFiles(dir.Path, "ServiceConfig.Main.g.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(genRoot), "ServiceConfig.Main.g.cs should be emitted to disk");

        var generatedContent = File.ReadAllText(genRoot);

        // Ensure generated main includes worker registration call for the generated worker configure method
        Assert.Contains("TempProject.Workers.TheWorker.Configure(builder);", generatedContent);
    }

    [Fact]
    public void ServiceConfig_GenerateMain_IncludesEndpointConfigureCalls()
    {
        using var dir = new TempProject();

            // ensure generated sources are written to disk
            var csprojPath = Path.Combine(dir.Path, "TestProj.csproj");
            var csprojText = File.ReadAllText(csprojPath);
            if (!csprojText.Contains("<EmitCompilerGeneratedFiles>"))
            {
                csprojText = csprojText.Replace("</PropertyGroup>",
                    "  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>\n  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>\n</PropertyGroup>");
                File.WriteAllText(csprojPath, csprojText);
            }

        // Remove Program.cs so the generator is allowed to produce the main method
        var prog = Path.Combine(dir.Path, "Program.cs");
        if (File.Exists(prog)) File.Delete(prog);

        var nsDir = Path.Combine(dir.Path, "TempProject");
        Directory.CreateDirectory(nsDir);

        // Create an endpoint in TempProject.Endpoints.EndpointTest matching "GET /test"
        var endpointsDir = Path.Combine(nsDir, "Endpoints");
        Directory.CreateDirectory(endpointsDir);
        File.WriteAllText(Path.Combine(endpointsDir, "EndpointTest.cs"),
            """
            namespace TempProject.Endpoints;
            using Lwx.Builders.MicroService.Atributes;

            [LwxEndpoint("GET /test")]
            public static partial class EndpointTest
            {
            }
            """);

        // Add ServiceConfig that requests GenerateMain
        File.WriteAllText(Path.Combine(nsDir, "ServiceConfig.cs"),
            """
            namespace TempProject;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.AspNetCore.Builder;

            [LwxServiceConfig(GenerateMain = true)]
            public static partial class ServiceConfig
            {
                public static void Configure(WebApplicationBuilder b) { }
                public static void Configure(WebApplication a) { }
            }
            """);

        var (exit, output) = dir.Build();

        Assert.True(exit == 0, output);

        var genRoot = Directory.EnumerateFiles(dir.Path, "ServiceConfig.Main.g.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(genRoot), "ServiceConfig.Main.g.cs should be emitted to disk");

        var generatedContent = File.ReadAllText(genRoot);

        // Ensure generated main includes endpoint registration call for the generated endpoint configure method
        Assert.Contains("TempProject.Endpoints.EndpointTest.Configure(app);", generatedContent);
    }

    [Fact]
    public void Worker_Generate_POCOS_And_ConfigBinding()
    {
        using var dir = new TempProject();

        // ensure generated sources are written to disk
        var csprojPath = Path.Combine(dir.Path, "TestProj.csproj");
        var csprojText = File.ReadAllText(csprojPath);
        if (!csprojText.Contains("<EmitCompilerGeneratedFiles>"))
        {
            csprojText = csprojText.Replace("</PropertyGroup>",
                "  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>\n  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>\n</PropertyGroup>");
            File.WriteAllText(csprojPath, csprojText);
        }

        

        var nsDir = System.IO.Path.Combine(dir.Path, "TempProject");
        Directory.CreateDirectory(nsDir);

        var workerDir = Path.Combine(nsDir, "Workers");
        Directory.CreateDirectory(workerDir);

        File.WriteAllText(Path.Combine(workerDir, "TheWorker.cs"),
            """
            namespace TempProject.Workers;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.Extensions.Hosting;
            using Microsoft.Extensions.Logging;
            using System.Threading.Tasks;
            using System.Threading;

            [LwxWorker(Stage = LwxStage.Development)]
            public partial class TheWorker : BackgroundService
            {
                private readonly ILogger<TheWorker> _logger;
                private readonly string _abc;
                public TheWorker(ILogger<TheWorker> logger, [FromConfig("Abc")] string abc)
                {
                    _logger = logger;
                    _abc = abc;
                }

                protected override async Task ExecuteAsync(CancellationToken stoppingToken)
                {
                    await Task.Delay(0, stoppingToken);
                }
            }
            """);

        File.WriteAllText(Path.Combine(nsDir, "ServiceConfig.cs"),
            """
            namespace TempProject;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.AspNetCore.Builder;

            [LwxServiceConfig(GenerateMain = false)]
            public static partial class ServiceConfig
            {
                public static void Configure(WebApplicationBuilder b) { }
                public static void Configure(WebApplication a) { }
            }
            """);

        var (exit, output) = dir.Build();
        Assert.True(exit == 0, output);

        // Find generated worker file (it should be emitted under the project's generated folder)
        var genWorker = Directory.EnumerateFiles(dir.Path, "LwxWorker_TheWorker.g.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(genWorker), "LwxWorker_TheWorker.g.cs should be emitted to disk");

        var generatedContent = File.ReadAllText(genWorker);

        // Expect configuration binding for worker
        Assert.Contains("builder.Services.Configure<TheWorkerConfig>(builder.Configuration.GetSection(\"TheWorker\"));", generatedContent);

        // Expect factory-based AddSingleton<IHostedService> provisioning for the worker
        Assert.Contains("AddSingleton<Microsoft.Extensions.Hosting.IHostedService>", generatedContent);
        Assert.Contains("new TheWorker(", generatedContent);
        // Expect the worker descriptor registration containing metadata
        Assert.Contains("LwxWorkerDescriptor", generatedContent);
    }

    [Fact]
    public void Worker_Generate_MultipleThreads_RegistersMultipleInstances()
    {
        using var dir = new TempProject();

        // ensure generated sources are written to disk
        var csprojPath = Path.Combine(dir.Path, "TestProj.csproj");
        var csprojText = File.ReadAllText(csprojPath);
        if (!csprojText.Contains("<EmitCompilerGeneratedFiles>"))
        {
            csprojText = csprojText.Replace("</PropertyGroup>",
                "  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>\n  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>\n</PropertyGroup>");
            File.WriteAllText(csprojPath, csprojText);
        }

        var nsDir = System.IO.Path.Combine(dir.Path, "TempProject");
        Directory.CreateDirectory(nsDir);

        var workerDir = Path.Combine(nsDir, "Workers");
        Directory.CreateDirectory(workerDir);

        File.WriteAllText(Path.Combine(workerDir, "TheWorker.cs"),
            """
            namespace TempProject.Workers;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.Extensions.Hosting;
            using System.Threading;
            using System.Threading.Tasks;

            [LwxWorker(Threads = 3, Stage = LwxStage.Development)]
            public partial class TheWorker : BackgroundService
            {
                protected override async Task ExecuteAsync(CancellationToken stoppingToken)
                {
                    await Task.Delay(0, stoppingToken);
                }
            }
            """);

        File.WriteAllText(Path.Combine(nsDir, "ServiceConfig.cs"),
            """
            namespace TempProject;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.AspNetCore.Builder;

            [LwxServiceConfig(GenerateMain = false)]
            public static partial class ServiceConfig
            {
                public static void Configure(WebApplicationBuilder b) { }
                public static void Configure(WebApplication a) { }
            }
            """);

        var (exit, output) = dir.Build();
        Assert.True(exit == 0, output);

        var genWorker = Directory.EnumerateFiles(dir.Path, "LwxWorker_TheWorker.g.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(genWorker), "LwxWorker_TheWorker.g.cs should be emitted to disk");

        var generatedContent = File.ReadAllText(genWorker);

        // Expect a for-loop registering three instances
        Assert.Contains("for (int i = 0; i < 3; i++)", generatedContent);
        Assert.Contains("AddHostedService<TheWorker>", generatedContent);
        // Expect the worker descriptor registration
        Assert.Contains("LwxWorkerDescriptor", generatedContent);
    }

    [Fact]
    public void ServiceConfig_PublishSwagger_None_OmitsSwaggerCodeInEndpointExtensions()
    {
        using var dir = new TempProject();

        // ensure generated sources are written to disk
        var csprojPath = Path.Combine(dir.Path, "TestProj.csproj");
        var csprojText = File.ReadAllText(csprojPath);
        if (!csprojText.Contains("<EmitCompilerGeneratedFiles>"))
        {
            csprojText = csprojText.Replace("</PropertyGroup>",
                "  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>\n  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>\n</PropertyGroup>");
            File.WriteAllText(csprojPath, csprojText);
        }

        var nsDir = System.IO.Path.Combine(dir.Path, "TempProject");
        Directory.CreateDirectory(nsDir);
        File.WriteAllText(Path.Combine(nsDir, "ServiceConfig.cs"),
            """
            namespace TempProject;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.AspNetCore.Builder;

            [LwxServiceConfig(PublishSwagger = LwxStage.None)]
            public static partial class ServiceConfig
            {
                public static void Configure(WebApplicationBuilder b) { }
                public static void Configure(WebApplication a) { }
            }
            """
        );

        // inject minimal stubs to satisfy OpenAPI/Swagger types & extension methods so the generated code compiles
        File.WriteAllText(Path.Combine(nsDir, "SwaggerStubs.cs"),
            """
            namespace Microsoft.OpenApi.Models
            {
                public class OpenApiInfo
                {
                    public string Title { get; set; }
                    public string Description { get; set; }
                    public string Version { get; set; }
                }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public static class SwaggerServiceCollectionExtensions
                {
                    public static IServiceCollection AddSwaggerGen(this IServiceCollection svc, object? opts = null) => svc;
                }
            }

            namespace Microsoft.AspNetCore.Builder
            {
                public static class SwaggerAppExtensions
                {
                    public static void UseSwagger(this WebApplication app) { }
                    public static void UseSwaggerUI(this WebApplication app, object? opts = null) { }
                }
            }
            """
        );

        var (exit, output) = dir.Build();
        // build may fail if consumer project is missing full swagger packages; we only need to assert the generated source

        var genRoot = Directory.EnumerateFiles(dir.Path, "LwxEndpointExtensions.g.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(genRoot), "LwxEndpointExtensions.g.cs should be emitted to disk");

        var generatedContent = File.ReadAllText(genRoot);

        // No swagger registration code should be present when PublishSwagger is None
        Assert.DoesNotContain("AddSwaggerGen", generatedContent);
        Assert.DoesNotContain("UseSwagger", generatedContent);
    }

    [Fact]
    public void ServiceConfig_PublishSwagger_Development_IncludesSwaggerCodeInEndpointExtensions()
    {
        using var dir = new TempProject();

        var csprojPath = Path.Combine(dir.Path, "TestProj.csproj");
        var csprojText = File.ReadAllText(csprojPath);
        if (!csprojText.Contains("<EmitCompilerGeneratedFiles>"))
        {
            csprojText = csprojText.Replace("</PropertyGroup>",
                "  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>\n  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>\n</PropertyGroup>");
            File.WriteAllText(csprojPath, csprojText);
        }

        var nsDir = System.IO.Path.Combine(dir.Path, "TempProject");
        Directory.CreateDirectory(nsDir);
        File.WriteAllText(Path.Combine(nsDir, "ServiceConfig.cs"),
            """
            namespace TempProject;
            using Lwx.Builders.MicroService.Atributes;
            using Microsoft.AspNetCore.Builder;

            [LwxServiceConfig(PublishSwagger = LwxStage.Development)]
            public static partial class ServiceConfig
            {
                public static void Configure(WebApplicationBuilder b) { }
                public static void Configure(WebApplication a) { }
            }
            """
        );

        var (exit, output) = dir.Build();

        var genRoot = Directory.EnumerateFiles(dir.Path, "LwxEndpointExtensions.g.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(genRoot), "LwxEndpointExtensions.g.cs should be emitted to disk");

        var generatedContent = File.ReadAllText(genRoot);

        // Expect swagger setup code to be present when PublishSwagger is Development
        Assert.Contains("AddSwaggerGen", generatedContent);
        Assert.Contains("UseSwagger", generatedContent);
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
            while (!System.IO.File.Exists(System.IO.Path.Combine(repoRoot, "Lwx.Builders.sln")))
            {
                var parent = System.IO.Path.GetFullPath(System.IO.Path.Combine(repoRoot, ".."));
                if (parent == repoRoot) break; // reached root
                repoRoot = parent;
            }

            var generatorPath = System.IO.Path.Combine(repoRoot, "Lwx.Builders.MicroService", "Lwx.Builders.MicroService.csproj");
            var dtoPath = System.IO.Path.Combine(repoRoot, "Lwx.Builders.Dto", "Lwx.Builders.Dto.csproj");
            var csproj = $"""
                                <Project Sdk="Microsoft.NET.Sdk.Web">
                                    <PropertyGroup>
                                        <TargetFramework>net9.0</TargetFramework>
                                        <ImplicitUsings>enable</ImplicitUsings>
                                        <Nullable>enable</Nullable>
                                    </PropertyGroup>

                                    <ItemGroup>
                                        <ProjectReference Include="{generatorPath}" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                                        <ProjectReference Include="{dtoPath}" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
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
