using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace exs.commons.Json
{
    public class JsonExpandoConverter : JsonConverter<dynamic>
    {
        public override dynamic Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.True) return true;
            if (reader.TokenType == JsonTokenType.False) return false;
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt64(out long l)) return l;
                return reader.GetDouble();
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                //if (reader.TryGetDateTime(out DateTime datetime)) return datetime;
                return reader.GetString()!;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var documentV = JsonDocument.ParseValue(ref reader);
                return readObject(documentV.RootElement);
            }
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                using var documentV = JsonDocument.ParseValue(ref reader);
                return readList(documentV.RootElement)!;
            }
            // Use JsonElement as fallback.
            // Newtonsoft uses JArray or JObject.
            var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }

        private object readObject(JsonElement jsonElement)
        {
            IDictionary<string, object?> expandoObject = new ExpandoObject();
            foreach (var obj in jsonElement.EnumerateObject())
            {
                var k = obj.Name;
                var value = readValue(obj.Value);
                expandoObject[k] = value;
            }
            return expandoObject;
        }

        private object? readValue(JsonElement jsonElement)
        {
            object? result = null;
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Object:
                    result = readObject(jsonElement);
                    break;
                case JsonValueKind.Array:
                    result = readList(jsonElement);
                    break;
                case JsonValueKind.String:
                    //TODO: Missing Datetime&Bytes Convert
                    result = jsonElement.GetString();
                    break;
                case JsonValueKind.Number:
                    //TODO: more num type
                    result = 0;
                    if (jsonElement.TryGetInt64(out long l))
                    {
                        result = l;
                    }
                    else if (jsonElement.TryGetDouble(out double d))
                    {
                        result = d;
                    }
                    break;
                case JsonValueKind.True:
                    result = true;
                    break;
                case JsonValueKind.False:
                    result = false;
                    break;
                case JsonValueKind.Undefined:
                case JsonValueKind.Null:
                    result = null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return result;
        }

        private object? readList(JsonElement jsonElement)
        {
            var list = new List<object?>();
            foreach (var item in jsonElement.EnumerateArray())
            {
                list.Add(readValue(item));
            }
            return list.Count == 0 ? null : list;
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            // writer.WriteStringValue(value.ToString());
        }
    }
}
