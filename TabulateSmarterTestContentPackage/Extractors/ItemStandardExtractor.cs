using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Extractors
{
    public static class ItemStandardExtractor
    {
        public static IEnumerable<ItemStandard> Extract(XElement metadata)
        {
            var sXmlNs = new XmlNamespaceManager(new NameTable());
            sXmlNs.AddNamespace("sa", "http://www.smarterapp.org/ns/1/assessment_item_metadata");
            return metadata.XPathSelectElements(".//sa:StandardPublication", sXmlNs)
                .Select(x => x.XPathSelectElement("./sa:PrimaryStandard", sXmlNs).Value.Split(':'))
                .Select(x => new ItemStandard
                {
                    Publication = x.Aggregate((y, z) => $"{y}:{z}"),
                    Claim = x.LastOrDefault()?.Split('|').FirstOrDefault() ?? string.Empty,
                    Target =
                        x.LastOrDefault()?
                            .Split('|')
                            .Skip(SkipForPublication(x.FirstOrDefault() ?? string.Empty))
                            .FirstOrDefault() ?? string.Empty
                });
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
        private static int SkipForPublication(string publication)
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
    }
}