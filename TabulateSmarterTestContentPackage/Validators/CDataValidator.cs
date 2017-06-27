using System;
using System.Collections.Generic;
using System.Linq;
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
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static bool IsValid(XCData cData, ItemContext itemContext)
        {
            if (cData == null)
            {
                Logger.Error("invalid input");
                return false;
            }
            try
            {
                // Adding a "<root> element parent to prevent cases where there are multiple XML roots in input
                var cDataSection = new XDocument().LoadXml($"<root>{cData.Value}</root>");

                // There is no way to predict where the images will appear in the CData (if they appear at all)
                // use a global selector.
                var imgTags = cDataSection.XPathSelectElements("//img");

                return imgTags.All(x => ImgElementHasValidAltTag(x, itemContext))
                       && ElementsFreeOfColorAlterations(cDataSection.Root, itemContext)
                       && ElementsFreeOfViolatingStyleTags(cDataSection.Root, new List<string>
                       {
                           "color",
                           "background",
                           //"background-color",
                           "border",
                           //"border-bottom-color",
                           //"border-color",
                           //"border-left",
                           //"border-left-color",
                           //"border-right",
                           //"border-right-color",
                           //"border-top",
                           //"border-top-color",
                           "box-shadow",
                           "column-rule",
                           //"column-rule-color",
                           "filter",
                           "font",
                           "opacity",
                           "outline",
                           //"outline-color",
                           "text-decoration",
                           //"text-decoration-color",
                           "text-shadow" //select-color, padding TODO: Greg - condense this list to common terms 'background' etc and only throw 1 error per item max
                       }, itemContext);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe, ex.Message);
            }
            return false;
        }

        public static bool ElementsFreeOfColorAlterations(XElement rootElement, ItemContext itemContext)
        {
            if (rootElement == null)
            {
                Logger.Error("CData section provided to color validation does not have valid content.");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe, "CData section provided to color validation does not have valid content.");
                return false;
            }
            var restrictedValues = new List<string>
            {
                "color",
                "bgcolor"
            };
            return restrictedValues.All(x => ReportElementsInViolation(rootElement, "//*", x, itemContext));
        }

        public static bool ReportElementsInViolation(XElement rootElement, string path, string attribute, ItemContext itemContext)
        {
            var result = rootElement.ElementsByPathAndAttributeCaseInsensitive(path, attribute).ToList();
            if (!result.Any())
            {
                return true;
            }
            result.ForEach(
                x =>
                {
                    Logger.Error(
                        $"CData element {x.Name.LocalName} matches an illegal pattern: {path}[@{attribute.ToLower()}]");
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe, $"CData element {x.Name.LocalName} matches an illegal pattern: {path}[@{attribute.ToLower()}]");
                });
            return false;
        }

        public static bool ElementsFreeOfViolatingStyleTags(XElement rootElement, IEnumerable<string> restrictedCss, ItemContext itemContext)
        {
            var result = rootElement.ElementsByPathAndAttributeCaseInsensitive("//*", "style").ToList();
            var isValid = true;
            if (result.Any())
            {
               var candidates = result.Select(x => new { Element = x, Style = x.Attributes()
                    .First(y => y.Name.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase))
                    .Value});
                candidates.ToList().ForEach(x =>
                {
                    var violations = restrictedCss.Where(x.Style.Contains).ToList();
                    if (violations.Any())
                    {
                        isValid = false;
                        var errorText =
                                $"Element '{x.Element.Name.LocalName}' in CData contains illegal CSS marker(s) '{violations.Aggregate((y,z) => $"{y},{z}")}' in its 'style' attribute. Value: {x.Element}";
                        Logger.Error(errorText);
                        ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe, errorText);
                    }
                });
            }
            return isValid;
        }

        //<summary>This method takes a <img> element tag and determines whether
        //the provided <img> element contains a valid "alt" attribute </summary>
        //<param name="image"> The <img> tag to be validated </param>
        public static bool ImgElementHasValidAltTag(XElement imageElement, ItemContext itemContext)
        {
            if (imageElement == null)
            {
                Logger.Error("Encountered unparsable image element");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded, "Encountered unparsable image element");
                return false;
            }
            if (!imageElement.HasAttributes)
            {
                Logger.Error($"Image element contains no attributes. Value: {imageElement}");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded, $"Image element contains no attributes. Value: {imageElement}");
                return false;
            }
            var altTag = imageElement.Attributes().Select(x =>
                new
                {
                    Name = x.Name.LocalName,
                    x.Value
                }).FirstOrDefault(x => x.Name.Equals("alt"));
            if (altTag == null)
            {
                Logger.Error($"Img element does not contain a valid alt attribute. Value: {imageElement}");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded, $"Img element does not contain a valid alt attribute. Value: {imageElement}");
                return false;
            }
            if (string.IsNullOrEmpty(altTag.Value))
            {
                Logger.Error($"Img tag's alt attribute is not valid. Value: {imageElement}");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded, $"Img tag's alt attribute is not valid. Value: {imageElement}");
                return false;
            }
            return true;
        }
    }
}