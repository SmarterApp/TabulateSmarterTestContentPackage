using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace ContentPackageTabulator.Extractors
{
    public static class CDataExtractor
    {
        public static IEnumerable<XCData> ExtractCData(XElement element)
        {
            return element?.DescendantNodes()
                .Where(x => x.NodeType == XmlNodeType.CDATA)
                .Cast<XCData>();
        }
    }
}