using Lwx.Builders.Dto.Atributes;
namespace ErrorDto.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class DateTimeDto
{
    [LwxDtoProperty(JsonName = "timestamp", JsonConverter = typeof(System.Text.Json.Serialization.JsonConverter<System.DateTime>))]
    public partial System.DateTime Timestamp { get; set; }
}
