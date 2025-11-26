using Lwx.Builders.Dto.Atributes;
namespace ErrorDto.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class EnumDto
{
    [LwxDtoProperty(JsonName = "color")]
    public partial MyColors Color { get; set; }
}

public enum MyColors { Red = 0, Green = 1 }
