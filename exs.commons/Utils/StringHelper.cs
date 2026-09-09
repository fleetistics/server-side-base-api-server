using System.ComponentModel;

namespace exs.Commons.Utils
{
    public static class StringHelper
    {
        public static string Join(char ch, params string?[] list)
        {
            if (list == null || list.Length == 0) return "";
            return string.Join(ch, list.Where(s => !string.IsNullOrEmpty(s)));
        }

        public static string Join(string delim, params string?[] list)
        {
            if (list == null || list.Length == 0) return "";
            return string.Join(delim, list.Where(s => !string.IsNullOrEmpty(s)));
        }

        public static bool GenericTryParse<T>(this string input, out T? value)
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));

            if (converter != null && converter.IsValid(input))
            {
                try
                {
                    value = (T?)converter?.ConvertFromString(input) ?? default(T);
                    return true;
                }
                catch { }
            }
            value = default(T);
            return false;
        }

        public static T[] ParseIdsList<T>(this string? str, char delimiter = ',') where T : struct, IEquatable<T>
        {
            if (string.IsNullOrEmpty(str)) return Array.Empty<T>();
            return str.Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.GenericTryParse(out T id) ? id : default(T))
                    .Where(id => !default(T).Equals(id)).ToArray();
        }

        public static string Truncate(this string str, int maxLen)
        {
            return str.Length > maxLen ? str.Substring(0, maxLen) : str;
        }

		public static string TruncateWithDots(this string str, int maxLen)
		{
			if (string.IsNullOrEmpty(str) || str.Length <= maxLen)
				return str;

			if (maxLen <= 3)
				return str.Substring(0, maxLen);

			int targetLen = maxLen - 3;

			// Find the last whitespace before targetLen
			int lastSpace = str.LastIndexOf(' ', targetLen - 1, targetLen);
			if (lastSpace > 0)
			{
				return str.Substring(0, lastSpace) + "...";
			}
			return str.Substring(0, targetLen) + "...";
		}

		public static bool Equals(string? str1, string? str2)
        {
            if ( string.IsNullOrWhiteSpace(str1)) return string.IsNullOrWhiteSpace(str2);
            else
            {
                if (string.IsNullOrWhiteSpace(str2)) return false;
                else return str1.Equals(str2);
            }
        }
        public static bool EqualsIgnoreCase(this string? str1, string? str2)
        {
            if (str1 == null)
            {
                return str2 == null;
            }
            else if (str2 == null)
            {
                return false;
            }
            return str1.Equals(str2, StringComparison.OrdinalIgnoreCase);
        }

        public static string TrimPhone(string phone)
        {
            return phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        }

        public static byte[] FromHexString(string src)
        {
            int arrlen = (src.Length + 1) / 2;
            var result = new byte[arrlen];
            int pos = 0;
            for (int i = -(src.Length % 2); i < src.Length; i += 2)
            {
                char c1 = i >= 0 ? char.ToUpper(src[i]) : '0';
                char c2 = char.ToUpper(src[i + 1]);
                int b1 = Array.IndexOf(toHexLookup, c1);
                int b2 = Array.IndexOf(toHexLookup, c2);
                result[pos++] = (byte)((b1 << 4) | (b2 & 0xF));
            }
            return result;
        }

        public static Span<byte> FromHexString(string src, Span<byte> dst)
        {
            int pos = 0;
            for (int i = -(src.Length % 2); i < src.Length; i += 2)
            {
                char c1 = i >= 0 ? char.ToUpper(src[i]) : '0';
                char c2 = char.ToUpper(src[i + 1]);
                int b1 = Array.IndexOf(toHexLookup, c1);
                int b2 = Array.IndexOf(toHexLookup, c2);
                dst[pos++] = (byte)((b1 << 4) | (b2 & 0xF));
            }
            return dst.Slice(pos);
        }

        public static string ToHexString(byte[] data)
        {
            return ToHexString(data, "");
        }

        public static string ToHexString(byte[] data, string prefix)
        {
            if (data == null)
            {
                return "";
            }
            int i = 0, p = prefix.Length, l = data.Length;
            char[] c = new char[l * 2 + p];
            byte d;
            for (; i < p; ++i) c[i] = prefix[i];
            i = -1;
            --l;
            --p;
            while (i < l)
            {
                d = data[++i];
                c[++p] = toHexLookup[d >> 4];
                c[++p] = toHexLookup[d & 0xF];
            }
            return new string(c, 0, c.Length);
        }

        public static string ToHexString(ReadOnlySpan<byte> data)
        {
            return ToHexString(data, "");
        }

        public static string ToHexString(ReadOnlySpan<byte> data, string prefix)
        {
            if (data.IsEmpty)
            {
                return "";
            }
            int i = 0, p = prefix.Length, l = data.Length;
            char[] c = new char[l * 2 + p];
            byte d;
            for (; i < p; ++i) c[i] = prefix[i];
            i = -1;
            --l;
            --p;
            while (i < l)
            {
                d = data[++i];
                c[++p] = toHexLookup[d >> 4];
                c[++p] = toHexLookup[d & 0xF];
            }
            return new string(c, 0, c.Length);
        }

        private static char[] toHexLookup = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
    }
}
