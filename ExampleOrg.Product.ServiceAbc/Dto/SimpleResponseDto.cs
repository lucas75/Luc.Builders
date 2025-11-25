using System.Text.Json.Serialization;
using Lwx.Builders.Dto.Atributes;

namespace ExampleOrg.Product.ServiceAbc.Dto;

[LwxDto(Type = DtoType.Normal)]
public partial class SimpleResponseDto
{
    [LwxDtoProperty(JsonName = "ok")]
    public required partial bool Ok { get; set; }

    [LwxDtoProperty(JsonName = "err-id")]
    public partial string? ErrId { get; set; }

    [LwxDtoProperty(JsonName = "err-msg")]
    public partial string? ErrMsg { get; set; }
}

