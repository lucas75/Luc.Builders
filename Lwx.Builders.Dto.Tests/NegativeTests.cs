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
    [InlineData("{\"id\":\"not-an-int\"}")]
    // invalid offset
    [InlineData("{\"offset\":\"not-a-datetime\"}")]
    // invalid date
    [InlineData("{\"date\":\"2025-99-99\"}")]
    // invalid time
    [InlineData("{\"time\":\"25:61:00\"}")]
    // invalid enum value
    [InlineData("{\"color\":\"NotAColor\"}")]
    public void NormalDto_InvalidJson_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NormalDto>(json));
    }

    [Theory]
    // invalid integer for id
    [InlineData("{\"id\":\"not-an-int\"}")]
    // invalid offset
    [InlineData("{\"offset\":\"not-a-datetime\"}")]
    // invalid date
    [InlineData("{\"date\":\"2025-99-99\"}")]
    // invalid time
    [InlineData("{\"time\":\"25:61:00\"}")]
    // invalid enum value
    [InlineData("{\"color\":\"NotAColor\"}")]
    public void DictDto_InvalidJson_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DictDto>(json));
    }

    [Theory]
    // invalid int for ok
    [InlineData("{\"ok\":\"bad-int\"}")]
    // invalid offset
    [InlineData("{\"offset\":\"not-a-datetime\"}")]
    // invalid date
    [InlineData("{\"date\":\"2025-99-99\"}")]
    // invalid time
    [InlineData("{\"time\":\"25:61:00\"}")]
    // invalid enum
    [InlineData("{\"color\":\"NotAColor\"}")]
    public void IgnoreDto_InvalidJson_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IgnoreDto>(json));
    }
}
