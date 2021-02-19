using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VintageStory.ModPeek
{
    public static class Util
    {
        public static T[] Pop<T>(this T[] array)
        {
            T[] cut = new T[array.Length - 1];
            Array.Copy(array, cut, array.Length - 1);
            return cut;
        }
    }
}
