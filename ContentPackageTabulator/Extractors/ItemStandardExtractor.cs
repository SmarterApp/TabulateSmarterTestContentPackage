using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using ContentPackageTabulator.Models;

namespace ContentPackageTabulator.Extractors
{
    public static class ItemStandardExtractor
    {
        public static IEnumerable<ItemStandard> Extract(XElement metadata, string standard = "PrimaryStandard")
        {
            var sXmlNs = new XmlNamespaceManager(new NameTable());
            sXmlNs.AddNamespace("sa", "http://www.smarterapp.org/ns/1/assessment_item_metadata");
            var XNodes = metadata.XPathSelectElements($".//sa:{standard}", sXmlNs).ToList();
            if (!XNodes.Any())
            {
                return new List<ItemStandard>();
            }
            var result = XNodes.Select(x => new
                {
                    Publication = x.Value.Split(':').FirstOrDefault(),
                    Metadata = x.Value.Split(':').LastOrDefault()?.Split('|')
                })
                .Where(x => !x.Publication.Equals("SBAC-MA-v6"))
                .Select(x => new ItemStandard
                {
                    Publication = x.Publication,
                    Standard =
                        x.Publication.Equals("SBAC-MA-v6") || x.Metadata.Length <= 2
                            ? string.Empty
                            : x.Metadata.LastOrDefault(),
                    Claim = x.Metadata.FirstOrDefault() ?? string.Empty,
                    Target =
                        x.Metadata.Skip(SkipToTargetForPublication(x.Publication ?? string.Empty))
                            .FirstOrDefault() ?? string.Empty,
                    ContentDomain = x.Publication.Equals("SBAC-ELA-v1")
                        ? string.Empty
                        : x.Metadata.Skip(SkipToContentDomainForPublication(x.Publication ?? string.Empty))
                              .FirstOrDefault() ?? string.Empty
                }).ToList();
            if (result.Count > 1 && standard.Equals("PrimaryStandard"))
            {
                return DeterminePrimaryStandard(result).ToList();
            }
            return result;
        }

        // Functionally, there are a maximum of two primary standards. If we have the v6 first and reverse we should get the good one
        private static IEnumerable<ItemStandard> DeterminePrimaryStandard(IList<ItemStandard> candidates)
        {
            return candidates.Count() > 1 && candidates.First().Standard.Equals("SBAC-MA-v6")
                ? candidates.Reverse()
                : candidates;
        }

        /* 
         * Locate and parse the standard, claim, and target from the metadata
         * 
         * Claim and target are specified in one of the following formats:
         * SBAC-ELA-v1 (there is only one alignment for ELA, this is used for delivery)
         *     Claim|Assessment Target|Common Core Standard
         * SBAC-MA-v6 (Math, based on the blueprint hierarchy, primary alignment and does not go to standard level, THIS IS USED FOR DELIVERY, should be the same as SBAC-MA-v4)
         *     Claim|Content Category|Target Set|Assessment Target
         * SBAC-MA-v5 (Math, based on the content specifications hierarchy secondary alignment to the standard level)
         *     Claim|Content Domain|Target|Emphasis|Common Core Standard
         * SBAC-MA-v4 (Math, based on the content specifications hierarchy primary alignment to the standard level)
         *     Claim|Content Domain|Target|Emphasis|Common Core Standard
         */
        // This is the index of the expected target in the PrimaryStandard node given a particular publication
        private static int SkipToTargetForPublication(string publication)
        {
            switch (publication)
            {
                case "SBAC-ELA-v1":
                    return 1;
                case "SBAC-MA-v4":
                case "SBAC-MA-v5":
                    return 2;
                case "SBAC-MA-v6":
                    return 3;
                default:
                    return 0;
            }
        }

        private static int SkipToContentDomainForPublication(string publication)
        {
            switch (publication)
            {
                case "SBAC-MA-v4":
                case "SBAC-MA-v5":
                case "SBAC-MA-v6":
                    return 1;
                default:
                    return 0;
            }
        }

        // If there are both a primary v4 and a v6, take the v4 because it has a common core standard
        // If there is only a primary v6, take that (no standard in this case)
        // Secondary standards/claims/targets are semicolon delimited in a seperate field

        // claim|content domain|target <-- Super fancy Alla format (semicolon separated)
    }
}