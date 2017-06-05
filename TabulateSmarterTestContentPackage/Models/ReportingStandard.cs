using System.Collections.Generic;
using System.Linq;

namespace TabulateSmarterTestContentPackage.Models
{
    public class ReportingStandard
    {
        public ReportingStandard(IList<ItemStandard> primary, IList<ItemStandard> secondary)
        {
            PrimaryCommonCore = primary.Select(x => x.Standard).Aggregate((x, y) => $"{x};{y}");
            PrimaryClaimsContentTargets = CombineClaimsContentTargets(primary);
            if (secondary.Any())
            {
                SecondaryCommonCore = secondary.Select(x => x.Standard).Aggregate((x, y) => $"{x};{y}");
                SecondaryClaimsContentTargets = CombineClaimsContentTargets(secondary);
            }
        }

        public string PrimaryCommonCore { get; set; }
        public string PrimaryClaimsContentTargets { get; set; }
        public string SecondaryCommonCore { get; set; } = string.Empty;
        public string SecondaryClaimsContentTargets { get; set; } = string.Empty;

        private static string CombineClaimsContentTargets(IEnumerable<ItemStandard> itemStandards)
        {
            return itemStandards.Select(x => $"{x.Claim}|{x.ContentDomain}|{x.Target}")
                .Aggregate((x, y) => $"{x};{y}");
        }
    }
}