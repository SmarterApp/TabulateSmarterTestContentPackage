using System.IO;
using System.Xml.Linq;

namespace TabulateSmarterTestContentPackage.Extensions
{
    public static class XDocumentExtensions
    {
        public static XDocument LoadXml(this XDocument document, string xml)
        {
            using (var reader = new StringReader(xml))
            {
                return XDocument.Load(reader);
            }
        }
    }
}