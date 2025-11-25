using System;
#nullable enable
 
namespace Lwx.Builders.Dto.Atributes
{
    public class LwxDtoPropertyAttribute : Attribute
    {
        public string? JsonName { get; set; }
        public Type? JsonConverter { get; set; }

        public LwxDtoPropertyAttribute() { }
    }
}
