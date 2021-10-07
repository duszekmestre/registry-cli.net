using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace registry_cli.Extensions
{
    internal static class IEnumerableExtensions
    {
        internal static IEnumerable<string> FilterCollection(this IEnumerable<string> list, IEnumerable<string> filters)
        {
            if (filters?.Any() == true)
            {
                List<Regex> re = filters.Select(filter => new Regex(filter)).ToList();
                return list.Where(item => re.Any(r => r.IsMatch(item)));
            }

            return list;
        }
    }
}
