using DotLiquid;
using exs.Commons.Utils;
using System.Collections;
using System.Dynamic;
using System.Reflection;
using System.Text.Json.Nodes;

namespace exs.commons.Templates
{
    public static class LiquidTemplate
    {
        public static void Init()
        {
            Template.RegisterFilter(typeof(LiquidFilters));
        }

        public static string Process(string template, IDictionary<string, object?>? fields)
        {
            return string.IsNullOrEmpty(template) ? "" : Template.Parse(template)
                .Render(fields != null ? LiquidParameters.FromObject(fields) : null);
        }

        public static string Process(string template, object fields)
        {
            return string.IsNullOrEmpty(template) ? "" : Template.Parse(template)
                .Render(fields != null ? LiquidParameters.FromObject(fields) : null);
        }
    }

    public static class LiquidFilters
    {
        public static string tzdate(object input, string fmt, int offset)
        {
            var dt = input as DateTime?;
            if (dt == null || dt == DateTime.MinValue) return "";
            dt = dt.Value.AddHours(offset);
            return string.IsNullOrEmpty(fmt) ? dt.ToString()! : dt.Value.ToString(fmt)!;
        }

        public static string tzunixdate(object input, string fmt, int offset)
        {
            var dt = input as long?;
            return tzdate(DateTimeHelper.UnixTimeToUtcDateTime(dt ?? 0), fmt, offset);
        }

        public static string udate(Context ctx, object input, string fmt)
        {
            var tz = ctx.HasKey("UserTimeZone") ? ctx["UserTimeZone"] as IIndexable : null;
            return input != null ? tzdate(input, fmt, tz != null ? Convert.ToInt16(tz["Offset"]) : 0) : "";
        }

        public static string uunixdate(Context ctx, object input, string fmt)
        {
            var tz = ctx.HasKey("UserTimeZone") ? ctx["UserTimeZone"] as IIndexable : null;
            return tzunixdate(input, fmt, tz != null ? Convert.ToInt16(tz["Offset"]) : 0);
        }
    }

    public class LiquidParameters
    {
        public LiquidParameters() { }
        public LiquidParameters(IDictionary<string, object?> dict)
        {
            Add(dict);
        }

        public static Hash FromObject(object obj) => FromDictionary(JsonHelper.ObjectToDictionary(obj));

        public static Hash FromDictionary(IDictionary<string, object?> dict) => new LiquidParameters(dict).ToHash();

        public void Clear() => Dict.Clear();
        public void Add(string key, object value) => Dict.Add(key, LiquidUtils.Liquidize(value));

        public void Add(IDictionary<string, object?> dict)
        {
            foreach (var en in dict)
            {
                Dict.Add(en.Key, LiquidUtils.Liquidize(en.Value));
            }
        }

        public Dictionary<string, object?> Dict { get; } = new Dictionary<string, object?>();

        public Hash ToHash() => Hash.FromDictionary(Dict);
    }

    class LiquidUtils
    {
        public static object? Liquidize(object? obj)
        {
            if (obj == null)
            {
                return null;
            }
            if (obj.GetType().GetTypeInfo().IsPrimitive)
            {
                return obj;
            }
            if (obj is string)
            {
                return obj;
            }
            if (obj is decimal)
            {
                return obj;
            }
            if (obj is DateTime)
            {
                return obj;
            }
            if (obj is DateTimeOffset)
            {
                return obj;
            }
            if (obj is TimeSpan)
            {
                return obj;
            }
            if (obj is Guid)
            {
                return obj;
            }
            if (obj is JsonObject jobj)
            {
                return new LiquidJObjectWrapper(jobj);
            }
            if (obj is IList ilist)
            {
                return new LiquidListWrapper(ilist);
            }
            //if (obj is JsonNode jv)
            //{
            //    return jv.as;
            //}
            if (obj is ExpandoObject xobj)
            {
                return new LiquidExpandoObjectWrapper(xobj);
            }
            return new LiquidObjectWrapper(obj);
        }
    }

    class LiquidEnumeratorWrapper : IEnumerator
    {
        public LiquidEnumeratorWrapper(IEnumerator enumerator)
        {
            mEnumerator = enumerator;
        }

        public object Current => LiquidUtils.Liquidize(mEnumerator.Current)!;
        public bool MoveNext() => mEnumerator.MoveNext();
        public void Reset() => mEnumerator.Reset();

        private readonly IEnumerator mEnumerator;
    }

    class LiquidListWrapper : IList
    {
        public LiquidListWrapper(IList list)
        {
            mList = list;
        }

        public object? this[int index] { get => LiquidUtils.Liquidize(mList[index])!; set => throw new NotImplementedException(); }

        public bool IsFixedSize => true;
        public bool IsReadOnly => true;
        public int Count => mList.Count;
        public bool IsSynchronized => mList.IsSynchronized;
        public object SyncRoot => mList.SyncRoot;

        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotImplementedException();
        public bool Contains(object? value) => mList.Contains(value);
        public void CopyTo(Array array, int index) => throw new NotImplementedException();

        public IEnumerator GetEnumerator() => new LiquidEnumeratorWrapper(mList.GetEnumerator());

        public int IndexOf(object? value) => mList.IndexOf(value);

        public void Insert(int index, object? value) => throw new NotImplementedException();
        public void Remove(object? value) => throw new NotImplementedException();
        public void RemoveAt(int index) => throw new NotImplementedException();

        private readonly IList mList;
    }

    class LiquidJObjectWrapper : IIndexable, ILiquidizable
    {
        public LiquidJObjectWrapper(JsonObject obj)
        {
            Source = obj;
        }

        public JsonObject Source { get; }

        public object? this[object key]
        {
            get => LiquidUtils.Liquidize(Source[key.ToString()!]);
        }

        public bool ContainsKey(object key)
        {
            return Source.ContainsKey(key.ToString()!);
        }

        public object ToLiquid() => this;
    }

    class LiquidExpandoObjectWrapper : IIndexable, ILiquidizable
    {
        public LiquidExpandoObjectWrapper(ExpandoObject obj)
        {
            Source = obj;
        }

        public ExpandoObject Source { get; }

        public object? this[object key]
        {
            get => LiquidUtils.Liquidize((Source as IDictionary<string, object?>)[key.ToString()!]);
        }

        public bool ContainsKey(object key)
        {
            return (Source as IDictionary<string, object?>).ContainsKey(key.ToString()!);
        }

        public object ToLiquid() => this;
    }

    class LiquidObjectWrapper : Drop
    {
        public LiquidObjectWrapper(object obj)
        {
            Source = obj;
        }

        public object Source { get; }

        public override object? this[object key]
        {
            get
            {
                var prop = Source.GetType().GetProperties().FirstOrDefault(
                    e => e.Name.Equals(key?.ToString(), StringComparison.InvariantCultureIgnoreCase));
                if (prop != null)
                {
                    var value = prop.GetMethod?.Invoke(Source, null) ?? null;
                    return LiquidUtils.Liquidize(prop.GetMethod?.Invoke(Source, null));
                }
                return null;
            }
        }

        public override bool ContainsKey(object key)
        {
            return Source.GetType().GetProperties().Any(e => e.Name.Equals(key?.ToString(), StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
