using System.Text.Json;
using System.Text.Json.Serialization;

namespace exs.commons.Json
{
    public static class ConfigureJson
    {
        public static JsonSerializerOptions CreateDefaultConfiguration()
        {
            var conf = new JsonSerializerOptions();
            FillSerializerSettings(conf);
            return conf;
        }

        public static void FillSerializerSettings(JsonSerializerOptions settings)
        {
            settings.Converters.Add(new DateTime2UnixSerializer());
            settings.Converters.Add(new DateTimeNullable2UnixSerializer());
            settings.Converters.Add(new ValueTupleConverter());
            settings.Converters.Add(new TupleConverter());
            settings.Converters.Add(new OptionalJsonConverterFactory());
            settings.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
			settings.PropertyNameCaseInsensitive = true;
            settings.AllowTrailingCommas = true;
            settings.PropertyNamingPolicy = null;
            settings.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        }
    }
}
