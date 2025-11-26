using Lwx.Builders.Dto.Atributes;
namespace DateDto.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class DateTimeDto
{
    [LwxDtoProperty(JsonName = "timestamp", JsonConverter = typeof(System.Text.Json.Serialization.JsonConverter<System.DateTime>))]
    public partial System.DateTime Timestamp { get; set; }
}
