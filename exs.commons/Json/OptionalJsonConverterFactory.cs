using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace exs.commons.Json
{
    public class OptionalJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (!CanConvert(typeToConvert)) return null;
            var innerType = typeToConvert.GetGenericArguments()[0];
            return (JsonConverter)Activator.CreateInstance(
                typeof(OptionalConverterImpl<>).MakeGenericType(innerType),
                BindingFlags.Instance | BindingFlags.Public, binder: null, args: null, culture: null)!;
        }

        private class OptionalConverterImpl<T> : JsonConverter<Optional<T>>
        {
            // Must opt in explicitly: STJ's default handling for a non-nullable value
            // type (Optional<T> is a struct) throws on a JSON null token before Read()
            // is ever invoked. Reading "null" as a legitimate present value (clearing a
            // nullable field) requires Read() to run for it.
            public override bool HandleNull => true;

            public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                // Delegates to T's own (possibly custom) converter, so e.g. DateTime2UnixSerializer
                // still applies if T is a date type.
                var value = JsonSerializer.Deserialize<T>(ref reader, options);
                return Optional<T>.Of(value);
            }

            public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
            {
                // Optional<T>-bearing DTOs are request-only (PATCH bodies) and are never
                // serialized as output — a working Write path has no caller. Throwing
                // loudly here surfaces that assumption breaking instead of silently
                // emitting a wrong shape if it ever is.
                throw new NotSupportedException(
                    $"{typeof(Optional<T>)} is deserialize-only and must never be serialized as output.");
            }
        }
    }
}
