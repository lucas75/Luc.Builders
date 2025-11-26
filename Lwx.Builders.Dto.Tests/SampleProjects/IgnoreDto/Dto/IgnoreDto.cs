using Lwx.Builders.Dto.Atributes;
namespace IgnoreDto.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class IgnoreDto
{
    [LwxDtoIgnore]
    public int Ignored { get; set; }

    [LwxDtoProperty(JsonName = "ok")]
    public partial int Ok { get; set; }
}
