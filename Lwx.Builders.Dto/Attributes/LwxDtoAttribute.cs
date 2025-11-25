using System;

namespace Lwx.Builders.Dto.Atributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class LwxDtoAttribute : Attribute
    {
        public Lwx.Builders.Dto.Atributes.DtoType Type { get; set; } = Lwx.Builders.Dto.Atributes.DtoType.Normal;

        public LwxDtoAttribute() { }

        public LwxDtoAttribute(Lwx.Builders.Dto.Atributes.DtoType type)
        {
            Type = type;
        }
    }
}
