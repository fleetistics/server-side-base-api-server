namespace exs.Commons.Utils
{
    public static class THashKeyHelper
    {
        public static string BuildHashKey(long id)
        {
            ulong tmpID = (ulong)id;
            char[] charArray = new char[10];

            int curCharIdx;
            int idx = charArray.Count() - 1;
            ulong radix = (ulong)mSymbols.Length;
            while (tmpID != 0)
            {
                curCharIdx = (int)(tmpID % radix);
                charArray[idx--] = mSymbols[curCharIdx];
                tmpID = tmpID / radix;
            }
            while (idx >= 0) charArray[idx--] = ' ';
            return (new string(charArray)).Trim();
        }
        public static string PrepareKeyForSearch(string key)
        {
            return key.Trim().ToUpper();//.Replace('1', 'I').Replace('0', 'O')
        }
        static string mSymbols = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    }
}
