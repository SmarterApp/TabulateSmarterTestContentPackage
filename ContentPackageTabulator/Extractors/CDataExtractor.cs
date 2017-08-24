using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ContentPackageTabulator.Extractors
{
    public static class CDataExtractor
    {
        public static IEnumerable<XCData> ExtractCData(XElement element, bool studentConentOnly = true)
        {
            if (!studentConentOnly)
            {
                return element?.DescendantNodes()
                    .Where(x => x.NodeType == XmlNodeType.CDATA)
                    .Cast<XCData>();
            }
            // By default, we only want the elements shown to the student
			var studentContent = element?.XPathSelectElements("itemrelease/item/content/stem")?
                                         .SelectMany(x => x.DescendantNodes())
			                             .Where(x => x.NodeType == XmlNodeType.CDATA)
										 .Cast<XCData>().ToList();
            studentContent.AddRange(element?.XPathSelectElements("itemrelease/item/content/optionlist/option/val")?
								   .SelectMany(x => x.DescendantNodes())
										 .Where(x => x.NodeType == XmlNodeType.CDATA)
										 .Cast<XCData>() ?? new List<XCData>());
            return studentContent;
        }
    }
}