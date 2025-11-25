using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Xunit;

public class UnitTest1
{
    [Fact]
    public void DtoGenerator_Generates_For_Partial_Properties()
    {
        var src = """
        using Lwx.Builders.Dto.Atributes;
        namespace TempDto;
        [LwxDto(Type = DtoType.Normal)]
        public partial class SimpleDto
        {
            [LwxDtoProperty(JsonName = "id")]
            public required partial int Id { get; set; }

            [LwxDtoProperty(JsonName = "name")]
            public partial string? Name { get; set; }
        }
        """;

        var compilation = CSharpCompilation.Create(
            "comp",
            new[] { CSharpSyntaxTree.ParseText(src) },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Lwx.Builders.Dto.DtoGenerator).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Lwx.Builders.Dto.DtoGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var driverAfter = driver.RunGenerators(compilation);
        var run = driverAfter.GetRunResult();
        Assert.Empty(run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var gen = run.Results.SelectMany(r => r.GeneratedSources).FirstOrDefault(g => g.HintName.Contains("LwxDto_SimpleDto"));
        Assert.True(gen.HintName != null && gen.HintName.Contains("LwxDto_SimpleDto"));
        var text = gen!.SourceText.ToString();
        Assert.Contains("public partial class SimpleDto", text);
        Assert.Contains("public partial int Id", text);
        Assert.Contains("public partial string? Name", text);
    }

    [Fact]
    public void DtoGenerator_Reports_LWX005_When_Missing_Property_Attribute()
    {
        var src = """
        using Lwx.Builders.Dto.Atributes;
        namespace TempDto2;
        [LwxDto(Type = DtoType.Normal)]
        public partial class BrokenDto
        {
            public partial int Bad { get; set; }
        }
        """;

        var compilation = CSharpCompilation.Create(
            "comp2",
            new[] { CSharpSyntaxTree.ParseText(src) },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Lwx.Builders.Dto.DtoGenerator).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Lwx.Builders.Dto.DtoGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var driverAfter = driver.RunGenerators(compilation);
        var run = driverAfter.GetRunResult();
        // The processor should report LWX005 for the missing property attribute
        var has = run.Results.SelectMany(r => r.Diagnostics).Any(d => d.Id == "LWX005");
        Assert.True(has, "Expected diagnostic LWX005 to be reported when DTO property is missing LwxDtoProperty or LwxDtoIgnore");
    }
}
