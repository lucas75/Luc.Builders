using System;
using System.Text.Json;
using Xunit;
using Lwx.Builders.Dto.Tests.Dto;

// NegativeTests: runtime deserialization failures — JSON inputs that should be rejected
// by System.Text.Json when targeting the DTO types. These tests assert that
// invalid values cause JsonException (or equivalent) instead of producing an object.

public class NegativeTests
{
    [Theory]
    // invalid integer for id
    [InlineData("""{"id":"not-an-int"}""")]
    // invalid offset
    [InlineData("""{"offset":"not-a-datetime"}""")]
    // invalid date
    [InlineData("""{"date":"2025-99-99"}""")]
    // invalid time
    [InlineData("""{"time":"25:61:00"}""")]
    // invalid enum value
    [InlineData("""{"color":"NotAColor"}""")]
    // integer overflow
    [InlineData("""{"id":999999999999999999999}""")]
    // floating point for integer
    [InlineData("""{"id":1.23}""")]
    // malformed JSON (guaranteed parser error)
    [InlineData("""{""")]
    public void NormalDto_InvalidJson_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NormalDto>(json));
    }

    [Theory]
    // invalid integer for id
    [InlineData("""{"id":"not-an-int"}""")]
    // invalid offset
    [InlineData("""{"offset":"not-a-datetime"}""")]
    // invalid date
    [InlineData("""{"date":"2025-99-99"}""")]
    // invalid time
    [InlineData("""{"time":"25:61:00"}""")]
    // invalid enum value
    [InlineData("""{"color":"NotAColor"}""")]
    // integer overflow
    [InlineData("""{"id":999999999999999999999}""")]
    // floating point for integer
    [InlineData("""{"id":1.23}""")]
    // malformed JSON (guaranteed parser error)
    [InlineData("""{""")]
    public void DictDto_InvalidJson_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DictDto>(json));
    }

    [Theory]
    // invalid int for ok
    [InlineData("""{"ok":"bad-int"}""")]
    // invalid offset
    [InlineData("""{"offset":"not-a-datetime"}""")]
    // invalid date
    [InlineData("""{"date":"2025-99-99"}""")]
    // invalid time
    [InlineData("""{"time":"25:61:00"}""")]
    // invalid enum
    [InlineData("""{"color":"NotAColor"}""")]
    // integer overflow
    [InlineData("""{"ok":999999999999999999999}""")]
    // floating point for integer
    [InlineData("""{"ok":1.23}""")]
    // malformed JSON (guaranteed parser error)
    [InlineData("""{""")]
    public void IgnoreDto_InvalidJson_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IgnoreDto>(json));
    }

    [Theory]
    // malformed JSON (guaranteed parser error)
    [InlineData("""{""")]
    // explicit text that's not valid JSON
    [InlineData("""not-json""")]
    public void CustomHolder_InvalidJson_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NegativeCustomHolder>(json));
    }

    // small types used purely for the custom-object negative tests
    public class NegativeCustomHolder { public NegativeNested Nested { get; set; } = new(); }
    public class NegativeNested { public int X { get; set; } }
}
