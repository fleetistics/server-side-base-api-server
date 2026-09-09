using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace exs.commons.Json
{
    public class ValueTupleConverter : JsonConverterFactory
    {
        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (!typeToConvert.IsGenericType) return null;
            var genType = typeToConvert.GetGenericTypeDefinition();
            if (genType == typeof(ValueTuple<>))
            {
                var type = genType.GetGenericArguments()[0];
                return (JsonConverter)Activator.CreateInstance(
                    typeof(Tuple1ConverterImpl<>).MakeGenericType(type),
                    BindingFlags.Instance | BindingFlags.Public, binder: null, args: null, culture: null)!;
            }
            if (genType == typeof(ValueTuple<,>))
            {
                var type1 = typeToConvert.GetGenericArguments()[0];
                var type2 = typeToConvert.GetGenericArguments()[1];
                return (JsonConverter)Activator.CreateInstance(
                    typeof(Tuple2ConverterImpl<,>).MakeGenericType(new Type[] { type1, type2 }),
                    BindingFlags.Instance | BindingFlags.Public, binder: null, args: null, culture: null)!;
            }
            if (genType == typeof(ValueTuple<,,>))
            {
                var type1 = typeToConvert.GetGenericArguments()[0];
                var type2 = typeToConvert.GetGenericArguments()[1];
                var type3 = typeToConvert.GetGenericArguments()[2];
                return (JsonConverter)Activator.CreateInstance(
                    typeof(Tuple3ConverterImpl<,,>).MakeGenericType(new Type[] { type1, type2, type3 }),
                    BindingFlags.Instance | BindingFlags.Public, binder: null, args: null, culture: null)!;
            }
            if (genType == typeof(ValueTuple<,,,>))
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

        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType) return false;
            var genType = typeToConvert.GetGenericTypeDefinition();
            return genType == typeof(ValueTuple<>) || genType == typeof(ValueTuple<,>) || 
                genType == typeof(ValueTuple<,,>) || genType == typeof(ValueTuple<,,,>);
        }

        private class Tuple1ConverterImpl<T> : JsonConverter<ValueTuple<T>>
        {
            public Tuple1ConverterImpl() { }

            public override ValueTuple<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, ValueTuple<T> value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                JsonSerializer.Serialize(writer, value.Item1, _options);
                writer.WriteEndArray();
            }

            private static JsonSerializerOptions _options = ConfigureJson.CreateDefaultConfiguration();
        }

        private class Tuple2ConverterImpl<T1, T2> : JsonConverter<ValueTuple<T1, T2>>
        {
            public Tuple2ConverterImpl() 
            { 

            }

            public override ValueTuple<T1, T2> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, ValueTuple<T1, T2> value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                JsonSerializer.Serialize(writer, value.Item1, _options);
                JsonSerializer.Serialize(writer, value.Item2, _options);
                writer.WriteEndArray();
            }

            private static JsonSerializerOptions _options = ConfigureJson.CreateDefaultConfiguration();
        }

        private class Tuple3ConverterImpl<T1, T2, T3> : JsonConverter<ValueTuple<T1, T2, T3>>
        {
            public Tuple3ConverterImpl() { }

            public override ValueTuple<T1, T2, T3> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, ValueTuple<T1, T2, T3> value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                JsonSerializer.Serialize(writer, value.Item1, _options);
                JsonSerializer.Serialize(writer, value.Item2, _options);
                JsonSerializer.Serialize(writer, value.Item3, _options);
                writer.WriteEndArray();
            }

            private static JsonSerializerOptions _options = ConfigureJson.CreateDefaultConfiguration();
        }

        private class Tuple4ConverterImpl<T1, T2, T3, T4> : JsonConverter<ValueTuple<T1, T2, T3, T4>>
        {
            public Tuple4ConverterImpl() { }

            public override ValueTuple<T1, T2, T3, T4> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, ValueTuple<T1, T2, T3, T4> value, JsonSerializerOptions options)
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
