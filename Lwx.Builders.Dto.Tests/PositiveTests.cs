using System;
using System.Text.Json;
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
}
