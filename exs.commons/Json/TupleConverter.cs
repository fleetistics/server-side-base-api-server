using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace exs.commons.Json
{
    public class TupleConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType) return false;
            var genType = typeToConvert.GetGenericTypeDefinition();
            return genType == typeof(Tuple<>) || genType == typeof(Tuple<,>) ||
                genType == typeof(Tuple<,,>) || genType == typeof(Tuple<,,,>);
        }

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (!typeToConvert.IsGenericType) return null;
            var genType = typeToConvert.GetGenericTypeDefinition();
            if (genType == typeof(Tuple<>))
            {
                var type = genType.GetGenericArguments()[0];
                return (JsonConverter)Activator.CreateInstance(
                    typeof(Tuple1ConverterImpl<>).MakeGenericType(type),
                    BindingFlags.Instance | BindingFlags.Public, binder: null, args: null, culture: null)!;
            }
            if (genType == typeof(Tuple<,>))
            {
                var type1 = typeToConvert.GetGenericArguments()[0];
                var type2 = typeToConvert.GetGenericArguments()[1];
                return (JsonConverter)Activator.CreateInstance(
                    typeof(Tuple2ConverterImpl<,>).MakeGenericType(new Type[] { type1, type2 }),
                    BindingFlags.Instance | BindingFlags.Public, binder: null, args: null, culture: null)!;
            }
            if (genType == typeof(Tuple<,,>))
            {
                var type1 = typeToConvert.GetGenericArguments()[0];
                var type2 = typeToConvert.GetGenericArguments()[1];
                var type3 = typeToConvert.GetGenericArguments()[2];
                return (JsonConverter)Activator.CreateInstance(
                    typeof(Tuple3ConverterImpl<,,>).MakeGenericType(new Type[] { type1, type2, type3 }),
                    BindingFlags.Instance | BindingFlags.Public, binder: null, args: null, culture: null)!;
            }
            if (genType == typeof(Tuple<,,,>))
            {
                var type1 = typeToConvert.GetGenericArguments()[0];
                var type2 = typeToConvert.GetGenericArguments()[1];
                var type3 = typeToConvert.GetGenericArguments()[2];
                var type4 = typeToConvert.GetGenericArguments()[3];
                return (JsonConverter)Activator.CreateInstance(
                    typeof(Tuple4ConverterImpl<,,,>).MakeGenericType(new Type[] { type1, type2, type3, type4 }),
                    BindingFlags.Instance | BindingFlags.Public, binder: null, args: null, culture: null)!;
            }
            return null;
        }

        private class Tuple1ConverterImpl<T> : JsonConverter<Tuple<T>>
        {
            public Tuple1ConverterImpl() { }

            public override Tuple<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, Tuple<T> value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                JsonSerializer.Serialize(writer, value.Item1, _options);
                writer.WriteEndArray();
            }

            private static JsonSerializerOptions _options = ConfigureJson.CreateDefaultConfiguration();
        }

        private class Tuple2ConverterImpl<T1, T2> : JsonConverter<Tuple<T1, T2>>
        {
            public Tuple2ConverterImpl() { }

            public override Tuple<T1, T2> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, Tuple<T1, T2> value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                JsonSerializer.Serialize(writer, value.Item1, _options);
                JsonSerializer.Serialize(writer, value.Item2, _options);
                writer.WriteEndArray();
            }

            private static JsonSerializerOptions _options = ConfigureJson.CreateDefaultConfiguration();
        }

        private class Tuple3ConverterImpl<T1, T2, T3> : JsonConverter<Tuple<T1, T2, T3>>
        {
            public Tuple3ConverterImpl() { }

            public override Tuple<T1, T2, T3> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, Tuple<T1, T2, T3> value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                JsonSerializer.Serialize(writer, value.Item1, _options);
                JsonSerializer.Serialize(writer, value.Item2, _options);
                JsonSerializer.Serialize(writer, value.Item3, _options);
                writer.WriteEndArray();
            }

            private static JsonSerializerOptions _options = ConfigureJson.CreateDefaultConfiguration();
        }

        private class Tuple4ConverterImpl<T1, T2, T3, T4> : JsonConverter<Tuple<T1, T2, T3, T4>>
        {
            public Tuple4ConverterImpl() { }

            public override Tuple<T1, T2, T3, T4> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, Tuple<T1, T2, T3, T4> value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                JsonSerializer.Serialize(writer, value.Item1, _options);
                JsonSerializer.Serialize(writer, value.Item2, _options);
                JsonSerializer.Serialize(writer, value.Item3, _options);
                JsonSerializer.Serialize(writer, value.Item4, _options);
                writer.WriteEndArray();
            }

            private static JsonSerializerOptions _options = ConfigureJson.CreateDefaultConfiguration();
        }
    }
}
