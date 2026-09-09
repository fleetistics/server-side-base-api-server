using System.Text.Json;
using System.Text.Json.Serialization;

namespace exs.commons.Json
{
    public class DateTime2UnixSerializer : JsonConverter<DateTime>
    {
        public override bool HandleNull => true;
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return default(DateTime);
            if (reader.TokenType != JsonTokenType.Number) throw new JsonException($"Unexpected token parsing date. Expected long, got {reader.TokenType}.");
            return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).DateTime;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            if (value == default) writer.WriteNullValue();
            else writer.WriteNumberValue(new DateTimeOffset(value, TimeSpan.Zero).ToUnixTimeSeconds());
        }
    }
    
}
