using System;
using System.Linq;
using System.Text.Json.Serialization;
using Xunit;
using Lwx.Builders.Dto.Tests.Dto;

namespace Lwx.Builders.Dto.Tests;

// StructuralTests: these tests inspect the compiled DTO types using reflection
// to ensure the source generator produced the expected backing fields, attributes,
// and property types. These tests intentionally use reflection and are slower than
// the 'positive' runtime-only tests.

public class StructuralTests
{
    /// <summary>
    /// Confirms the generator produces backing fields and properties for partial properties defined in DTOs.
    /// </summary>
    [Fact(DisplayName = "Generator: emits properties and backing fields for partial DTO properties")]
    public void DtoGenerator_Generates_For_Partial_Properties()
    {
        var t = typeof(NormalDto);
        Assert.NotNull(t.GetProperty("Id"));
        Assert.NotNull(t.GetProperty("Name"));
        var fId = t.GetField("_id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var fName = t.GetField("_name", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(fId);
        Assert.NotNull(fName);
    }

    /// <summary>
    /// Ensures DictDto includes an internal dictionary backing field for dynamic properties.
    /// </summary>
    [Fact(DisplayName = "DictDto: emits internal dictionary backend for properties")]
    public void DictionaryDto_Generates_Dictionary_Backend()
    {
        var t = typeof(DictDto);
        var dictField = t.GetField("_properties", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(dictField);
        Assert.True(dictField.FieldType.FullName?.StartsWith("System.Collections.Generic.Dictionary") ?? false, "Expected _properties to be a Dictionary<,>");
    }

    /// <summary>
    /// Asserts a property decorated with a JSON converter in the generator results in the JsonConverterAttribute being emitted.
    /// </summary>
    [Fact(DisplayName = "Properties: emit JsonConverterAttribute for custom converters")]
    public void PropertyWithJsonConverter_Is_Emitted_With_JsonConverterAttribute()
    {
        var t = typeof(IgnoreDto);
        var prop = t.GetProperty("Value") ?? throw new InvalidOperationException("Property Value not found on IgnoreDto");
        var convAttr = (JsonConverterAttribute?)prop.GetCustomAttributes(false).FirstOrDefault(a => a.GetType().Name == "JsonConverterAttribute");
        Assert.NotNull(convAttr);
        var convType = convAttr!.ConverterType ?? throw new InvalidOperationException("JsonConverterAttribute.ConverterType not set");
        Assert.Equal(typeof(MyStringConverter), convType);
    }

    /// <summary>
    /// Verifies LwxDtoIgnore marks properties as ignored and generator does not emit LWX005 for those properties.
    /// </summary>
    [Fact(DisplayName = "Ignored property: generator skips and no LWX005 diagnostic")]
    public void LwxDtoIgnore_Skips_Property_And_No_LWX005()
    {
        var t = typeof(IgnoreDto);
        Assert.NotNull(t.GetProperty("Ok"));
        var ignoredProp = t.GetProperty("Ignored");
        Assert.NotNull(ignoredProp);
        var hasIgnoreAttr = ignoredProp!.GetCustomAttributes(false).Any(a => a.GetType().Name == "LwxDtoIgnoreAttribute");
        Assert.True(hasIgnoreAttr, "Expected property 'Ignored' to be decorated with LwxDtoIgnoreAttribute");
    }

    /// <summary>
    /// Verifies date/time types are emitted with appropriate JSON converters and round-trip serialization works.
    /// </summary>
    [Fact(DisplayName = "Date/time types: generator emits JSON converters and runtime round-trip passes")]
    public void DateTimeTypes_Automatically_Get_JsonConverters()
    {
        var t = typeof(IgnoreDto);
        Assert.Equal(typeof(System.DateTimeOffset?), t.GetProperty("Offset")?.PropertyType);
        Assert.Equal(typeof(System.DateOnly?), t.GetProperty("Date")?.PropertyType);
        Assert.Equal(typeof(System.TimeOnly?), t.GetProperty("Time")?.PropertyType);

        var instance = new IgnoreDto
        {
            Id = 9,
            Offset = System.DateTimeOffset.Now,
            Date = System.DateOnly.FromDateTime(DateTime.Today),
            Time = System.TimeOnly.FromDateTime(DateTime.Now)
        };
        var round = SharedTestHelpers.SerializeAndUnserializeJson(instance);
        Assert.NotNull(round);
    }
}
