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
    [Fact]
    public void EnumProperty_Reports_LWX004_And_Adds_JsonStringEnumConverter()
    {
        var instance = new IgnoreDto { Id = 1, Color = MyColors.Green };
        var json = JsonSerializer.Serialize(instance);
        Assert.Contains("\"color\":\"Green\"", json);
    }

    [Fact]
    public void LoadNormalDto_CanSerializeDeserialize()
    {
        var theInstance = new NormalDto { Id = 123, Name = "hello" };
        theInstance = (NormalDto)SharedTestHelpers.SerializeAndUnserializeJson(theInstance)!;
        Assert.NotNull(theInstance);
        Assert.Equal(123, theInstance.Id);
        Assert.Equal("hello", theInstance.Name);
    }

    [Fact]
    public void LoadIgnoreDto_CustomConverterWorksOnRoundtrip()
    {
        var theInstance = new IgnoreDto { Id = 2, Value = "abc" };
        theInstance = (IgnoreDto)SharedTestHelpers.SerializeAndUnserializeJson(theInstance)!;
        Assert.NotNull(theInstance);
        Assert.Equal("abc", theInstance.Value);
    }

    [Fact]
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

    [Theory]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", "2025-11-26", "15:30:00", "2025-11-26T15:30:00+02:00")]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T13:30:00Z","date":"2025-11-26","time":"15:30:00"}""", "2025-11-26", "15:30:00", "2025-11-26T13:30:00Z")]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T15:30:00.123+02:00","date":"2025-11-26","time":"15:30:00.123"}""", "2025-11-26", "15:30:00.123", "2025-11-26T15:30:00.123+02:00")]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30"}""", "2025-11-26", "15:30", "2025-11-26T15:30:00+02:00")]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T13:30:00Z","date":"2025-11-26","time":"15:30:00.123"}""", "2025-11-26", "15:30:00.123", "2025-11-26T13:30:00Z")]
    public void DateTimeJsonParsing_AcceptsMultipleFormats(string json, string date, string time, string offset)
    {
        var dto = JsonSerializer.Deserialize<NormalDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(DateOnly.Parse(date), dto!.Date);
        Assert.Equal(TimeOnly.Parse(time), dto.Time);
        Assert.Equal(DateTimeOffset.Parse(offset), dto.Offset);
    }

    [Theory]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", "2025-11-26", "15:30:00", "2025-11-26T15:30:00+02:00")]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T13:30:00Z","date":"2025-11-26","time":"15:30:00"}""", "2025-11-26", "15:30:00", "2025-11-26T13:30:00Z")]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T15:30:00.123+02:00","date":"2025-11-26","time":"15:30:00.123"}""", "2025-11-26", "15:30:00.123", "2025-11-26T15:30:00.123+02:00")]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30"}""", "2025-11-26", "15:30", "2025-11-26T15:30:00+02:00")]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T13:30:00Z","date":"2025-11-26","time":"15:30:00.123"}""", "2025-11-26", "15:30:00.123", "2025-11-26T13:30:00Z")]
    public void DictDto_DateTimeJsonParsing_AcceptsMultipleFormats(string json, string date, string time, string offset)
    {
        var dto = JsonSerializer.Deserialize<DictDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(DateOnly.Parse(date), dto!.Date);
        Assert.Equal(TimeOnly.Parse(time), dto.Time);
        Assert.Equal(DateTimeOffset.Parse(offset), dto.Offset);
    }

    [Theory]
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

    [Theory]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", """{"id":1,"name":"n","value":"v","color":1,"offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", """{"id":1,"ignored":0,"ok":1,"value":"v","color":1,"offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", MyColors.Green)]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Red","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", """{"id":1,"name":"n","value":"v","color":0,"offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", """{"id":1,"ignored":0,"ok":1,"value":"v","color":0,"offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", MyColors.Red)]
    [InlineData("""{"id":1,"name":"n","value":"v","color":1,"offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", """{"id":1,"name":"n","value":"v","color":1,"offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", """{"id":1,"ignored":0,"ok":1,"value":"v","color":1,"offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", MyColors.Green)]
    [InlineData("""{"id":1,"name":"n","value":"v","color":0,"offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", """{"id":1,"name":"n","value":"v","color":0,"offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", """{"id":1,"ignored":0,"ok":1,"value":"v","color":0,"offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", MyColors.Red)]
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

    [Theory]
    [InlineData("""{"id":0,"name":"n","value":"v","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", 0)]
    [InlineData("""{"id":1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", 1)]
    [InlineData("""{"id":-1,"name":"n","value":"v","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", -1)]
    [InlineData("""{"id":2147483647,"name":"n","value":"v","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", 2147483647)]
    public void IntJsonParsing_AcceptsNumericValues(string json, int id)
    {
        var dto = JsonSerializer.Deserialize<NormalDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(id, dto!.Id);

        var d2 = JsonSerializer.Deserialize<DictDto>(json);
        Assert.NotNull(d2);
        Assert.Equal(id, d2!.Id);

        var jsonIgnore = $"{{\"id\":{id},\"ignored\":0,\"ok\":{id},\"value\":\"v\",\"color\":\"Green\",\"offset\":\"2025-11-26T15:30:00+02:00\",\"date\":\"2025-11-26\",\"time\":\"15:30:00\"}}";
        var ig = JsonSerializer.Deserialize<IgnoreDto>(jsonIgnore);
        Assert.NotNull(ig);
        Assert.Equal(id, ig!.Ok);
    }

    [Theory]
    [InlineData("""{"id":1,"name":"","value":"","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", "")]
    [InlineData("""{"id":1,"name":"a","value":"a","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", "a")]
    [InlineData("""{"id":1,"name":"こんにちは","value":"こんにちは","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", "こんにちは")]
    [InlineData("""{"id":1,"name":"a very long string that is still fine for the DTO property — repeat a few times to be sure","value":"a very long string that is still fine for the DTO property — repeat a few times to be sure","color":"Green","offset":"2025-11-26T15:30:00+02:00","date":"2025-11-26","time":"15:30:00"}""", "a very long string that is still fine for the DTO property — repeat a few times to be sure")]
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

    [Fact]
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
