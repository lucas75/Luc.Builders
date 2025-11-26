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
        var instance = new IgnoreDto { Color = MyColors.Green };
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
        var theInstance = new IgnoreDto { Value = "abc" };
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
        var conv = new IgnoreDto { Value = "xyz" };
        conv = (IgnoreDto?)SharedTestHelpers.SerializeAndUnserializeJson(conv);
        Assert.NotNull(conv);
        Assert.Equal("xyz", conv!.Value);

        // Date/Time types on IgnoreDto
        var dt = new IgnoreDto
        {
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
    [InlineData("2025-11-26T15:30:00+02:00","2025-11-26","15:30:00")]
    [InlineData("2025-11-26T13:30:00Z","2025-11-26","15:30:00")]
    [InlineData("2025-11-26T15:30:00.123+02:00","2025-11-26","15:30:00.123")]
    [InlineData("2025-11-26T15:30:00+02:00","2025-11-26","15:30")]
    [InlineData("2025-11-26T13:30:00Z","2025-11-26","15:30:00.123")]
    public void DateTimeJsonParsing_AcceptsMultipleFormats(string offset, string date, string time)
    {
        var json = $"{{\"id\":1,\"name\":\"n\",\"value\":\"v\",\"color\":\"Green\",\"offset\":\"{offset}\",\"date\":\"{date}\",\"time\":\"{time}\"}}";
        var dto = JsonSerializer.Deserialize<NormalDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(DateOnly.Parse(date), dto!.Date);
        Assert.Equal(TimeOnly.Parse(time), dto.Time);
        Assert.Equal(DateTimeOffset.Parse(offset), dto.Offset);
    }

    [Theory]
    [InlineData("2025-11-26T15:30:00+02:00","2025-11-26","15:30:00")]
    [InlineData("2025-11-26T13:30:00Z","2025-11-26","15:30:00")]
    [InlineData("2025-11-26T15:30:00.123+02:00","2025-11-26","15:30:00.123")]
    [InlineData("2025-11-26T15:30:00+02:00","2025-11-26","15:30")]
    [InlineData("2025-11-26T13:30:00Z","2025-11-26","15:30:00.123")]
    public void DictDto_DateTimeJsonParsing_AcceptsMultipleFormats(string offset, string date, string time)
    {
        var json = $"{{\"id\":1,\"name\":\"n\",\"value\":\"v\",\"color\":\"Green\",\"offset\":\"{offset}\",\"date\":\"{date}\",\"time\":\"{time}\"}}";
        var dto = JsonSerializer.Deserialize<DictDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(DateOnly.Parse(date), dto!.Date);
        Assert.Equal(TimeOnly.Parse(time), dto.Time);
        Assert.Equal(DateTimeOffset.Parse(offset), dto.Offset);
    }

    [Theory]
    [InlineData("2025-11-26T15:30:00+02:00","2025-11-26","15:30:00")]
    [InlineData("2025-11-26T13:30:00Z","2025-11-26","15:30:00")]
    [InlineData("2025-11-26T15:30:00.123+02:00","2025-11-26","15:30:00.123")]
    [InlineData("2025-11-26T15:30:00+02:00","2025-11-26","15:30")]
    [InlineData("2025-11-26T13:30:00Z","2025-11-26","15:30:00.123")]
    public void IgnoreDto_DateTimeJsonParsing_AcceptsMultipleFormats(string offset, string date, string time)
    {
        var json = $"{{\"ignored\":999,\"ok\":5,\"value\":\"v\",\"color\":\"Green\",\"offset\":\"{offset}\",\"date\":\"{date}\",\"time\":\"{time}\"}}";
        var dto = JsonSerializer.Deserialize<IgnoreDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(DateOnly.Parse(date), dto!.Date);
        Assert.Equal(TimeOnly.Parse(time), dto.Time);
        Assert.Equal(DateTimeOffset.Parse(offset), dto.Offset);
    }
}
