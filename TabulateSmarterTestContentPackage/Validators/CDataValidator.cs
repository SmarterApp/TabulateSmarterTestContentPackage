using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using NLog;
using TabulateSmarterTestContentPackage.Extensions;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;

namespace TabulateSmarterTestContentPackage.Validators
{
    public static class CDataValidator
    {
        const string cColorContrast = "color contrast";
        const string cZoom = "zoom";
        const string cColorOrZoom = "color contrast or zoom";

        // Dictionaries map from attributes or styles to a description of what they interfere with.
        static Dictionary<string, string> s_prohibitedElements = new Dictionary<string, string>
        {
            { "font", cColorOrZoom }
        };

        static Dictionary<string, string> s_prohibitedAttributes = new Dictionary<string, string>
        {
            { "color", cColorContrast },
            { "bgcolor", cColorContrast }
        };

        static Dictionary<string, string> s_prohibitedStyleProperties = new Dictionary<string, string>
        {
            { "font", cColorOrZoom },
            { "background", cColorContrast },
            { "background-color", cColorContrast },
            { "color", cColorContrast }
        };

        static HashSet<string> s_styleSizeProperties = new HashSet<string>
        {
            "font-size",
            "line-height"
        };

        static HashSet<string> s_prohibitedUnitSuffixes = new HashSet<string>
        { "cm", "mm", "in", "px", "pt", "pc" };

        public static void ValidateItemContent(ItemContext it, IXPathNavigable html)
        {
            var root = html.CreateNavigator();
            ImgElementsHaveValidAltReference(it, root);

            ElementsFreeOfProhibitedAttributes(it, root);
        }

        public static bool ElementsFreeOfProhibitedAttributes(ItemContext it, XPathNavigator root)
        {
            bool valid = true;
            XPathNavigator ele = root.Clone();
            while (ele.MoveToFollowing(XPathNodeType.Element))
            {
                if (s_prohibitedElements.TryGetValue(ele.Name, out string interferesWith))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                        $"Item content has element that may interfere with {interferesWith}.", $"element='{ele.OuterXml}'");
                    valid = false;
                }

                var attribute = ele.Clone();
                if (attribute.MoveToFirstAttribute())
                {
                    do
                    {
                        // Check for prohibited attribute
                        if (s_prohibitedAttributes.TryGetValue(attribute.Name, out interferesWith))
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                $"Item content has attribute that may interfere with {interferesWith}.", $"attribute='{attribute.Name}' element='{ele.OuterXml}'");
                            valid = false;
                        }

                        // Check for prohibited style properties
                        else if (attribute.Name.Equals("style"))
                        {
                            string[] styleProps = attribute.Value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach(string prop in styleProps)
                            {
                                int ieq = prop.IndexOf(':');
                                string name;
                                string value;
                                if (ieq >= 0)
                                {
                                    name = prop.Substring(0, ieq).Trim().ToLower();
                                    value = prop.Substring(ieq + 1).Trim();
                                }
                                else
                                {
                                    name = prop.Trim().ToLower();
                                    value = string.Empty;
                                }

                                // Special case for "background-color". Transparent is acceptable.
                                if (name.Equals("background-color", StringComparison.Ordinal))
                                {
                                    if (!value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                            $"Item content has style property that may interfere with color contrast.", $"style='{name}' element='{ele.OuterXml}'");
                                    }
                                }

                                // Special handling for "font". Look for any component with a prohibited suffix
                                else if (name.Equals("font", StringComparison.Ordinal))
                                {
                                    foreach (string part in value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (HasProhibitedUnitSuffix(part))
                                        {
                                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                                $"Item content has style property that may interfere with zoom.", $"style='{name}' element='{ele.OuterXml}'");
                                        }
                                    }
                                }

                                // Check for prohibited style properties
                                else if (s_prohibitedStyleProperties.TryGetValue(name, out interferesWith))
                                {
                                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                        $"Item content has style property that may interfere with {interferesWith}.", $"style='{name}' element='{ele.OuterXml}'");
                                    valid = false;
                                }

                                // Check whether size properties use prohibited units
                                else if (s_styleSizeProperties.Contains(name))
                                {
                                    if (HasProhibitedUnitSuffix(value))
                                    {
                                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                            $"Item content has style property that may interfere with zoom.", $"style='{name}' element='{ele.OuterXml}'");
                                    }
                                }
                            }
                        }
                    }
                    while (attribute.MoveToNextAttribute());

                }
            }

            return valid;
        }

        public static bool ImgElementsHaveValidAltReference(ItemContext it, XPathNavigator root)
        {
            bool success = true;
            foreach (XPathNavigator imgEle in root.Select("//img"))
            {
                success &= ImgElementHasValidAltReference(it, imgEle);
            }
            return success;
        }

        //<summary>This method takes a <img> element tag and determines whether
        //the provided <img> element contains a valid "alt" attribute </summary>
        //<param name="image"> The <img> tag to be validated </param>
        public static bool ImgElementHasValidAltReference(ItemContext it, XPathNavigator imgEle)
        {
            string id = imgEle.GetAttribute("id", string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                    "Img element does not contain a valid id attribute necessary to provide alt text.", $"Value: {imgEle.OuterXml}");
                return false;
            }

            /* TODO: This is incomplete. It ensures that images have an ID but it still
               must make sure that the corresponding accessibility information is in the
               item XML that will supply alt text at runtime.
            
               Look up the corresponding apipAccessibility/acessibilityinfo/accessElement
               element in the item XML and ensure it has a readAloud/audioText element.
             */

            return true;
        }
                            
        private static bool HasProhibitedUnitSuffix(string value)
        {
            // Value should be a number for the magnitude followed by
            // letters indicating units.
            int split = 0;
            while (split < value.Length && (char.IsDigit(value[split]) || value[split] == '.')) ++split;
            string units = value.Substring(split).ToLower();

            return s_prohibitedUnitSuffixes.Contains(units);
        }

    }
}