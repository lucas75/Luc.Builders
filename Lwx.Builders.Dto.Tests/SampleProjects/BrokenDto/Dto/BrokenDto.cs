using Lwx.Builders.Dto.Atributes;
namespace BrokenDto.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class BrokenDto
{
    public partial int Bad { get; set; }
}
