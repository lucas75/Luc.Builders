using Lwx.Builders.Dto.Atributes;
namespace Lwx.Builders.Dto.Tests.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class NormalDto
{
    [LwxDtoProperty(JsonName = "id")]
    public partial int Id { get; set; }

    [LwxDtoProperty(JsonName = "name")]
    public partial string? Name { get; set; }

    [LwxDtoProperty(JsonName = "value", JsonConverter = typeof(MyStringConverter))]
    public partial string? Value { get; set; }

    [LwxDtoProperty(JsonName = "color")]
    public partial MyColors Color { get; set; }

    [LwxDtoProperty(JsonName = "offset")]
    public partial System.DateTimeOffset Offset { get; set; }

    [LwxDtoProperty(JsonName = "date")]
    public partial System.DateOnly Date { get; set; }

    [LwxDtoProperty(JsonName = "time")]
    public partial System.TimeOnly Time { get; set; }
}
