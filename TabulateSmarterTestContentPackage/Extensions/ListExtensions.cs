using System.Collections.Generic;
using System.Linq;

namespace TabulateSmarterTestContentPackage.Extensions
{
    public static class ListExtensions
    {
        public static string AggregatePropertyWithSeparator<TE>(this IList<TE> list, string propertyName,
            string separator)
        {
            return list.Select(x => typeof(TE).GetProperty(propertyName).GetValue(x, null).ToString())
                .Aggregate((x, y) => $"{x}{separator}{y}");
        }
    }
}