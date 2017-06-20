using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace TabulateSmarterTestContentPackage.Extractors
{
    public static class CDataExtractor
    {
        public static IEnumerable<XCData> ExtractCData(XElement document)
        {
            return document.DescendantNodes()
                .Where(x => x.NodeType == XmlNodeType.CDATA)
                .Cast<XCData>();
        }
    }
}