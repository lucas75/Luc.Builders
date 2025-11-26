using Lwx.Builders.Dto.Atributes;
namespace ConvDto.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class ConvDto
{
    [LwxDtoProperty(JsonName = "val", JsonConverter = typeof(MyStringConverter))]
    public partial string? Value { get; set; }
}
