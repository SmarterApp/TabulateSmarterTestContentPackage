using System.Linq;
using System.Xml.Linq;

namespace TabulateSmarterTestContentPackage.Utilities
{
    public static class XmlUtilities
    {
        public static XElement StripNamespace(XElement root)
        {
            return new XElement(
                root.Name.LocalName,
                root.HasElements
                    ? root.Elements().Select(StripNamespace)
                    : (object) root.Value
            );
        }
    }
}