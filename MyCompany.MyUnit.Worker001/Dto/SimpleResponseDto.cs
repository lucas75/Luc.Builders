using System.Text.Json.Serialization;
using Lwx.Archetype.MicroService;
using Lwx.Archetype.MicroService.Atributes;

namespace MyCompany.MyUnit.Worker001.Dto;

// DtoType.Normal -> the implementation of the partial properties will be backing fields;
// DtoNormal.Dictionary -> the implementation of the partial properties will use a dictionary to store the values;
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

// LwxDtoProperty must support also JsonConverter=
// LwxDtoProperty will add converter for enum to use JsonStringEnumConverter
// Enums accessed via LwxDtoProperty will issue warnings if the constant does not declare JsonPropertyName
// LwxDtoProperty will issue error if the property result type is not primitive, is not enum, is not annotated with [LwxDto] and doesn't declare a JsonConverter