using Lwx.Builders.Dto.Atributes;
namespace DictDto.Dto;

[LwxDto(Type = DtoType.Dictionary)]
public partial class DictDto
{
    [LwxDtoProperty(JsonName = "id")]
    public required partial int Id { get; set; }

    [LwxDtoProperty(JsonName = "name")]
    public partial string? Name { get; set; }
}
