using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml.XPath;

namespace TabulateSmarterTestContentPackage.Extensions
{
    public static class XElementExtensions
    {
        public static IEnumerable<XElement> ElementsByPathAndAttributeCaseInsensitive(this XElement rootElement,
            string xPathBase, string attribute)
        {
            return rootElement.XPathSelectElements($"{xPathBase}{ApplyXmlCaseInsensitivityTransformation(attribute)}");
        }

        private static string ApplyXmlCaseInsensitivityTransformation(string pattern)
        {
            return $"[translate(@{pattern.ToLower()}, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')]";
        }
    }
}