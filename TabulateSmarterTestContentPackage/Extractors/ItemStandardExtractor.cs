using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;

namespace TabulateSmarterTestContentPackage.Extractors
{
    public static class ItemStandardExtractor
    {

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

        public static IReadOnlyList<ItemStandard> Extract(ItemIdentifier ii, IXPathNavigable metadata, string standard = "PrimaryStandard")
        {
            var result = new List<ItemStandard>();
            XPathNavigator root = metadata.CreateNavigator();
            ItemStandard std = new ItemStandard();
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

                // Parse out the standard according to which publication
                switch (parts[0])
                {
                    case "SBAC-MA-v4":
                    case "SBAC-MA-v5":
                        SetCheckMatch(ii, "Claim", ref std.Claim, parts, 1);
                        SetCheckMatch(ii, "ContentDomain", ref std.ContentDomain, parts, 2);
                        SetCheckMatch(ii, "Target", ref std.Target, parts, 3);
                        SetCheckMatch(ii, "Emphasis", ref std.Emphasis, parts, 4);
                        SetCheckMatch(ii, "CCSS", ref std.CCSS, parts, 5);
                        break;

                    case "SBAC-MA-v6":
                        SetCheckMatch(ii, "Claim", ref std.Claim, parts, 1);
                        SetCheckMatch(ii, "ContentCategory", ref std.ContentCategory, parts, 2);
                        SetCheckMatch(ii, "TargetSet", ref std.TargetSet, parts,3);
                        SetCheckMatch(ii, "Target", ref std.Target, parts, 4);
                        break;

                    case "SBAC-ELA-v1":
                        SetCheckMatch(ii, "Claim", ref std.Claim, parts, 1);
                        SetCheckMatch(ii, "Target", ref std.Target, parts, 2);
                        SetCheckMatch(ii, "CCSS", ref std.CCSS, parts, 3);
                        break;
                }

                // Set the common field
                std.Standard = node.Value;

                // Retrieve grade suffix from target (if present)
                {
                    int tlen = std.Target.Length;
                    if (tlen >= 3 && std.Target[tlen - 2] == '-' && char.IsDigit(std.Target[tlen - 1]))
                    {
                        std.Grade = std.Target.Substring(tlen - 1);
                    }
                }

                pubEncountered.Add(parts[0]);

            }
            if (!std.IsEmpty)
            {
                result.Add(std);
            }
            return result;
        }

        private static void SetCheckMatch(ItemIdentifier ii, string fieldName, ref string rDest, string[] vals, int index)
        {
            if (vals.Length <= index)
            {
                return;
            }
            if (string.IsNullOrEmpty(rDest))
            {
                rDest = vals[index];
                return;
            }
            if (!string.Equals(rDest, vals[index], System.StringComparison.Ordinal))
            {
                ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, $"Standard publications specify conflicting metadata.", $"property='{fieldName}' valA='{rDest}' valB='{vals[index]}'");
            }
        }

        public static void ValidateStandards(ItemIdentifier ii, IReadOnlyList<ItemStandard> standards, bool primary, string expectedGrade)
        {
            if (primary)
            {
                // Validate count
                if (standards.Count < 1)
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "No primary standard specified.");
                    return;
                }
                if (standards.Count > 1)
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Multiple primary standards specified.", $"count='{standards.Count()}'");
                }

                // Ensure CCSS is present
                if (string.IsNullOrEmpty(standards[0].CCSS))
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Common Core standard not included in PrimaryStandard metadata.");
                }
            }

            foreach (var standard in standards)
            {
                // Validate claim
                if (!sValidClaims.Contains(standard.Claim))
                {
                    ReportingUtility.ReportError(ii, ErrorCategory.Metadata, ErrorSeverity.Degraded, "Unexpected claim value (should be 1, 2, 3, or 4 with possible suffix).", $"Claim='{standard.Claim}'");
                }

                // Validate grade (derived from target suffix)
                if (!standard.Grade.Equals(expectedGrade, System.StringComparison.Ordinal))
                {
                    ReportingUtility.ReportError("tgs", ii, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                        "Target suffix indicates a different grade from item attribute.",
                        $"ItemAttributeGrade='{expectedGrade}' TargetSuffixGrade='{standard.Grade}'");
                }
            }
        }
    }
}