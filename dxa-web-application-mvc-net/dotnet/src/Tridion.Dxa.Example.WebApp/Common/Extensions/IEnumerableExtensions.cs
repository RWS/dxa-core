using System.Collections.Generic;
using System.Linq;

namespace Tridion.Dxa.Example.WebApp.Common.Extensions
{
    public static class IEnumerableExtensions
    {
        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> enumerable)
        {
            return enumerable ?? Enumerable.Empty<T>();
        }

        public static List<T> EmptyIfNull<T>(this List<T> list)
        {
            return list ?? new List<T>();
        }
    }
}
