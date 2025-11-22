using System.Text.Json.Serialization;
using Lwx.Archetype.MicroService;
using Lwx.Archetype.MicroService.Atributes;

namespace ExampleCompany.ExampleProduct.Worker001.Dto;

[LwxDto(Type=DtoType.Normal)]
public partial class SimpleResponseDto
{    
    [LwxDtoProperty(JsonName ="ok")]
    public required partial bool Ok { get; set; }

    [LwxDtoProperty(JsonName="err-id")]
    public partial string? ErrId { get; set; }

    [LwxDtoProperty(JsonName="err-msg")]
    public partial string? ErrMsg { get; set; }       
}

