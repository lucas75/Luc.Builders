using Lwx.Builders.Dto.Atributes;
namespace Lwx.Builders.Dto.Tests.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class IgnoreDto
{
    [LwxDtoIgnore]
    public int Ignored { get; set; }

    [LwxDtoProperty(JsonName = "ok")]
    public partial int Ok { get; set; }

    // Additional properties used by tests: enum, custom converter and date/time
    [LwxDtoProperty(JsonName = "value", JsonConverter = typeof(MyStringConverter))]
    public partial string Value { get; set; }

    [LwxDtoProperty(JsonName = "color")]
    public partial MyColors Color { get; set; }

    [LwxDtoProperty(JsonName = "offset")]
    public partial System.DateTimeOffset Offset { get; set; }

    [LwxDtoProperty(JsonName = "date")]
    public partial System.DateOnly Date { get; set; }

    [LwxDtoProperty(JsonName = "time")]
    public partial System.TimeOnly Time { get; set; }
}
