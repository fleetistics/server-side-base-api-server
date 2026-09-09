using System.Dynamic;

namespace exs.Commons.Utils
{
    public static class ObjectHelper
    {
        public static T? TypeCast<T>(this object obj)
        {
            var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.DBNull:
                case TypeCode.Empty:
                    return default(T);
                case TypeCode.Boolean:
                    return (T)(object)Convert.ToBoolean(obj);
                case TypeCode.Char:
                    return (T)(object)Convert.ToChar(obj);
                case TypeCode.SByte:
                    return (T)(object)Convert.ToSByte(obj);
                case TypeCode.Byte:
                    return (T)(object)Convert.ToByte(obj);
                case TypeCode.Int16:
                    return (T)(object)Convert.ToInt16(obj);
                case TypeCode.UInt16:
                    return (T)(object)Convert.ToUInt16(obj);
                case TypeCode.Int32:
                    return (T)(object)Convert.ToInt32(obj);
                case TypeCode.UInt32:
                    return (T)(object)Convert.ToUInt32(obj);
                case TypeCode.Int64:
                    return (T)(object)Convert.ToInt64(obj);
                case TypeCode.UInt64:
                    return (T)(object)Convert.ToUInt64(obj);
                case TypeCode.Single:
                    return (T)(object)Convert.ToSingle(obj);
                case TypeCode.Double:
                    return (T)(object)Convert.ToDouble(obj);
                case TypeCode.Decimal:
                    return (T)(object)Convert.ToDecimal(obj);
                case TypeCode.DateTime:
                    return (T)(object)Convert.ToDateTime(obj);
                default:
                    return (T)obj;
            }
        }

        public static dynamic DeepMerge(object item1, object item2)
        {
            var res = new ExpandoObject() as IDictionary<string, object?>;
            foreach (var property in item1.GetType().GetProperties())
            {
                if (property.CanRead) res[property.Name] = property.GetValue(item1);
            }
            foreach (var property in item2.GetType().GetProperties())
            {
                if (property.CanRead)
                {
                    var obj2 = property.GetValue(item2);
                    if (res.ContainsKey(property.Name))
                    {
                        var obj1 = res[property.Name];
                        if (obj1 != null && obj2 != null) res[property.Name] = DeepMerge(obj1, obj2);
                        else res[property.Name] = obj2;
                    }
                    else res[property.Name] = obj2;
                }
            }
            return res;
        }
        public static void SetValue(this ExpandoObject obj, string path, object value)
        {
            var pathItems = path.Split('.');
            var n = pathItems.Length - 1;
            var objectToSet = (IDictionary<string, object?>)obj;
            for (int i = 0; i < n; i++)
            {
                IDictionary<string, object?> node;
                if (!objectToSet.ContainsKey(pathItems[i]))
                {
                    node = new ExpandoObject();
                    objectToSet.TryAdd(pathItems[i], node);
                }
                else
                {
                    if (objectToSet[pathItems[i]] is IDictionary<string, object?> d) node = d;
                    else throw new Exception($"Can't set value to {path}. {pathItems[i]} not an object");
                }
                objectToSet = node;
            }
            objectToSet.TryAdd(pathItems[n], value);
        }
        public static T? GetValue<T>(this ExpandoObject obj, string path, T? defaultValue = default(T))
        {
            var i = path.LastIndexOf('.');
            if (i > 0)
            {
                var prop = path.Substring(i + 1);
                ExpandoObject node = obj;
                bool wasFound = false;
                foreach (var n in path.Substring(0, i).Split(new char[] { '.' }).Select(n => n.Trim()).Where(n => n.Length > 0))
                {
                    wasFound = false;
                    foreach (var kvp in node)
                    {
                        if (string.Compare(kvp.Key, n, true) == 0)
                        {
                            if (kvp.Value != null && kvp.Value is ExpandoObject)
                            {
                                node = (ExpandoObject)kvp.Value;
                                wasFound = true;
                                break;
                            }
                            else return defaultValue;
                        }
                    }
                    if (!wasFound) return defaultValue;
                }
                foreach (var kvp in node)
                {
                    if (string.Compare(kvp.Key, prop, true) == 0)
                    {
                        if (kvp.Value != null)
                        {
                            return (T)kvp.Value;
                        }
                        else return defaultValue;
                    }
                }
            }
            else
            {
                foreach (var kvp in obj)
                {
                    if (string.Compare(kvp.Key, path, true) == 0)
                    {
                        if (kvp.Value != null)
                        {
                            return (T)kvp.Value;
                        }
                        else return defaultValue;
                    }
                }
            }
            return defaultValue;
        }
        public static void DeleteProperty(this object obj, string name)
        {
            if (obj is IDictionary<string, object?> d) d.Remove(name);
        }
        public static void DeleteProperty(this ExpandoObject obj, string name)
        {
            if (obj is IDictionary<string, object?> d) d.Remove(name);
        }
        public static ExpandoObject PopulateObjectByList(object objFrom, List<string> fieldsList, List<string>? fieldsMap = null)
        {
            var objTo = (IDictionary<string, object?>)new ExpandoObject();
            foreach (var prop in objFrom.GetType().GetProperties())
            {
                var pos = fieldsList.IndexOf(prop.Name.ToLower());
                if ( pos == -1 ) continue;
                if (fieldsMap != null) objTo.Add(fieldsMap[pos], prop.GetValue(objFrom));
                else objTo.Add(prop.Name, prop.GetValue(objFrom));
            }
            return (ExpandoObject)objTo;
        }
        public static ExpandoObject? Find(this ExpandoObject obj, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var kvp in obj)
            {
                if (kvp.Key.EqualsIgnoreCase(name)) return kvp.Value as ExpandoObject;
            }
            foreach (var kvp in obj)
            {
                var found = (kvp.Value as ExpandoObject)?.Find(name);
                if (found != null) return found;
            }
            return null;
        }
        public static ExpandoObject Update(this ExpandoObject obj, ExpandoObject withObj)
        {
            if (obj == null) return withObj;
            if (withObj == null) return obj;
            var dict = obj as IDictionary<string, object?>;
            foreach (var en in withObj)
            {
                if (dict.TryGetValue(en.Key, out var curValue) && curValue != null)
                {
                    if (en.Value != null && en.Value is ExpandoObject newExp && curValue is ExpandoObject curExp)
                    {
                        dict[en.Key] = Update(curExp, newExp);
                    }
                    else
                    {
                        dict[en.Key] = en.Value;
                    }
                }
                else
                {
                    obj.TryAdd(en.Key, en.Value);
                }
            }
            return obj;
        }
    }
}
