using Lwx.Builders.Dto.Atributes;
namespace ErrorDto.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class BrokenDto
{
    public partial int Bad { get; set; }
}
