using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ContentPackageTabulator.Extensions
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

        public static string GetAttribute(this XElement element, string attributeName)
        {
            return
                element?.Attributes()
                    .FirstOrDefault(x => x.Name.LocalName.Equals(attributeName, StringComparison.OrdinalIgnoreCase))
                    ?.Value ?? string.Empty;
        }
    }
}