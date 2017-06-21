using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using NLog;
using TabulateSmarterTestContentPackage.Extensions;

namespace TabulateSmarterTestContentPackage.Validators
{
    public static class CDataValidator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static bool IsValid(XCData cData)
        {
            if (cData == null)
            {
                Logger.Error("invalid input");
                return false;
            }
            try
            {
                var cDataSection = new XDocument().LoadXml(cData.Value);

                // There is no way to predict where the images will appear in the CData (if they appear at all)
                // use a global selector.
                var imgTags = cDataSection.XPathSelectElements("//img");

                return imgTags.All(ImgElementHasValidAltTag)
                       && ElementsFreeOfColorAlterations(cDataSection.Root)
                       && ElementsFreeOfViolatingStyleTags(cDataSection.Root, new List<string>
                       {
                           "color",
                           "background",
                           "background-color",
                           "border",
                           "border-bottom-color",
                           "border-color",
                           "border-left",
                           "border-left-color",
                           "border-right",
                           "border-right-color",
                           "border-top",
                           "box-shadow",
                           "column-rule",
                           "column-rule-color",
                           "filter",
                           "font",
                           "opacity",
                           "outline",
                           "outline-color",
                           "text-decoration",
                           "text-decoration-color",
                           "text-shadow"
                       });
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
            return false;
        }

        public static bool ElementsFreeOfColorAlterations(XElement rootElement)
        {
            if (rootElement == null)
            {
                Logger.Error("CData section provided to color validation does not have valid content.");
                return false;
            }
            var restrictedValues = new List<string>
            {
                "color",
                "bgcolor"
            };
            return restrictedValues.All(x => ReportElementsInViolation(rootElement, "//*", x));
        }

        public static bool ReportElementsInViolation(XElement rootElement, string path, string attribute)
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
                });
            return false;
        }

        public static bool ElementsFreeOfViolatingStyleTags(XElement rootElement, IEnumerable<string> restrictedCss)
        {
            var result = rootElement.ElementsByPathAndAttributeCaseInsensitive("//*", "style").ToList();
            var isValid = true;
            if (result.Any())
            {
                restrictedCss.ToList().ForEach(x =>
                    result.ToList().Where(y => y.Attributes()
                        .First(z => z.Name.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase))
                        .Value.Equals(x, StringComparison.OrdinalIgnoreCase)).ToList().ForEach(y =>
                    {
                        var errorText =
                            $"Element {y.Name.LocalName} in CData contains an illegal CSS marker {x} in its 'style' attribute";
                        if (x.ToLower().Contains("color"))
                        {
                            Logger.Error(errorText);
                        }
                        else
                        {
                            Logger.Warn(errorText);
                        }
                        isValid = false;
                    })
                );
            }
            return isValid;
        }

        //<summary>This method takes a <img> element tag and determines whether
        //the provided <img> element contains a valid "alt" attribute </summary>
        //<param name="image"> The <img> tag to be validated </param>
        public static bool ImgElementHasValidAltTag(XElement imageElement)
        {
            if (imageElement == null)
            {
                Logger.Error("stub");
                return false;
            }
            if (!imageElement.HasAttributes)
            {
                Logger.Error("stub");
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
                Logger.Error("no valid alt tag");
                return false;
            }
            if (string.IsNullOrEmpty(altTag.Value))
            {
                Logger.Error("Alt tag present, but value is not valid");
                return false;
            }
            return true;
        }
    }
}