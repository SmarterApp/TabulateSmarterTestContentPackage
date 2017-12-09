using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TabulateSmarterTestContentPackage.Models
{
    public class ReportingStandard
    {
        public ReportingStandard(string primaryCCSS, string primaryClaimContentTarget, string secondaryCCSS, string secondaryClaimsContentTargets)
        {
            PrimaryCCSS = primaryCCSS;
            PrimaryClaimContentTarget = primaryClaimContentTarget;
            SecondaryCCSS = secondaryCCSS;
            SecondaryClaimsContentTargets = secondaryClaimsContentTargets;
        }

        public string PrimaryCCSS { get; set; }
        public string PrimaryClaimContentTarget { get; set; }
        public string SecondaryCCSS { get; set; }
        public string SecondaryClaimsContentTargets { get; set; }

    }
}