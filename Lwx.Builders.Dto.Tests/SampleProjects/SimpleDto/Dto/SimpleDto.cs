using Lwx.Builders.Dto.Atributes;
namespace SimpleDto.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class SimpleDto
{
    [LwxDtoProperty(JsonName = "id")]
    public required partial int Id { get; set; }

    [LwxDtoProperty(JsonName = "name")]
    public partial string? Name { get; set; }
}
