using exs.commons.Json;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace exs.Commons.Utils
{
    public static class JsonHelper
    {
        public static IDictionary<string, object?> ObjectToDictionary(object obj)
        {
            var config = ConfigureJson.CreateDefaultConfiguration();
            config.WriteIndented = false;
            config.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            var str = JsonSerializer.Serialize(obj, config);
            config = ConfigureJson.CreateDefaultConfiguration();
            config.Converters.Add(new JsonExpandoConverter());
            return (IDictionary<string, object?>)JsonSerializer.Deserialize<dynamic>(str, config)!;
        }

        public static ExpandoObject CloneExpandoObject(ExpandoObject obj)
        {
            var config = ConfigureJson.CreateDefaultConfiguration();
            config.WriteIndented = false;
            var json = JsonSerializer.Serialize(obj, config);
            config = ConfigureJson.CreateDefaultConfiguration();
            config.Converters.Add(new JsonExpandoConverter());
            return (ExpandoObject)JsonSerializer.Deserialize<dynamic>(json, config)!;
        }

        public static ExpandoObject DeepMerge(this ExpandoObject obj, ExpandoObject withObj)
        {
            foreach (var item in withObj)
            {
                if (item.Value == null) continue;
                if (item.Value is ExpandoObject exp)
                {
                    var exist = obj.GetValue<ExpandoObject>(item.Key);
                    if (exist != null) exist.DeepMerge(exp);
                    else obj.TryAdd(item.Key, DeepMerge(new ExpandoObject(), exp));
                }
                else obj.TryAdd(item.Key, item.Value);
            }
            return obj;
        }

        public static T? JsonStringToType<T>(string jsonStr)
        {
            return JsonSerializer.Deserialize<T>(jsonStr, ConfigureJson.CreateDefaultConfiguration());
        }

        public static T? JsonStreamToType<T>(Stream jsonStream)
        {
            return JsonSerializer.Deserialize<T>(jsonStream, ConfigureJson.CreateDefaultConfiguration());
        }

        public static ExpandoObject? ObjectToExpandoObject(object obj)
        {
            return JsonStringToExpandoObject(ToJsonMinimalString(obj));
        }

        public static ExpandoObject? JsonStringToExpandoObject(string jsonStr)
        {
            var conf = ConfigureJson.CreateDefaultConfiguration();
            conf.Converters.Add(new JsonExpandoConverter());
            return (ExpandoObject?)JsonSerializer.Deserialize<dynamic>(jsonStr, conf);
        }

        public static ExpandoObject? JsonStreamToExpandoObject(Stream jsonStream)
        {
            var conf = ConfigureJson.CreateDefaultConfiguration();
            conf.Converters.Add(new JsonExpandoConverter());
            return (ExpandoObject?)JsonSerializer.Deserialize<dynamic>(jsonStream, conf);
        }

        public static List<ExpandoObject>? JsonStringToExpandoObjectsList(string jsonStr)
        {
            var conf = ConfigureJson.CreateDefaultConfiguration();
            conf.Converters.Add(new JsonExpandoConverter());
            var result = JsonSerializer.Deserialize<dynamic>(jsonStr, conf);
            if (result != null) return null;
            if (result is List<object> list) return list.Cast<ExpandoObject>().ToList();
            return null;
        }

        public static List<ExpandoObject>? JsonStreamToExpandoObjectsList(Stream jsonStream)
        {
            var conf = ConfigureJson.CreateDefaultConfiguration();
            conf.Converters.Add(new JsonExpandoConverter());
            var result = JsonSerializer.Deserialize<dynamic>(jsonStream, conf);
            if (result != null) return null;
            if (result is List<object> list) return list.Cast<ExpandoObject>().ToList();
            return null;
        }

        public static string ToJsonIndentedString(object obj)
        {
            var config = ConfigureJson.CreateDefaultConfiguration();
            config.WriteIndented = true;
            return JsonSerializer.Serialize(obj, config);
        }

        public static string ToJsonString(object obj) => ToJsonIndentedString(obj);

        public static string ToJsonMinimalString(object obj)
        {
            var config = ConfigureJson.CreateDefaultConfiguration();
            config.WriteIndented = false;
            return JsonSerializer.Serialize(obj, config);
        }
    }
}
