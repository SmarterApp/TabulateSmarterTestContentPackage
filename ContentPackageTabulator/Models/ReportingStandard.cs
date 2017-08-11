using System.Collections.Generic;
using System.Linq;

namespace ContentPackageTabulator.Models
{
    public class ReportingStandard
    {
        public ReportingStandard(IList<ItemStandard> primary, IList<ItemStandard> secondary)
        {
            if (primary.Any(x => !string.IsNullOrEmpty(x.Standard) && !x.Standard.Equals("NA")))
            {
                PrimaryCommonCore =
                    primary.Select(x => !string.IsNullOrEmpty(x.Standard) ? $"{x.Standard}\t" : string.Empty)
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Aggregate((x, y) => $"{x};{y}");
            }
            if (primary.Any() && primary.Count > 1)
            {
                var second = primary.Last();
                primary.Remove(second);
                var tempList = new List<ItemStandard> {second};
                tempList.AddRange(secondary);
                secondary = tempList;
            }
            PrimaryClaimsContentTargets = CombineClaimsContentTargets(primary);

            if (secondary.Any(x => !string.IsNullOrEmpty(x.Standard) && !x.Standard.Equals("NA")))
            {
                SecondaryCommonCore =
                    secondary.Where(x => !x.Standard.Equals("NA"))
                        .Select(x => !string.IsNullOrEmpty(x.Standard) ? $"{x.Standard}\t" : string.Empty)
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Aggregate((x, y) => $"{x};{y}");
            }
            SecondaryClaimsContentTargets = CombineClaimsContentTargets(secondary);
        }

        public string PrimaryCommonCore { get; set; }
        public string PrimaryClaimsContentTargets { get; set; }
        public string SecondaryCommonCore { get; set; } = string.Empty;
        public string SecondaryClaimsContentTargets { get; set; } = string.Empty;

        private static string CombineClaimsContentTargets(IList<ItemStandard> itemStandards)
        {
            return itemStandards.Any()
                ? itemStandards.Select(x => !x.Publication.Equals("SBAC-ELA-v1")
                        ? $"{x.Claim}|{x.ContentDomain}|{x.Target}"
                        : $"{x.Claim}|{x.Target}")
                    .Aggregate((x, y) => $"{x};{y}")
                : string.Empty;
        }
    }
}