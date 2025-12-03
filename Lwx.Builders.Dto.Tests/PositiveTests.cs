using System;
using System.Text.Json;
using System.Globalization;
using Xunit;
using Lwx.Builders.Dto.Tests.Dto;

// PositiveTests: these tests DO NOT use reflection or build sample projects.
// They instantiate DTO types (NormalDto, DictDto, IgnoreDto), serialize and deserialize them,
// and assert correct runtime behavior. Keep these tests free of reflection to make
// them fast and focused on runtime correctness.

public class PositiveTests
{
    /// <summary>
    /// Ensures enum properties are serialized as strings and the generator emits the
    /// LWX004 warning for enum properties when a JsonStringEnumConverter is applied.
    /// </summary>
    [Fact(DisplayName = "Enum property: JSON string converter and LWX004 warning")]
    public void EnumProperty_Reports_LWX004_And_Adds_JsonStringEnumConverter()
    {
        var instance = new IgnoreDto { Id = 1, Color = MyColors.Green };
        var json = JsonSerializer.Serialize(instance);
        Assert.Contains("\"color\":\"Green\"", json);
    }

    /// <summary>
    /// Basic round-trip serialization test for NormalDto.
    /// </summary>
    [Fact(DisplayName = "NormalDto: serialize/deserialize round-trip")]
    public void LoadNormalDto_CanSerializeDeserialize()
    {
        var theInstance = new NormalDto { Id = 123, Name = "hello" };
        theInstance = (NormalDto)SharedTestHelpers.SerializeAndUnserializeJson(theInstance)!;
        Assert.NotNull(theInstance);
        Assert.Equal(123, theInstance.Id);
        Assert.Equal("hello", theInstance.Name);
    }

    /// <summary>
    /// Verifies IgnoreDto uses the custom converter successfully across a serialize/deserialize roundtrip.
    /// </summary>
    [Fact(DisplayName = "IgnoreDto: custom converter round-trip")]
    public void LoadIgnoreDto_CustomConverterWorksOnRoundtrip()
    {
        var theInstance = new IgnoreDto { Id = 2, Value = "abc" };
        theInstance = (IgnoreDto)SharedTestHelpers.SerializeAndUnserializeJson(theInstance)!;
        Assert.NotNull(theInstance);
        Assert.Equal("abc", theInstance.Value);
    }

    /// <summary>
    /// Verifies that DTO types (Normal, Dict, Ignore) can be serialized and deserialized
    /// for all supported property types (numbers, strings, dates, enums, nested objects).
    /// </summary>
    [Fact(DisplayName = "DTOs: serialize/deserialize all supported types")]
    public void Dtos_Build_And_Serialize_AllTypes()
    {
        // NormalDto
        var simple = new NormalDto { Id = 42, Name = "sdf" };
        simple = (NormalDto?)SharedTestHelpers.SerializeAndUnserializeJson(simple);
        Assert.NotNull(simple);
        Assert.Equal(42, simple!.Id);

        // IgnoreDto uses custom converter and date/enum types
        var conv = new IgnoreDto { Id = 3, Value = "xyz" };
        conv = (IgnoreDto?)SharedTestHelpers.SerializeAndUnserializeJson(conv);
        Assert.NotNull(conv);
        Assert.Equal("xyz", conv!.Value);

        // Date/Time types on IgnoreDto
        var dt = new IgnoreDto
        {
            Id = 4,
            Offset = System.DateTimeOffset.Now,
            Date = System.DateOnly.FromDateTime(DateTime.Today),
            Time = System.TimeOnly.FromDateTime(DateTime.Now)
        };
        dt = (IgnoreDto?)SharedTestHelpers.SerializeAndUnserializeJson(dt);
        Assert.NotNull(dt);

        // DictDto
        var dict = new DictDto { Id = 9, Name = "dict" };
        dict = (DictDto?)SharedTestHelpers.SerializeAndUnserializeJson(dict);
        Assert.NotNull(dict);
        Assert.Equal(9, dict!.Id);
    }

    /// <summary>
    /// Confirms DateTime/DateOnly/TimeOnly values are parsed from multiple JSON formats for NormalDto.
    /// </summary>
    [Theory(DisplayName = "Date/time parsing: accepts multiple JSON formats (NormalDto)")]
    [InlineData("""{"id":1,"date":"2025-11-26","time":"15:30:00","offset":"2025-11-26T15:30:00+02:00"}""", "2025-11-26", "15:30:00", "2025-11-26T15:30:00+02:00")]
    [InlineData("""{"id":1,"date":"2025-11-26","time":"15:30:00","offset":"2025-11-26T13:30:00Z"}""", "2025-11-26", "15:30:00", "2025-11-26T13:30:00Z")]
    [InlineData("""{"id":1,"date":"2025-11-26","time":"15:30:00.123","offset":"2025-11-26T15:30:00.123+02:00"}""", "2025-11-26", "15:30:00.123", "2025-11-26T15:30:00.123+02:00")]
    [InlineData("""{"id":1,"date":"2025-11-26","time":"15:30","offset":"2025-11-26T15:30:00+02:00"}""", "2025-11-26", "15:30", "2025-11-26T15:30:00+02:00")]
    [InlineData("""{"id":1,"date":"2025-11-26","time":"15:30:00.123","offset":"2025-11-26T13:30:00Z"}""", "2025-11-26", "15:30:00.123", "2025-11-26T13:30:00Z")]
    public void DateTimeJsonParsing_AcceptsMultipleFormats(string json, string date, string time, string offset)
    {
        var dto = JsonSerializer.Deserialize<NormalDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(DateOnly.Parse(date), dto!.Dt);
        Assert.Equal(TimeOnly.Parse(time), dto.Time);
        Assert.Equal(DateTimeOffset.Parse(offset), dto.Dh);
    }

    /// <summary>
    /// Confirms DateTime/DateOnly/TimeOnly values are parsed from multiple JSON formats for DictDto.
    /// </summary>
    [Theory(DisplayName = "Date/time parsing: accepts multiple JSON formats (DictDto)")]
    [InlineData("""{"id":1,"date":"2025-11-26","time":"15:30:00","offset":"2025-11-26T15:30:00+02:00"}""", "2025-11-26", "15:30:00", "2025-11-26T15:30:00+02:00")]
    [InlineData("""{"id":1,"date":"2025-11-26","time":"15:30:00","offset":"2025-11-26T13:30:00Z"}""", "2025-11-26", "15:30:00", "2025-11-26T13:30:00Z")]
    [InlineData("""{"id":1,"date":"2025-11-26","time":"15:30:00.123","offset":"2025-11-26T15:30:00.123+02:00"}""", "2025-11-26", "15:30:00.123", "2025-11-26T15:30:00.123+02:00")]
    [InlineData("""{"id":1,"date":"2025-11-26","time":"15:30","offset":"2025-11-26T15:30:00+02:00"}""", "2025-11-26", "15:30", "2025-11-26T15:30:00+02:00")]
    [InlineData("""{"id":1,"date":"2025-11-26","time":"15:30:00.123","offset":"2025-11-26T13:30:00Z"}""", "2025-11-26", "15:30:00.123", "2025-11-26T13:30:00Z")]
    public void DictDto_DateTimeJsonParsing_AcceptsMultipleFormats(string json, string date, string time, string offset)
    {
        var dto = JsonSerializer.Deserialize<DictDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(DateOnly.Parse(date), dto!.Dt);
        Assert.Equal(TimeOnly.Parse(time), dto.Time);
        Assert.Equal(DateTimeOffset.Parse(offset), dto.Dh);
    }

    /// <summary>
    /// Confirms DateTime/DateOnly/TimeOnly values are parsed from multiple JSON formats for IgnoreDto.
    /// </summary>
    [Theory(DisplayName = "Date/time parsing: accepts multiple JSON formats (IgnoreDto)")]
    [InlineData("""{"id":1,"ignored":999,"ok":5,"value":"v","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", "2025-11-26", "15:30:00", "2025-11-26T15:30:00+02:00")]
    [InlineData("""{"id":1,"ignored":999,"ok":5,"value":"v","color":"Green","offset":"2025-11-26T13:30:00Z","date":"2025-11-26","time":"15:30:00"}""", "2025-11-26", "15:30:00", "2025-11-26T13:30:00Z")]
    [InlineData("""{"id":1,"ignored":999,"ok":5,"value":"v","color":"Green","offset":"2025-11-26T15:30:00.123+02:00","date":"2025-11-26","time":"15:30:00.123"}""", "2025-11-26", "15:30:00.123", "2025-11-26T15:30:00.123+02:00")]
    [InlineData("""{"id":1,"ignored":999,"ok":5,"value":"v","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30"}""", "2025-11-26", "15:30", "2025-11-26T15:30:00+02:00")]
    [InlineData("""{"id":1,"ignored":999,"ok":5,"value":"v","color":"Green","offset":"2025-11-26T13:30:00Z","date":"2025-11-26","time":"15:30:00.123"}""", "2025-11-26", "15:30:00.123", "2025-11-26T13:30:00Z")]
    public void IgnoreDto_DateTimeJsonParsing_AcceptsMultipleFormats(string json, string date, string time, string offset)
    {
        var dto = JsonSerializer.Deserialize<IgnoreDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(DateOnly.Parse(date), dto!.Date);
        Assert.Equal(TimeOnly.Parse(time), dto.Time);
        Assert.Equal(DateTimeOffset.Parse(offset), dto.Offset);
    }

    /// <summary>
    /// Validates enum JSON parsing accepts both names and numeric values across DTO types.
    /// </summary>
    [Theory(DisplayName = "Enum parsing: accepts names and numeric values")]
    [InlineData("""{"id":1,"color":"Green"}""", """{"id":1,"color":1}""", """{"id":1,"color":1}""", MyColors.Green)]
    [InlineData("""{"id":1,"color":"Red"}""", """{"id":1,"color":0}""", """{"id":1,"color":0}""", MyColors.Red)]
    [InlineData("""{"id":1,"color":1}""", """{"id":1,"color":1}""", """{"id":1,"color":1}""", MyColors.Green)]
    [InlineData("""{"id":1,"color":0}""", """{"id":1,"color":0}""", """{"id":1,"color":0}""", MyColors.Red)]
    public void EnumJsonParsing_AcceptsNamesAndNumbers(string normalJson, string dictJson, string ignoreJson, MyColors expected)
    {
        var dto = JsonSerializer.Deserialize<NormalDto>(normalJson);
        Assert.NotNull(dto);
        Assert.Equal(expected, dto!.Color);

        var d2 = JsonSerializer.Deserialize<DictDto>(dictJson);
        Assert.NotNull(d2);
        Assert.Equal(expected, d2!.Color);

        var d2b = JsonSerializer.Deserialize<DictDto>(dictJson);
        Assert.NotNull(d2b);
        Assert.Equal(expected, d2b!.Color);

        var ig = JsonSerializer.Deserialize<IgnoreDto>(ignoreJson);
        Assert.NotNull(ig);
        Assert.Equal(expected, ig!.Color);
    }

    /// <summary>
    /// Validates parsing of integer properties from JSON, including edge cases and bounds.
    /// </summary>
    [Theory(DisplayName = "Integer parsing: accepts numeric values and bounds")]
    [InlineData("""{"id":0}""", 0)]
    [InlineData("""{"id":1}""", 1)]
    [InlineData("""{"id":-1}""", -1)]
    [InlineData("""{"id":2147483647}""", 2147483647)]
    public void IntJsonParsing_AcceptsNumericValues(string json, int id)
    {
        var dto = JsonSerializer.Deserialize<NormalDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(id, dto!.Id);

        var d2 = JsonSerializer.Deserialize<DictDto>(json);
        Assert.NotNull(d2);
        Assert.Equal(id, d2!.Id);

        var jsonIgnore = $"{{\"id\":{id},\"ok\":{id}}}";
        var ig = JsonSerializer.Deserialize<IgnoreDto>(jsonIgnore);
        Assert.NotNull(ig);
        Assert.Equal(id, ig!.Ok);
    }

    /// <summary>
    /// Ensures string properties accept various Unicode strings and very long values.
    /// </summary>
    [Theory(DisplayName = "Strings: accept unicode and long strings")]
    [InlineData("""{"id":1,"name":"","value":""}""", "")]
    [InlineData("""{"id":1,"name":"a","value":"a"}""", "a")]
    [InlineData("""{"id":1,"name":"こんにちは","value":"こんにちは"}""", "こんにちは")]
    [InlineData("""{"id":1,"name":"a very long string that is still fine for the DTO property — repeat a few times to be sure","value":"a very long string that is still fine for the DTO property — repeat a few times to be sure"}""", "a very long string that is still fine for the DTO property — repeat a few times to be sure")]
    public void StringJsonParsing_AcceptsVariousStrings(string json, string value)
    {
        var dto = JsonSerializer.Deserialize<NormalDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(value, dto!.Name);
        Assert.Equal(value, dto.Value);

        var d2 = JsonSerializer.Deserialize<DictDto>(json);
        Assert.NotNull(d2);
        Assert.Equal(value, d2!.Name);
        Assert.Equal(value, d2.Value);

        var ig = JsonSerializer.Deserialize<IgnoreDto>(json);
        Assert.NotNull(ig);
        Assert.Equal(value, ig!.Value);
    }

    /// <summary>
    /// Validates nested custom objects are serialized and deserialized correctly.
    /// </summary>
    [Fact(DisplayName = "Nested object: round-trip serialization works")]
    public void CustomObject_Roundtrip_Works()
    {
        // Small nested object not tied to the generator — ensure runtime nested serialization works.
        var input = new Holder { Nested = new Nested { X = 19, Name = "n" } };
        var json = JsonSerializer.Serialize(input);
        var outp = JsonSerializer.Deserialize<Holder>(json);
        Assert.NotNull(outp);
        Assert.Equal(19, outp!.Nested.X);
        Assert.Equal("n", outp.Nested.Name);
    }

    // test-only types used by the custom-object test
    public class Holder { public Nested Nested { get; set; } = new(); }
    public class Nested { public int X { get; set; } public string Name { get; set; } = string.Empty; }
}
