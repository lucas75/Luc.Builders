using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Xunit;

public class DtoGeneratorTests
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

    [Fact]
    public void DictionaryDto_Generates_Dictionary_Backend()
    {
        var src = """
        using Lwx.Builders.Dto.Atributes;
        namespace TempDto;
        [LwxDto(Type = DtoType.Dictionary)]
        public partial class DictDto
        {
            [LwxDtoProperty(JsonName = "id")]
            public required partial int Id { get; set; }

            [LwxDtoProperty(JsonName = "name")]
            public partial string? Name { get; set; }
        }
        """;

        var compilation = CSharpCompilation.Create(
            "compDict",
            new[] { CSharpSyntaxTree.ParseText(src) },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Lwx.Builders.Dto.DtoGenerator).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Lwx.Builders.Dto.DtoGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var after = driver.RunGenerators(compilation);
        var run = after.GetRunResult();

        var gen = run.Results.SelectMany(r => r.GeneratedSources).FirstOrDefault(g => g.HintName.Contains("LwxDto_DictDto"));
        Assert.True(gen.HintName != null && gen.HintName.Contains("LwxDto_DictDto"));
        var text = gen!.SourceText.ToString();
        Assert.Contains("_properties", text);
        Assert.Contains("_properties.TryGetValue", text);
        Assert.Contains("_properties[\"id\"]", text);
    }

    [Fact]
    public void EnumDto_Reports_LWX004_And_Adds_JsonStringEnumConverter()
    {
        var src = """
        using Lwx.Builders.Dto.Atributes;
        namespace TempDtoEnum;

        public enum MyColors { Red = 0, Green = 1 }

        [LwxDto(Type = DtoType.Normal)]
        public partial class EnumDto
        {
            [LwxDtoProperty(JsonName = "color")]
            public partial MyColors Color { get; set; }
        }
        """;

        var compilation = CSharpCompilation.Create(
            "compEnum",
            new[] { CSharpSyntaxTree.ParseText(src) },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Lwx.Builders.Dto.DtoGenerator).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Lwx.Builders.Dto.DtoGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var after = driver.RunGenerators(compilation);
        var run = after.GetRunResult();

        // should warn about missing JsonPropertyName on enum constants
        var hasLwx004 = run.Results.SelectMany(r => r.Diagnostics).Any(d => d.Id == "LWX004");
        Assert.True(hasLwx004, "Expected LWX004 warning for enum constants missing JsonPropertyName");

        var gen = run.Results.SelectMany(r => r.GeneratedSources).FirstOrDefault(g => g.HintName.Contains("LwxDto_EnumDto"));
        Assert.True(gen.HintName != null && gen.HintName.Contains("LwxDto_EnumDto"));
        var text = gen!.SourceText.ToString();
        Assert.Contains("JsonStringEnumConverter", text);
    }

    [Fact]
    public void ClassWithField_Reports_LWX006()
    {
        var src = """
        using Lwx.Builders.Dto.Atributes;
        namespace TempDtoField;

        [LwxDto(Type = DtoType.Normal)]
        public partial class FieldDto
        {
            public int NotAllowedField;

            [LwxDtoProperty(JsonName = "id")]
            public partial int Id { get; set; }
        }
        """;

        var compilation = CSharpCompilation.Create(
            "compField",
            new[] { CSharpSyntaxTree.ParseText(src) },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Lwx.Builders.Dto.DtoGenerator).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Lwx.Builders.Dto.DtoGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var after = driver.RunGenerators(compilation);
        var run = after.GetRunResult();

        var hasLwx006 = run.Results.SelectMany(r => r.Diagnostics).Any(d => d.Id == "LWX006");
        Assert.True(hasLwx006, "Expected LWX006 diagnostic when DTO definition contains fields");
    }

    [Fact]
    public void PropertyWithJsonConverter_Is_Emitted_With_JsonConverterAttribute()
    {
        var src = """
        using Lwx.Builders.Dto.Atributes;
        namespace TempDtoConv;

        using System.Text.Json;
        using System.Text.Json.Serialization;

        public sealed class MyStringConverter : JsonConverter<string>
        {
            public override string Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) => reader.GetString() ?? string.Empty;
            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
        }

        [LwxDto(Type = DtoType.Normal)]
        public partial class ConvDto
        {
            [LwxDtoProperty(JsonName = "val", JsonConverter = typeof(MyStringConverter))]
            public partial string? Value { get; set; }
        }
        """;

        var compilation = CSharpCompilation.Create(
            "compConv",
            new[] { CSharpSyntaxTree.ParseText(src) },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Lwx.Builders.Dto.DtoGenerator).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Lwx.Builders.Dto.DtoGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var after = driver.RunGenerators(compilation);
        var run = after.GetRunResult();

        // Ensure there are no unsupported-type diagnostics (LWX003)
        var hasLwx003 = run.Results.SelectMany(r => r.Diagnostics).Any(d => d.Id == "LWX003");
        Assert.False(hasLwx003, "Did not expect LWX003 for string with JsonConverter");

        var gen = run.Results.SelectMany(r => r.GeneratedSources).FirstOrDefault(g => g.HintName.Contains("LwxDto_ConvDto"));
        Assert.True(gen.HintName != null && gen.HintName.Contains("LwxDto_ConvDto"));
        var text = gen!.SourceText.ToString();

        // generator should include JsonConverter attribute referencing the converter type
        Assert.Contains("JsonConverter(typeof(TempDtoConv.MyStringConverter))", text);
    }

    [Fact]
    public void PropertyWithoutConverter_UnsupportedType_Reports_LWX003()
    {
        var src = """
        using Lwx.Builders.Dto.Atributes;
        namespace TempDtoBad;

        [LwxDto(Type = DtoType.Normal)]
        public partial class BadDto
        {
            [LwxDtoProperty(JsonName = "ts")]
            public partial System.DateTime Timestamp { get; set; }
        }
        """;

        var compilation = CSharpCompilation.Create(
            "compBad",
            new[] { CSharpSyntaxTree.ParseText(src) },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Lwx.Builders.Dto.DtoGenerator).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Lwx.Builders.Dto.DtoGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var after = driver.RunGenerators(compilation);
        var run = after.GetRunResult();

        var hasLwx003 = run.Results.SelectMany(r => r.Diagnostics).Any(d => d.Id == "LWX003");
        Assert.True(hasLwx003, "Expected LWX003 diagnostic when property type is unsupported and no JsonConverter is provided");
    }

    [Fact]
    public void LwxDtoIgnore_Skips_Property_And_No_LWX005()
    {
        var src = """
        using Lwx.Builders.Dto.Atributes;
        namespace TempDtoIgnore;

        [LwxDto(Type = DtoType.Normal)]
        public partial class IgnoreDto
        {
            [LwxDtoIgnore]
            public partial int Ignored { get; set; }

            [LwxDtoProperty(JsonName = "ok")]
            public partial int Ok { get; set; }
        }
        """;

        var compilation = CSharpCompilation.Create(
            "compIgnore",
            new[] { CSharpSyntaxTree.ParseText(src) },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Lwx.Builders.Dto.DtoGenerator).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Lwx.Builders.Dto.DtoGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var after = driver.RunGenerators(compilation);
        var run = after.GetRunResult();

        // there should be no LWX005 (missing property attribute) for the ignored property
        var hasLwx005 = run.Results.SelectMany(r => r.Diagnostics).Any(d => d.Id == "LWX005");
        Assert.False(hasLwx005, "Ignored property should not trigger LWX005");

        // generated source should include only the Ok property accessors and not the Ignored property
        var gen = run.Results.SelectMany(r => r.GeneratedSources).FirstOrDefault(g => g.HintName.Contains("LwxDto_IgnoreDto"));
        Assert.True(gen.HintName != null && gen.HintName.Contains("LwxDto_IgnoreDto"));
        var text = gen!.SourceText.ToString();
        Assert.Contains("public partial int Ok", text);
        Assert.DoesNotContain("public partial int Ignored", text);
    }

    [Fact]
    public void DateTimeTypes_Automatically_Get_JsonConverters()
    {
        var src = """
        using Lwx.Builders.Dto.Atributes;
        namespace TempDtoDates;

        [LwxDto(Type = DtoType.Normal)]
        public partial class DateDto
        {
            [LwxDtoProperty(JsonName = "offset")]
            public partial System.DateTimeOffset Offset { get; set; }

            [LwxDtoProperty(JsonName = "date")]
            public partial System.DateOnly Date { get; set; }

            [LwxDtoProperty(JsonName = "time")]
            public partial System.TimeOnly Time { get; set; }
        }
        """;

        var compilation = CSharpCompilation.Create(
            "compDates",
            new[] { CSharpSyntaxTree.ParseText(src) },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Lwx.Builders.Dto.DtoGenerator).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Lwx.Builders.Dto.DtoGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var after = driver.RunGenerators(compilation);
        var run = after.GetRunResult();

        // Ensure no LWX003 diagnostics for these supported types
        var hasLwx003 = run.Results.SelectMany(r => r.Diagnostics).Any(d => d.Id == "LWX003");
        Assert.False(hasLwx003, "DateTimeOffset, DateOnly, and TimeOnly should be supported without JsonConverter");

        var gen = run.Results.SelectMany(r => r.GeneratedSources).FirstOrDefault(g => g.HintName.Contains("LwxDto_DateDto"));
        Assert.True(gen.HintName != null && gen.HintName.Contains("LwxDto_DateDto"));
        var text = gen!.SourceText.ToString();

        // Check that JsonConverter attributes are automatically added
        Assert.Contains("JsonConverter(typeof(System.Text.Json.Serialization.JsonConverter<System.DateTimeOffset>))", text);
        Assert.Contains("JsonConverter(typeof(System.Text.Json.Serialization.JsonConverter<System.DateOnly>))", text);
        Assert.Contains("JsonConverter(typeof(System.Text.Json.Serialization.JsonConverter<System.TimeOnly>))", text);
    }

    [Fact]
    public void DateTime_Property_Warns_LWX007_Recommend_DateTimeOffset()
    {
        var src = """
        using Lwx.Builders.Dto.Atributes;
        namespace TempDtoDateTime;

        [LwxDto(Type = DtoType.Normal)]
        public partial class DateTimeDto
        {
            [LwxDtoProperty(JsonName = "timestamp", JsonConverter = typeof(System.Text.Json.Serialization.JsonConverter<System.DateTime>))]
            public partial System.DateTime Timestamp { get; set; }
        }
        """;

        var compilation = CSharpCompilation.Create(
            "compDateTime",
            new[] { CSharpSyntaxTree.ParseText(src) },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Lwx.Builders.Dto.DtoGenerator).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Lwx.Builders.Dto.DtoGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var after = driver.RunGenerators(compilation);
        var run = after.GetRunResult();

        // Should warn LWX007 for DateTime usage
        var hasLwx007 = run.Results.SelectMany(r => r.Diagnostics).Any(d => d.Id == "LWX007");
        Assert.True(hasLwx007, "Expected LWX007 warning when using DateTime, recommending DateTimeOffset");
    }
}
