using System;
using Xunit;

// CompileErrorTests: these tests use committed SampleProjects and MSBuild to assert
// compile-time diagnostics and generator warnings/errors.

public class CompileErrorTests
{
    [Fact]
    public void DtoGenerator_Reports_LWX005_When_Missing_Property_Attribute()
    {
        var res = SharedTestHelpers.BuildAndRunSampleProject("ErrorDto");
        var has = res.BuildOutput?.Contains("LWX005", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(has, "Expected diagnostic LWX005 to be reported when DTO property is missing LwxDtoProperty or LwxDtoIgnore");
    }

    [Fact]
    public void ClassWithField_Reports_LWX006()
    {
        var res = SharedTestHelpers.BuildAndRunSampleProject("ErrorDto");
        var hasLwx006 = res.BuildOutput?.Contains("LWX006", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(hasLwx006, "Expected LWX006 diagnostic when DTO definition contains fields");
    }

    [Fact]
    public void PropertyWithoutConverter_UnsupportedType_Reports_LWX003()
    {
        var res = SharedTestHelpers.BuildAndRunSampleProject("ErrorDto");
        var hasLwx003b = res.BuildOutput?.Contains("LWX003", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(hasLwx003b, "Expected LWX003 diagnostic when property type is unsupported and no JsonConverter is provided");
    }

    [Fact]
    public void DateTime_Property_Warns_LWX007_Recommend_DateTimeOffset()
    {
        var res = SharedTestHelpers.BuildAndRunSampleProject("ErrorDto");
        var hasLwx007 = res.BuildOutput?.Contains("LWX007", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(hasLwx007, "Expected LWX007 warning when using DateTime, recommending DateTimeOffset");
    }

    [Fact]
    public void EnumConstants_Report_LWX004()
    {
        var res = SharedTestHelpers.BuildAndRunSampleProject("ErrorDto");
        var hasLwx004 = res.BuildOutput?.Contains("LWX004", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.True(hasLwx004, "Expected LWX004 warning for enum constants without JsonPropertyName attributes");
    }

    [Fact]
    public void ErrorDto_Fails_To_Build()
    {
        var res = SharedTestHelpers.BuildAndRunSampleProject("ErrorDto");
        Assert.False(res.BuildSucceeded, "Expected ErrorDto project to fail to build");
        var hasKnown = (res.BuildOutput?.Contains("LWX003", StringComparison.OrdinalIgnoreCase) ?? false)
            || (res.BuildOutput?.Contains("LWX005", StringComparison.OrdinalIgnoreCase) ?? false)
            || (res.BuildOutput?.Contains("LWX006", StringComparison.OrdinalIgnoreCase) ?? false);
        Assert.True(hasKnown, "Expected at least one LWX003/LWX005/LWX006 diagnostic in the failed build output");
    }
}
