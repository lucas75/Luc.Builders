using Lwx.Builders.Dto.Atributes;
namespace BadDto.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class BadDto
{
    [LwxDtoProperty(JsonName = "ts")]
    public partial System.DateTime Timestamp { get; set; }
}
