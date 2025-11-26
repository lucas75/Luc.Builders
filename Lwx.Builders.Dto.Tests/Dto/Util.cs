using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lwx.Builders.Dto.Tests.Dto;

public class MyStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() ?? string.Empty;

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

public enum MyColors { Red = 0, Green = 1 }
