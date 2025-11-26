using System.Text.Json;
using System.Text.Json.Serialization;
namespace ConvDto.Dto;

public sealed class MyStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) => reader.GetString() ?? string.Empty;
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
