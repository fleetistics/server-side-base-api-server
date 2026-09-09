using System;
using System.Collections.Generic;
using System.Text;

namespace exs.commons.Utils
{
    public class SymbolKeyGenerator
    {
        const string mShortLettersOnly = "ABCDEFGHJKMNPRSTUVWXYZ";

        static public string GenerateSymbolKey(int length = 8)
        {
            var random = new Random();
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = mShortLettersOnly[random.Next(mShortLettersOnly.Length)];
            }
            return new string(chars);
        }
    }
}
