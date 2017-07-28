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
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static bool IsValid(XCData cData, ItemContext itemContext,
            ErrorSeverity errorSeverity = ErrorSeverity.Degraded)
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

                // Check to make sure all images have valid alt tags
                var imgTagsValid = imgTags.Select(x => ImgElementHasValidAltTag(x, itemContext, errorSeverity)).ToList();

                // Check for html attributes that modify color
                var noColorAlterations = ElementsFreeOfColorAlterations(cDataSection.Root, itemContext, errorSeverity);

                // Check for css elements inside style attributes that modify color (non-word specific colors)
                var noCssColorAlterationsPatterns = ElementsFreeOfViolatingStyleText(cDataSection.Root,
                    GetCssColorPatterns(),
                    itemContext, errorSeverity);

                // Check for css elements inside style attributes that modify color (word specific colors)
                var noCssColorAlterationsNames = ElementsFreeOfViolatingStyleText(cDataSection.Root,
                    GetCssColorCodes().ToDictionary(x => x, Regexify),
                    itemContext, errorSeverity);

                // Check for nested glossary tags in CData
                var noNestedGlossaryTags = CDataGlossaryTagsAreNotIllegallyNested(cDataSection.Root, itemContext,
                    errorSeverity);

                // Check for a whole host of glossary tag errors
                var noGlossaryTagErrors = CDataGlossaryTagStartAndEndTagsLineUpAppropriately(cDataSection.Root,
                    itemContext, errorSeverity);

                // Check to make sure all incidences of tagged items are tagged elsewhere
                var tagged = AllMatchingTermsTagged(cDataSection.Root, itemContext,
                    errorSeverity, noGlossaryTagErrors);


                return imgTagsValid.All(x => x)
                       && noColorAlterations
                       && noCssColorAlterationsPatterns
                       && noCssColorAlterationsNames;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded, ex.Message);
            }
            return false;
        }

        public static bool ElementsFreeOfColorAlterations(XElement rootElement, ItemContext itemContext,
            ErrorSeverity errorSeverity)
        {
            if (rootElement == null)
            {
                Logger.Error("CData section provided to color validation does not have valid content.");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded,
                    "CData section provided to color validation does not have valid content.");
                return false;
            }
            var restrictedValues = new List<string>
            {
                "color",
                "bgcolor"
            };
            return
                restrictedValues.All(x => ReportElementsInViolation(rootElement, "//*", x, itemContext, errorSeverity));
        }

        public static bool ReportElementsInViolation(XElement rootElement, string path, string attribute,
            ItemContext itemContext, ErrorSeverity errorSeverity)
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
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                        "CData element matches an illegal pattern",
                        $"Pattern: {path}[@{attribute.ToLower()}] Element: {x.Name.LocalName}");
                });
            return false;
        }

        public static bool ElementsFreeOfViolatingStyleText(XElement rootElement,
            IDictionary<string, string> restrictedPatterns,
            ItemContext itemContext, ErrorSeverity errorSeverity)
        {
            var isValid = true;
            var candidates = ExtractElementsWithCssStyling(rootElement);
            candidates.ForEach(x =>
            {
                var violations = restrictedPatterns.Keys.Where(y => Regex.IsMatch(x.Style, restrictedPatterns[y])).ToList();
                if (violations.Any())
                {
                    isValid = false;
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item,
                        errorSeverity, "CData css style tags contain restricted keywords or patterns",
                        $"Violation: [{violations.Aggregate((y, z) => $"{y},{z}")}] Element: {x.Element.Name.LocalName} Value: {x.Element}");
                }
            });

            return isValid;
        }

        public static List<CssElement> ExtractElementsWithCssStyling(XElement rootElement)
        {
            var elements = rootElement.ElementsByPathAndAttributeCaseInsensitive("//*", "style").ToList();
            var isValid = true;
            if (elements.Any())
            {
                return elements.Select(x => new CssElement
                {
                    Element = x,
                    Style = x.Attributes()
                        .First(y => y.Name.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase))
                        .Value
                }).ToList();
            }
            return new List<CssElement>();
        }

        //<summary>This method takes a <img> element tag and determines whether
        //the provided <img> element contains a valid "alt" attribute </summary>
        //<param name="image"> The <img> tag to be validated </param>
        public static bool ImgElementHasValidAltTag(XElement imageElement, ItemContext itemContext,
            ErrorSeverity errorSeverity)
        {
            if (imageElement == null)
            {
                Logger.Error("Encountered unparsable image element");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                    "Encountered unparsable image element");
                return false;
            }
            if (!imageElement.HasAttributes)
            {
                Logger.Error($"Image element contains no attributes. Value: {imageElement}");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                    "Image element contains no attributes", $"Value: {imageElement}");
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
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                    "Img element does not contain a valid alt attribute", $"Value: {imageElement}");
                return false;
            }
            if (string.IsNullOrEmpty(altTag.Value))
            {
                Logger.Error($"Img tag's alt attribute is not valid. Value: {imageElement}");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                    "Img tag's alt attribute is not valid",
                    $"Value: {imageElement}"
                );
                return false;
            }
            return true;
        }

        public static bool CDataGlossaryTagsAreNotIllegallyNested(XElement rootElement, ItemContext itemContext,
            ErrorSeverity errorSeverity)
        {
            var valid = true;
            var glossaryTags =
                rootElement.XPathSelectElements(
                    ".//span[@data-tag='word' and @data-tag-boundary='start']//span[@data-tag='word' and @data-tag-boundary='start']");
            glossaryTags.ToList().ForEach(x =>
            {
                Logger.Error($"Glossary tag {x} is nested illegally within another span tag");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                    "Glossary tag is nested illegally within another span tag",
                    $"Value: {x}");
                valid = false;
            });
            return valid;
        }

        public static IDictionary<string, int> CDataGlossaryTagStartAndEndTagsLineUpAppropriately(XElement rootElement,
            ItemContext itemContext,
            ErrorSeverity errorSeverity)
        {
            var result = new Dictionary<string, int>();

            var glossaryTags =
                rootElement.XPathSelectElements(".//span[@data-tag='word' and @data-tag-boundary='start']").ToList();
            glossaryTags.ForEach(x =>
            {
                ReportInappropriateGlossarySpanValues(x, itemContext, errorSeverity);
                var id =
                    x.Attributes()
                        .FirstOrDefault(y => y.Name.LocalName.Equals("id", StringComparison.OrdinalIgnoreCase))?
                        .Value;
                if (string.IsNullOrEmpty(id))
                {
                    // We have a span without an ID which is bad
                    Logger.Error($"Glossary start tag {x} does not have a required ID value");
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Wordlist, errorSeverity,
                        "Glossary start tag does not have a required ID value",
                        $"Tag: {x}");
                    return;
                }
                var siblings = x.NodesAfterSelf().ToList();

                // Ensure that the start tag has a matching sibling end tag before continuing
                if (siblings.All(y => !IsMatchingEndTag(y, id)))
                {
                    Logger.Error($"Glossary start tag '{x}' does not have a matching sibling end tag");
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Wordlist, errorSeverity,
                        "Glossary start tag does not have a matching sibling end tag",
                        $"Tag: {x}");
                    return;
                }
                var node = siblings.FirstOrDefault();
                var index = 0;

                // Empty glossary term
                if (IsMatchingEndTag(node, id))
                {
                    ReportInappropriateGlossarySpanValues(node.Cast(), itemContext, errorSeverity);
                    Logger.Error($"Glossary tag '{x}' does not have a value");
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Wordlist, errorSeverity,
                        "Glossary tag does not have a value",
                        $"Tag: {x}");
                    return;
                }

                var closingSpanTag = false;

                while (node != null)
                {
                    if (IsMatchingEndTag(node, id))
                    {
                        ReportInappropriateGlossarySpanValues(node.Cast(), itemContext, errorSeverity);
                        return;
                    }

                    var element = node.Cast();
                    // The first element we expect to find as a sibling is the closing span tag to match the opening one
                    if (element != null && element.Name.LocalName.Equals("span", StringComparison.OrdinalIgnoreCase) &&
                        element.IsEmpty)
                    {
                        closingSpanTag = true;
                        node = siblings.Count >= index ? siblings[index++] : null;
                        continue;
                    }
                    // We found a value nested in this span, which is illegal
                    if (!string.IsNullOrEmpty(element?.Value) && !closingSpanTag)
                    {
                        Logger.Error($"Glossary tag {x} has an illegally nested value '{element.Value}'");
                        ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                            "Glossary tag has an illegally nested value",
                            $"Tag: {x} Value: {element.Value}");
                        node = siblings.Count >= index ? siblings[index++] : null;
                        continue;
                    }
                    // There's a glossary term here
                    if (node.NodeType == XmlNodeType.Text)
                    {
                        if (result.ContainsKey(node.ToString()))
                        {
                            // build up the full glossary term
                            result[node.ToString()]++;
                        }
                        else
                        {
                            // add a new glossary entry
                            result.Add(node.ToString(), 1);
                        }
                        node = siblings.Count >= index ? siblings[++index] : null;
                        continue;
                    }
                    // Check for another opening tag (which means they are overlapping inappropriately)
                    if (IsStartingTag(node))
                    {
                        Logger.Error($"Glossary tag {element ?? node} overlaps with another tag {x}");
                        ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                            "Glossary tag overlaps with another tag",
                            $"Tag: {element ?? node} Overlaps: {x}");
                        node = siblings.Count >= index ? siblings[index++] : null;
                        return;
                    }
                    // We found something that shouldn't be here
                    Logger.Error($"Unrecognized element {element ?? node} encountered while processing siblings of {x}");
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                        "Unrecognized element encountered while processing sibling nodes in CData Glossary",
                        $"Element: {element ?? node} Base Node: {x}");
                    // We're going to continue processing
                    node = siblings.Count >= index ? siblings[++index] : null;
                }
            });

            return result;
        }

        public static bool IsMatchingEndTag(XNode node, string id)
        {
            var element = node.Cast();
            if (element == null)
            {
                return false;
            }
            return element.Name.LocalName.Equals("span", StringComparison.OrdinalIgnoreCase)
                   && element.HasAttributes
                   &&
                   element.Attributes()
                       .Any(x => x.Name.LocalName.Equals("data-tag-ref", StringComparison.OrdinalIgnoreCase))
                   &&
                   element.Attributes()
                       .First(x => x.Name.LocalName.Equals("data-tag-ref", StringComparison.OrdinalIgnoreCase))
                       .Value.Equals(id, StringComparison.OrdinalIgnoreCase)
                   &&
                   element.Attributes()
                       .Any(x => x.Name.LocalName.Equals("data-tag-boundary", StringComparison.OrdinalIgnoreCase))
                   &&
                   element.Attributes()
                       .First(x => x.Name.LocalName.Equals("data-tag-boundary", StringComparison.OrdinalIgnoreCase))
                       .Value.Equals("end", StringComparison.OrdinalIgnoreCase);
        }

        public static void ReportInappropriateGlossarySpanValues(XElement element, ItemContext itemContext,
            ErrorSeverity errorSeverity)
        {
            if (!string.IsNullOrEmpty(element.Value))
            {
                Logger.Error($"Glossary element '{element}' contains inappropriate value '{element.Value}'");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Wordlist, errorSeverity,
                    "Glossary element contains inappropriate value",
                    $"Element {element} Value: {element.Value}");
            }
        }

        public static bool IsStartingTag(XNode node)
        {
            var element = node.Cast();
            if (element == null)
            {
                return false;
            }
            return element.Name.LocalName.Equals("span", StringComparison.OrdinalIgnoreCase)
                   && element.HasAttributes
                   && element.Attributes().Any(x => x.Name.LocalName.Equals("id", StringComparison.OrdinalIgnoreCase))
                   &&
                   element.Attributes()
                       .Any(x => x.Name.LocalName.Equals("data-tag-boundary", StringComparison.OrdinalIgnoreCase))
                   &&
                   element.Attributes()
                       .First(x => x.Name.LocalName.Equals("data-tag-boundary", StringComparison.OrdinalIgnoreCase))
                       .Value.Equals("start", StringComparison.OrdinalIgnoreCase)
                   &&
                   element.Attributes()
                       .Any(x => x.Name.LocalName.Equals("data-tag", StringComparison.OrdinalIgnoreCase))
                   &&
                   element.Attributes()
                       .First(x => x.Name.LocalName.Equals("data-tag", StringComparison.OrdinalIgnoreCase))
                       .Value.Equals("word", StringComparison.OrdinalIgnoreCase);
        }

        public static bool AllMatchingTermsTagged(XElement rootElement,
            ItemContext itemContext, ErrorSeverity errorSeverity, IDictionary<string, int> terms)
        {
            var result = true;
            var words =
                rootElement.DescendantNodesAndSelf()
                    .Where(x => x.NodeType == XmlNodeType.Text)
                    .Select(x => x.ToString());
            terms.Keys.ToList().ForEach(x =>
            {
                if (!IsValidTag(x))
                {
                    result = false;
                    Logger.Error(
                        $"Tagged section {x} contains an illegal character. Only letters, punctuation, " +
                        "and spaces are permitted");
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Wordlist, errorSeverity,
                        "Tagged section contains an illegal character. Only letters, punctuation, " +
                        "and spaces are permitted",
                        $"Section: {x}");
                }
                var regex = @"(?>^|\W+)(" + x + @")(?>\W+|$)";
                var wordMatches = words.Where(y => Regex.Matches(y, regex, RegexOptions.IgnoreCase).Count > 0).ToList();
                if (wordMatches.Count() != terms[x])
                {
                    result = false;
                    Logger.Error(
                        $"There is a mismatch between the incidence of the glossary term '{x}' " +
                        $"within a formal glossary element '{terms[x]}' " +
                        $"and the incidence of the same term within the text of the CData element [{wordMatches.Aggregate((y, z) => $"'{y}'|'{z}'")}]");
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                        "There is a mismatch between the incidence of the glossary term within a formal glossary element " +
                        "and the incidence of the same term within the text of the CData element",
                        $"Counts: [{wordMatches.Aggregate((y, z) => $"'{y}'|'{z}'")}] Term: {x} Element: {terms[x]}"
                    );
                }
            });
            return result;
        }

        public static bool IsValidTag(string tag)
        {
            const string pattern = @"^([a-zA-Z,'.\-\s])+$";
            return Regex.IsMatch(tag, pattern);
        }

        public static IDictionary<string, string> GetCssColorPatterns()
        {
            return new Dictionary<string, string>
            {
                {"rgb", @"(((R|r)(G|g)(B|b))\s*\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*\))"},
                {"rgba", @"(((R|r)(G|g)(B|b)(A|a))\s*\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*(0|1)*\.\d+\s*\))"},
                {"hsl", @"(((H|h)(S|s)(L|l))\s*\(\s*\d{1,3}\s*,\s*\d{1,3}\s*%\s*,\s*\d{1,3}\s*%\s*\))"},
                {
                    "hsla",
                    @"(((H|h)(S|s)(L|l)(A|a))\s*\(\s*\d{1,3}\s*,\s*\d{1,3}\s*%\s*,\s*\d{1,3}\s*%\s*,\s*(0|1)*\.\d+\s*\))"
                },
                {"hexadecimal", @"#[a-fA-F0-9]{6}"}
            };
        }

        // Eliminates false positives where colors are embedded in words
        public static string Regexify(string target)
        {
            var result = @"(\W";
            target.ToList().ForEach(x =>
                result += $"[{x.ToString().ToUpperInvariant()}{x.ToString().ToLowerInvariant()}]"
            );
            result += @"\W)";
            return result;
        }

        public static IEnumerable<string> GetCssColorCodes()
        {
            return new List<string>
            {
                "aliceblue",
                "antiquewhite",
                "aqua",
                "aquamarine",
                "azure",
                "beige",
                "bisque",
                "black",
                "blanchedalmond",
                "blue",
                "blueviolet",
                "brown",
                "burlywood",
                "cadetblue",
                "chartreuse",
                "chocolate",
                "coral",
                "cornflowerblue",
                "cornsilk",
                "crimson",
                "cyan",
                "darkblue",
                "darkcyan",
                "darkgoldenrod",
                "darkgray",
                "darkgreen",
                "darkkhaki",
                "darkmagenta",
                "darkolivegreen",
                "darkorange",
                "darkorchid",
                "darkred",
                "darksalmon",
                "darkseagreen",
                "darkslateblue",
                "darkslategray",
                "darkturquoise",
                "darkviolet",
                "deeppink",
                "deepskyblue",
                "dimgray",
                "dodgerblue",
                "firebrick",
                "floralwhite",
                "forestgreen",
                "fuchsia",
                "gainsboro",
                "ghostwhite",
                "gold",
                "goldenrod",
                "gray",
                "green",
                "greenyellow",
                "honeydew",
                "hotpink",
                "indianred",
                "indigo",
                "ivory",
                "khaki",
                "lavender",
                "lavenderblush",
                "lawngreen",
                "lemonchiffon",
                "lightblue",
                "lightcoral",
                "lightcyan",
                "lightgoldenrodyellow",
                "lightgray",
                "lightgreen",
                "lightpink",
                "lightsalmon",
                "lightseagreen",
                "lightskyblue",
                "lightslategray",
                "lightsteelblue",
                "lightyellow",
                "lime",
                "limegreen",
                "linen",
                "magenta",
                "maroon",
                "mediumaquamarine",
                "mediumblue",
                "mediumorchid",
                "mediumpurple",
                "mediumseagreen",
                "mediumslateblue",
                "mediumspringgreen",
                "mediumturquoise",
                "mediumvioletred",
                "midnightblue",
                "mintcream",
                "mistyrose",
                "moccasin",
                "navajowhite",
                "navy",
                "oldlace",
                "olive",
                "olivedrab",
                "orange",
                "orangered",
                "orchid",
                "palegoldenrod",
                "palegreen",
                "paleturquoise",
                "palevioletred",
                "papayawhip",
                "peachpuff",
                "peru",
                "pink",
                "plum",
                "powderblue",
                "purple",
                "rebeccapurple",
                "red",
                "rosybrown",
                "royalblue",
                "saddlebrown",
                "salmon",
                "sandybrown",
                "seagreen",
                "seashell",
                "sienna",
                "silver",
                "skyblue",
                "slateblue",
                "slategray",
                "snow",
                "springgreen",
                "steelblue",
                "tan",
                "teal",
                "thistle",
                "tomato",
                "turquoise",
                "violet",
                "wheat",
                "white",
                "whitesmoke",
                "yellow",
                "yellowgreen"
            };
        }
    }
}