using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Lwx.Builders.Dto.Tests.MockServices;

// CompileErrorTests: use an in-memory generator harness rather than invoking MSBuild.
public class CompileErrorTests
{   
    [Fact]
    public void ErrorDto_AllExpectedDiagnosticsPresent()
    {
        var ErrorDtoSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Program.cs"] = $$"""
                System.Console.WriteLine("ERROR_DTO_OK");
                return 0;

                """,

            ["Dto/BadDto.cs"] = $$"""
                using Lwx.Builders.Dto.Atributes;
                namespace ErrorDto.Dto;

                [LwxDto(Type = DtoType.Normal)]
                public partial class BadDto
                {
                    [LwxDtoProperty(JsonName = "ts")]
                    public partial System.DateTime Timestamp { get; set; }
                }

                """,

            ["Dto/BrokenDto.cs"] = $$"""
                using Lwx.Builders.Dto.Atributes;
                namespace ErrorDto.Dto;

                [LwxDto(Type = DtoType.Normal)]
                public partial class BrokenDto
                {
                    public partial int Bad { get; set; }
                }

                """,

            ["Dto/DateTimeDto.cs"] = $$"""
                using Lwx.Builders.Dto.Atributes;
                namespace ErrorDto.Dto;

                [LwxDto(Type = DtoType.Normal)]
                public partial class DateTimeDto
                {
                    [LwxDtoProperty(JsonName = "timestamp", JsonConverter = typeof(System.Text.Json.Serialization.JsonConverter<System.DateTime>))]
                    public partial System.DateTime Timestamp { get; set; }
                }

                """,

            ["Dto/EnumDto.cs"] = $$"""
                using Lwx.Builders.Dto.Atributes;
                namespace ErrorDto.Dto;

                [LwxDto(Type = DtoType.Normal)]
                public partial class EnumDto
                {
                    [LwxDtoProperty(JsonName = "color")]
                    public partial MyColors Color { get; set; }
                }

                public enum MyColors { Red = 0, Green = 1 }

                """,

            ["Dto/FieldDto.cs"] = $$"""
                using Lwx.Builders.Dto.Atributes;
                namespace ErrorDto.Dto;

                [LwxDto(Type = DtoType.Normal)]
                public partial class FieldDto
                {
                    public int NotAllowedField;

                    [LwxDtoProperty(JsonName = "id")]
                    public partial int Id { get; set; }
                }

                """,
        };

        var RunResult = MockCompiler.RunGenerator(ErrorDtoSources);

        // Ensure compilation/generator produced errors as expected
        Assert.True(RunResult.HasErrors, $"Expected generator/compilation errors. Diagnostics: {MockCompiler.FormatDiagnostics(RunResult)}");

        // Collect diagnostic IDs
        var diagIds = RunResult.Diagnostics.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Error diagnostics we expect
        Assert.True(diagIds.Contains("LWX003"), $"Expected diagnostic LWX003 (unsupported property type / missing converter). Diagnostics: {MockCompiler.FormatDiagnostics(RunResult)}");
        Assert.True(diagIds.Contains("LWX005"), $"Expected diagnostic LWX005 (missing property attribute). Diagnostics: {MockCompiler.FormatDiagnostics(RunResult)}");
        Assert.True(diagIds.Contains("LWX006"), $"Expected diagnostic LWX006 (field detected where not allowed). Diagnostics: {MockCompiler.FormatDiagnostics(RunResult)}");

        // Warnings we expect
        Assert.True(diagIds.Contains("LWX004"), $"Expected warning LWX004 (enum constants). Diagnostics: {MockCompiler.FormatDiagnostics(RunResult)}");
        Assert.True(diagIds.Contains("LWX007"), $"Expected warning LWX007 (naming/path mismatch). Diagnostics: {MockCompiler.FormatDiagnostics(RunResult)}");

        // At least one of the main error categories must be present (safety net)
        var hasKnown = diagIds.Contains("LWX003") || diagIds.Contains("LWX005") || diagIds.Contains("LWX006");
        Assert.True(hasKnown, "Expected at least one LWX003/LWX005/LWX006 diagnostic in the failed result");
    }
}
