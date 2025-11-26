using Lwx.Builders.Dto.Atributes;
namespace EnumDto.Dto;

public enum MyColors { Red = 0, Green = 1 }

[LwxDto(Type = DtoType.Normal)]
public partial class EnumDto
{
    [LwxDtoProperty(JsonName = "color")]
    public partial MyColors Color { get; set; }
}
