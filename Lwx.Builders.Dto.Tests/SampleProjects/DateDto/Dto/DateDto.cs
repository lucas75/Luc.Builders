using Lwx.Builders.Dto.Atributes;
namespace DateDto.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class DateDto
{
    [LwxDtoProperty(JsonName = "offset")]
    public partial System.DateTimeOffset Offset { get; set; }

    [LwxDtoProperty(JsonName = "date")]
    public partial System.DateOnly Date { get; set; }

    [LwxDtoProperty(JsonName = "time")]
    public partial System.TimeOnly Time { get; set; }
}
