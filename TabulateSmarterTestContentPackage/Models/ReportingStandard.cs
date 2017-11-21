using System;
using System.Collections.Generic;
using System.Linq;

namespace TabulateSmarterTestContentPackage.Models
{
    public class ReportingStandard
    {
        public ReportingStandard(IReadOnlyList<ItemStandard> primary, IReadOnlyList<ItemStandard> secondary)
        {
            // If more than one primary standard, move it to the secondary list
            // (Note that the standard extractor will prevent duplicates)
            if (primary.Count > 1)
            {
                var newPrimary = new List<ItemStandard> { primary[0] };
                var newSecondary = new List<ItemStandard>(primary);
                newSecondary.RemoveAt(0);
                newSecondary.AddRange(secondary);
                primary = newPrimary;
                secondary = newSecondary;
            }

            if (primary.Any(x => !string.IsNullOrEmpty(x.CCSS) && !x.CCSS.Equals("NA")))
            {
                PrimaryCommonCore =
                    primary.Select(x => !string.IsNullOrEmpty(x.CCSS) ? $"{x.CCSS}\t" : string.Empty)
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Aggregate((x, y) => $"{x};{y}");
            }
            PrimaryClaimsContentTargets = CombineClaimsContentTargets(primary);

            if (secondary.Any(x => !string.IsNullOrEmpty(x.CCSS) && !x.CCSS.Equals("NA")))
            {
                SecondaryCommonCore =
                    secondary.Where(x => !x.CCSS.Equals("NA"))
                        .Select(x => !string.IsNullOrEmpty(x.CCSS) ? $"{x.CCSS}\t" : string.Empty)
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Aggregate((x, y) => $"{x};{y}");
            }
            SecondaryClaimsContentTargets = CombineClaimsContentTargets(secondary);
        }

        public string PrimaryCommonCore { get; set; }
        public string PrimaryClaimsContentTargets { get; set; }
        public string SecondaryCommonCore { get; set; } = string.Empty;
        public string SecondaryClaimsContentTargets { get; set; } = string.Empty;

        private static string CombineClaimsContentTargets(IReadOnlyList<ItemStandard> itemStandards)
        {
            return itemStandards.Any()
                ? itemStandards.Select(x => !x.Standard.StartsWith("SBAC-ELA-v1", StringComparison.Ordinal)
                        ? $"{x.Claim}|{x.ContentDomain}|{x.Target}"
                        : $"{x.Claim}|{x.Target}")
                    .Aggregate((x, y) => $"{x};{y}")
                : string.Empty;
        }
    }
}