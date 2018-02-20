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

        public static IReadOnlyList<ItemStandard> Extract(ItemIdentifier ii, IXPathNavigable metadata)
        {
            // Get the primary standard
            var result = new List<ItemStandard>();
            Extract(ii, metadata, "PrimaryStandard", result);
            if (result.Count == 0)
            {
                ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "No PrimaryStandard found in metadata.");
            }
            if (result.Count != 1)
            {
                ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Found more than one PrimaryStandard.", $"count='{result.Count}'");
            }

            // Get any secondary standards
            Extract(ii, metadata, "SecondaryStandard", result);

            // Do not return an empty result - make a blank one if necessary
            if (result.Count == 0)
            {
                result.Add(new ItemStandard());
            }

            return result;
        }

        private static void Extract(ItemIdentifier ii, IXPathNavigable metadata, string standard, List<ItemStandard> result)
        {
            XPathNavigator root = metadata.CreateNavigator();
            ItemStandard std = new ItemStandard(); // A new standard has empty string for all values
            HashSet<string> stdEncountered = new HashSet<string>();
            HashSet<string> pubEncountered = new HashSet<string>();

            // Look at all values for the specified Primary or Secondary standard.
            // Merge values if different publications. Add values if same publication.
            foreach (XPathNavigator node in root.Select($".//sa:{standard}", s_nsMetadata))
            {
                // Check whether we have processed this standard yet. Skip if so.
                if (!stdEncountered.Add(node.Value))
                {
                    continue;
                }

                var parts = node.Value.Split(c_standardDelimiters);
                if (parts.Length < 2)
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, $"{standard} metadata does not match expected format.", $"standard='{node.Value}'");
                    continue;
                }

                // If this publication has been encountered and the standard is not empty
                // add existing value to the list
                if (pubEncountered.Contains(parts[0]))
                {
                    if (!std.IsEmpty)
                    {
                        result.Add(std);
                        std = new ItemStandard();
                    }
                    pubEncountered.Clear();
                }

                // Set the common field
                if (string.IsNullOrEmpty(std.Standard))
                {
                    std.Standard = node.Value;
                }
                else
                {
                    std.Standard = string.Concat(std.Standard, ";", node.Value);
                }

                // Parse out the standard according to which publication
                switch (parts[0])
                {
                    case "SBAC-MA-v4":
                    case "SBAC-MA-v5":
                        std.Subject = cSubjectMath;
                        SetCheckMatch(ii, "Claim", std.Standard, ref std.Claim, parts, 1);
                        SetCheckMatch(ii, "ContentDomain", std.Standard, ref std.ContentDomain, parts, 2);
                        SetTargetCheckMatch(ii, std.Standard, std, parts, 3);
                        SetCheckMatch(ii, "Emphasis", std.Standard, ref std.Emphasis, parts, 4);
                        SetCheckMatch(ii, "CCSS", std.Standard, ref std.CCSS, parts, 5);
                        break;

                    case "SBAC-MA-v6":
                        std.Subject = cSubjectMath;
                        SetCheckMatch(ii, "Claim", std.Standard, ref std.Claim, parts, 1);
                        SetCheckMatch(ii, "ContentCategory", std.Standard, ref std.ContentCategory, parts, 2);
                        SetCheckMatch(ii, "TargetSet", std.Standard, ref std.TargetSet, parts, 3);
                        SetTargetCheckMatch(ii, std.Standard, std, parts, 4);
                        break;

                    case "SBAC-ELA-v1":
                        std.Subject = cSubjectEla;
                        SetCheckMatch(ii, "Claim", std.Standard, ref std.Claim, parts, 1);
                        SetTargetCheckMatch(ii, std.Standard, std, parts, 2);
                        SetCheckMatch(ii, "CCSS", std.Standard, ref std.CCSS, parts, 3);
                        break;
                }

                pubEncountered.Add(parts[0]);

            }
            if (!std.IsEmpty)
            {
                result.Add(std);
            }
        }

        private static void SetCheckMatch(ItemIdentifier ii, string fieldName, string standard, ref string rDest, string[] vals, int index)
        {
            if (vals.Length <= index)
            {
                return;
            }
            SetCheckMatch(ii, fieldName, standard, ref rDest, vals[index]);
        }

        private static void SetCheckMatch(ItemIdentifier ii, string fieldName, string standard, ref string rDest, string value)
        {
            if (string.IsNullOrEmpty(rDest))
            {
                rDest = value;
                return;
            }
            if (!string.Equals(rDest, value, System.StringComparison.Ordinal))
            {
                ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, $"Standard publications specify conflicting metadata.", $"property='{fieldName}' val1='{rDest}' val1='{value}' standards='{standard}'");
            }
        }

        private static void SetTargetCheckMatch(ItemIdentifier ii, string standard, ItemStandard std, string[] vals, int index)
        {
            if (vals.Length <= index) return;

            // Target may have a grade level suffix. If so, separate it and set both parts
            string target = vals[index];
            string grade;
            int tlen = target.Length;
            int cursor = target.Length;
            while (cursor > 0 && char.IsDigit(target[cursor - 1])) --cursor;
            if (cursor > 0 && target[cursor - 1] == '-')
            {
                cursor--;
                grade = target.Substring(cursor + 1);
                target = target.Substring(0, cursor);
            }
            else
            {
                grade = string.Empty;   // When target doesn't have a suffix, grade is empty
            }

            SetCheckMatch(ii, "Target", standard, ref std.Target, target);
            SetCheckMatch(ii, "Grade", standard, ref std.Grade, grade);
        }


        public static ReportingStandard ValidateAndSummarize(ItemIdentifier ii, IReadOnlyList<ItemStandard> standards, string expectedSubject, string expectedGrade)
        {
            // Validate each of the standards in the list
            foreach (var standard in standards)
            {
                // Validate claim
                if (!sValidClaims.Contains(standard.Claim))
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Degraded, "Unexpected claim value (should be 1, 2, 3, or 4 with possible suffix).", $"Claim='{standard.Claim}'");
                }

                // Validate subject
                if (!standard.Subject.Equals(expectedSubject, StringComparison.OrdinalIgnoreCase))
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                        "Metadata standard publication indicates subject different from item.",
                        $"ItemAttributeSubject='{expectedSubject}' MetadataSubject='{standard.Subject}'");
                }

                // Validate grade (derived from target suffix)
                if (!standard.Grade.Equals(expectedGrade, System.StringComparison.Ordinal) && Program.gValidationOptions.IsEnabled("tgs"))
                {
                    if (string.IsNullOrEmpty(standard.Grade))
                    {
                        ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                            "Grade level target suffix not included in standard reference.",
                            $"ItemAttributeGrade='{expectedGrade}' StandardString='{standard.Standard}'");
                    }
                    else
                    {
                        ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                            "Target suffix indicates a different grade from item attribute.",
                            $"ItemAttributeGrade='{expectedGrade}' TargetSuffixGrade='{standard.Grade}' StandardString='{standard.Standard}'");
                    }
                }
            }

            // === Extract the Primary CCSS ===

            //   Special case for Math claims 2,3,4. In those cases the primary CCSS is
            //   supplied on a secondary standard string with claim 1.
            string primaryCCSS = string.Empty;
            int primaryCcssIndex = -1;
            if (standards[0].Subject.Equals(cSubjectMath, StringComparison.OrdinalIgnoreCase)
                && standards[0].Claim.Length > 0 && standards[0].Claim[0] >= '2' && standards[0].Claim[0] <= '4')
            {
                // If empty CCSS (which should be the case) find the CCSS on a claim 1 standard
                if (string.IsNullOrEmpty(standards[0].CCSS)
                    || standards[0].CCSS.Equals(cValueNA, StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 1; i < standards.Count; ++i)
                    {
                        if (standards[i].Claim.StartsWith("1")
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
                    "CCSS standard is missing from item.", $"claim='{standards[0].Claim}' standard='{standards[0].Standard}'");
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
        public static string CombineClaimsContentTargets(IReadOnlyList<ItemStandard> itemStandards, int first, int count = 500)
        {
            int end = first + count;
            if (end > itemStandards.Count) end = itemStandards.Count;
            var result = new System.Text.StringBuilder();
            for (int i = first; i < end; ++i)
            {
                if (result.Length > 0) result.Append(';');
                if (itemStandards[i].Subject.Equals(cSubjectMath, StringComparison.Ordinal))
                {
                    result.Append($"{itemStandards[i].Claim}|{itemStandards[i].ContentDomain}|{itemStandards[i].Target}");
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