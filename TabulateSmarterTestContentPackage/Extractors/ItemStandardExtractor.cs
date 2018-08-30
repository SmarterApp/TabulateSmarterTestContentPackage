using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;

namespace TabulateSmarterTestContentPackage.Extractors
{
    public static class ItemStandardExtractor
    {
        const string cSubjectMath = "MATH";
        const string cSubjectEla = "ELA";
        const string cValueNA = "NA";

        static XmlNamespaceManager s_nsMetadata;

        static char[] c_standardDelimiters = new char[] { ':', '|' };

        static readonly HashSet<string> sValidClaims = new HashSet<string>(new[] {
                "1",
                "1-LT",
                "1-IT",
                "2",
                "2-W",
                "3",
                "3-L",
                "3-S",
                "4",
                "4-CR"
        });

        static ItemStandardExtractor()
        {
            s_nsMetadata = new XmlNamespaceManager(new NameTable());
            s_nsMetadata.AddNamespace("sa", "http://www.smarterapp.org/ns/1/assessment_item_metadata");
        }

        public static IReadOnlyList<SmarterApp.ContentSpecId> Extract(ItemIdentifier ii, string strGrade, IXPathNavigable metadata)
        {
            var grade = SmarterApp.ContentSpecId.ParseGrade(strGrade);
            if (grade == SmarterApp.ContentSpecGrade.Unspecified)
            {
                ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Invalid grade attribute.", $"grade='{strGrade}'");
            }

            XPathNavigator root = metadata.CreateNavigator();

            List<SmarterApp.ContentSpecId> result = null;

            // Enumerate each standard publication
            foreach (XPathNavigator publication in root.Select($".//sa:StandardPublication", s_nsMetadata))
            {
                string pubName = (publication.Evaluate("string(sa:Publication)", s_nsMetadata) as string) ?? string.Empty;
                var pubStandards = new List<SmarterApp.ContentSpecId>();

                Extract(ii, grade, publication, "PrimaryStandard", pubStandards);
                if (pubStandards.Count == 0)
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "No PrimaryStandard found for StandardPublication.", $"publication='{pubName}'");
                }
                else if (pubStandards.Count != 1)
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Found more than one PrimaryStandard.", $"publication='{pubName}' count='{pubStandards.Count}'");
                }

                // Get any secondary standards
                Extract(ii, grade, publication, "SecondaryStandard", pubStandards);

                // If at least two standards, update the domain for the first
                if (pubStandards.Count >= 2)
                {
                    pubStandards[0].SetDomainFromClaimOneId(pubStandards[1]);
                }

                if (pubStandards.Count == 0)
                {
                    continue;
                }

                // If no result yet, set it
                if (result == null)
                {
                    result = pubStandards;
                }

                // Else merge and compare with prior publications
                else
                {
                    int count = Math.Min(result.Count, pubStandards.Count);
                    int i; // Counter persists after comparison
                    for (i=0; i<count; ++i)
                    {
                        // Replace the result if parse was more successful
                        if (result[i].ParseErrorSeverity > pubStandards[i].ParseErrorSeverity)
                        {
                            result[i] = pubStandards[i];
                        }
                        // If new standard was not a failure then compare
                        else if (pubStandards[i].ParseErrorSeverity != SmarterApp.ErrorSeverity.Invalid)
                        {
                            // If standards don't match then report an error.
                            if (!StandardsMatch(result[i], pubStandards[i]))
                            {
                                ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Standards from different publications don't match.",
                                    $"pub1='{result[i].ParseFormat}' pub2='{pubStandards[i].ParseFormat}' "
                                    + $"std1='{result[i].ToString(SmarterApp.ContentSpecIdFormat.Enhanced)}' std2='{pubStandards[i].ToString(SmarterApp.ContentSpecIdFormat.Enhanced)}'");
                            }

                            // If new publication has CCSS replace
                            // That's because some publication formats don't include CCSS
                            // and we want to make sure it's in place
                            if (!string.IsNullOrEmpty(pubStandards[i].CCSS))
                            {
                                result[i] = pubStandards[i];
                            }
                        }
                    }

                    // So long as there are more results in the new publication, add them
                    for (; i<pubStandards.Count; ++i)
                    {
                        result.Add(pubStandards[i]);
                    }
                }
            }

            // If at least two standards, update the domain for the first
            // (This was already done on pubStandards, but we do it again because lists may have been merged)
            if (result.Count >= 2)
            {
                result[0].SetDomainFromClaimOneId(result[1]);
            }

            // Do not return an empty result - make a blank one if necessary
            if (result == null)
            {
                result = new List<SmarterApp.ContentSpecId>();
                result.Add(new SmarterApp.ContentSpecId());
            }

            return result;
        }

        // Only match on Subject, Grade, Claim and Target.
        // (ContentSpecId.Equals includes domain and ccss in the comparison)
        private static bool StandardsMatch(SmarterApp.ContentSpecId a, SmarterApp.ContentSpecId b)
        {
            return a.Subject == b.Subject
                && a.Grade == b.Grade
                && a.Claim == b.Claim
                && string.Equals(a.Target, b.Target, StringComparison.Ordinal);
        }

        private static void Extract(ItemIdentifier ii, SmarterApp.ContentSpecGrade grade, XPathNavigator publication, string tag, List<SmarterApp.ContentSpecId> result)
        {
            foreach (XPathNavigator standard in publication.Select(string.Concat("sa:", tag), s_nsMetadata))
            {
                string stdStr = standard.Value ?? string.Empty;

                // Attempt to parse the standard
                var csid = SmarterApp.ContentSpecId.TryParse(stdStr, grade);
                if (csid.ParseErrorSeverity == SmarterApp.ErrorSeverity.Invalid)
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, $"{tag} failed to parse.", $"value='{stdStr}' err='{csid.ParseErrorDescription}'");
                }
                else if (csid.ParseErrorSeverity == SmarterApp.ErrorSeverity.Corrected)
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, $"{tag} has correctable error.", $"value='{stdStr}' err='{csid.ParseErrorDescription}'");
                }
                else if (csid.Validate() == SmarterApp.ErrorSeverity.Invalid)
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, $"{tag} validation error.", $"value='{stdStr}' err='{csid.ValidationErrorDescription}'");
                }

                //if (csid.ParseErrorSeverity != SmarterApp.ErrorSeverity.Invalid)
                {
                    result.Add(csid);
                }
            }
        }

        public static ReportingStandard Summarize(ItemIdentifier ii, IReadOnlyList<SmarterApp.ContentSpecId> standards, string expectedSubject, string expectedGrade)
        {
            // === Extract the Primary CCSS ===

            //   Special case for Math claims 2,3,4. In those cases the primary CCSS is
            //   supplied on a secondary standard string with claim 1.
            string primaryCCSS = string.Empty;
            int primaryCcssIndex = -1;
            if (standards[0].Subject == SmarterApp.ContentSpecSubject.Math
                && standards[0].Claim >= SmarterApp.ContentSpecClaim.C2 
                && standards[0].Claim <= SmarterApp.ContentSpecClaim.C4)
            {
                // If empty CCSS (which should be the case) find the CCSS on a claim 1 standard
                if (string.IsNullOrEmpty(standards[0].CCSS)
                    || standards[0].CCSS.Equals(cValueNA, StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 1; i < standards.Count; ++i)
                    {
                        if (standards[i].Claim == SmarterApp.ContentSpecClaim.C1
                            && !string.IsNullOrEmpty(standards[i].CCSS)
                            && !standards[i].CCSS.Equals(cValueNA, StringComparison.OrdinalIgnoreCase))
                        {
                            primaryCCSS = standards[i].CCSS;
                            primaryCcssIndex = i;
                            break;
                        }
                    }
                    if (primaryCcssIndex < 0)
                    {
                        ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                            "Math Claim 2, 3, 4 primary alignment should be paired with a claim 1 secondary alignment.", $"claim='{standards[0].Claim}'");
                    }
                    else if (string.IsNullOrEmpty(primaryCCSS) || primaryCCSS.Equals(cValueNA, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                            "Math Claim 2, 3, 4 primary alignment is missing CCSS standard.", $"claim='{standards[0].Claim}'");
                    }
                }
                else
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                        "Expected blank CCSS for Math Claim 2, 3, or 4", $"claim='{standards[0].Claim}' CCSS='{standards[0].CCSS}'");
                }
            }
            // Only accept value if it's non-empty and not NA
            else if (!string.IsNullOrEmpty(standards[0].CCSS)
                && !standards[0].CCSS.Equals(cValueNA, StringComparison.OrdinalIgnoreCase))
            {
                primaryCCSS = standards[0].CCSS;
                primaryCcssIndex = 0;
            }
            // Otherwise empty
            else
            {
                // primaryCCSS is already set to string.Empty;
                ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                    "CCSS standard is missing from item.", $"claim='{standards[0].Claim}' standard='{standards[0]}'");
            }

            // === Extract the Secondary CCSS ===
            var secondaryCcss = new StringBuilder();
            for (int i = 0; i < standards.Count; ++i)
            {
                if (i == primaryCcssIndex) continue;
                if (!string.IsNullOrEmpty(standards[i].CCSS)
                    && !standards[i].CCSS.Equals(cValueNA, StringComparison.OrdinalIgnoreCase))
                {
                    if (secondaryCcss.Length > 0) secondaryCcss.Append(';');
                    secondaryCcss.Append(standards[i].CCSS);
                }
            }

            // Return the summary value
            return new ReportingStandard(
                primaryCCSS,
                CombineClaimsContentTargets(standards, 0, 1),
                secondaryCcss.ToString(),
                CombineClaimsContentTargets(standards, 1));
        }

        // Formats claim content target for reporting app.
        public static string CombineClaimsContentTargets(IReadOnlyList<SmarterApp.ContentSpecId> itemStandards, int first, int count = 500)
        {
            int end = first + count;
            if (end > itemStandards.Count) end = itemStandards.Count;
            var result = new System.Text.StringBuilder();
            for (int i = first; i < end; ++i)
            {
                if (result.Length > 0) result.Append(';');
                if (itemStandards[i].Subject == SmarterApp.ContentSpecSubject.Math)
                {
                    result.Append($"{itemStandards[i].Claim}|{itemStandards[i].LegacyDomain}|{itemStandards[i].Target}");
                }
                else
                {
                    result.Append($"{itemStandards[i].Claim}|{itemStandards[i].Target}");
                }
            }

            return result.ToString();
        }



    }
}