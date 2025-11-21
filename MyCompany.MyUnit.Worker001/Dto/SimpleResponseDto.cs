using System.Text.Json.Serialization;
using Lwx.Archetype.MicroService;
using Lwx.Archetype.MicroService.Atributes;

namespace MyCompany.MyUnit.Worker001.Dto;

[LwxDto(Type=DtoType.Normal)]
public partial class SimpleResponseDto
{
    [JsonPropertyName("ok")]
    public required partial bool Ok { get; set; }

    // since this is nullable, the implementation will include [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]   
    [LwxDtoProperty(JsonName="err-id")]
    public partial string? ErrId { get; set; }

    // since this is nullable, the implementation will include [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]       
    [LwxDtoProperty(JsonName="err-msg")]
    public partial string? ErrMsg { get; set; }       
}

