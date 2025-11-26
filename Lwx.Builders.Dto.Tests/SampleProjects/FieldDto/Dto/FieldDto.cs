using Lwx.Builders.Dto.Atributes;
namespace FieldDto.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class FieldDto
{
    public int NotAllowedField;

    [LwxDtoProperty(JsonName = "id")]
    public partial int Id { get; set; }
}
