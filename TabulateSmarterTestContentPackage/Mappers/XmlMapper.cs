using System.Xml;
using System.Xml.Linq;

namespace TabulateSmarterTestContentPackage.Mappers
{
    public static class XmlMapper
    {
        public static XElement MapToXElement(this XmlDocument document)
        {
            return XDocument.Parse(document.OuterXml).Root;
        }
    }
}
