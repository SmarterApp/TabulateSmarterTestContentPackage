using System;
using System.Collections.Generic;
using System.Xml.XPath;
using System.Xml;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
using System.IO;
using System.Diagnostics;

namespace TabulateSmarterTestContentPackage.Validators
{
    public static class CDataValidator
    {
        const string cColorContrast = "interferes with color contrast";
        const string cZoom = "interferes with zoom";
        const string cColorOrZoom = "interferes with color contrast or zoom";
        const string cTimsTts = "internal TIMS TTS attribute should not be exported";
        const string cAltText = "alt text for images belongs in the accessibility section";

        // Dictionaries map from attributes or styles to a description of what they interfere with.
        static Dictionary<string, string> s_prohibitedElements = new Dictionary<string, string>
        {
            { "font", cColorOrZoom }
        };

        static Dictionary<string, string> s_prohibitedAttributes = new Dictionary<string, string>
        {
            { "color", cColorContrast },
            { "bgcolor", cColorContrast },
            { "data-iat-tts-vi", cTimsTts },
            { "data-iat-tts", cTimsTts },
            { "alt", cAltText }
        };

        static Dictionary<string, string> s_prohibitedClasses = new Dictionary<string, string>
        {
            { "iat-text2speech", cTimsTts },
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

        public static void ValidateItemContent(ItemContext it, IXPathNavigable contentElement, IXPathNavigable html, bool brailleSupported, string language, SmarterApp.ContentSpecId primaryStandard)
        {
            var htmlNav = html.CreateNavigator();

            if (language.Equals("ENU", StringComparison.OrdinalIgnoreCase) || Program.gValidationOptions.IsEnabled("ats"))
            {
                ImgElementsHaveValidAltReference(it, contentElement.CreateNavigator(), htmlNav, brailleSupported);
            }

            if (language.Equals("ENU", StringComparison.OrdinalIgnoreCase) && Program.gValidationOptions.IsEnabled("tss"))
            {
                // Silencing is appropriate for ELA Claim 2 Target 9
                // on 2019-05-09, the checks for ELA, Claim 2, and Target 9 were all set to !=. Not sure if this was intentional or not, but
                // shouldn't the logic be if the subject is equal to ELA, Claim equal to 2, and Target equal to 9?
                if (primaryStandard == null
                    || primaryStandard.Subject == SmarterApp.ContentSpecSubject.ELA
                    || primaryStandard.Claim == SmarterApp.ContentSpecClaim.C2
                    || primaryStandard.Target.StartsWith("9"))
                {
                    ValidateTtsSilencingTags(it, contentElement.CreateNavigator(), htmlNav, brailleSupported);
                }
            }

            ValidateHtmlElements(it, htmlNav, language);
        }

        static bool ValidateHtmlElements(ItemContext it, XPathNavigator root, string language)
        {
            bool valid = true;
            XPathNavigator ele = root.Clone();
            while (ele.MoveToFollowing(XPathNodeType.Element))
            {
                if (s_prohibitedElements.TryGetValue(ele.Name.ToLowerInvariant(), out string issueDescription))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0181, $"language='{language}' issue='{issueDescription}' element='{StartTagXml(ele)}'");
                    valid = false;
                }

                if (!s_acceptableHtmlElements.Contains(ele.Name.ToLowerInvariant()))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0210, $"language='{language}' tag='{StartTagXml(ele)}'");
                    valid = false;
                }

                var attribute = ele.Clone();
                if (attribute.MoveToFirstAttribute())
                {
                    do
                    {
                        // Check for prohibited attribute
                        if (s_prohibitedAttributes.TryGetValue(attribute.Name.ToLowerInvariant(), out issueDescription))
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0182, $"language='{language}' element='{StartTagXml(ele)}' attribute='{attribute.Name}' issue='{issueDescription}'");
                            valid = false;
                        }

                        // Check for prohibited style properties
                        else if (attribute.Name.Equals("style", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] styleProps = attribute.Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
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
                                    if (!value.Equals("transparent", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value))
                                    {
                                        ReportingUtility.ReportError(it, ErrorId.T0183, $"language='{language}' style='{name}' issue='{cColorContrast}' element='{StartTagXml(ele)}'");
                                    }
                                }

                                // Special handling for "font". Look for any component with a prohibited suffix
                                else if (name.Equals("font", StringComparison.Ordinal))
                                {
                                    foreach (string part in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (HasProhibitedUnitSuffix(part))
                                        {
                                            ReportingUtility.ReportError(it, ErrorId.T0183, $"language='{language}' style='{name}' issue='{cZoom}' element='{StartTagXml(ele)}'");
                                        }
                                    }
                                }

                                // Check for prohibited style properties
                                else if (s_prohibitedStyleProperties.TryGetValue(name, out issueDescription) && !string.IsNullOrEmpty(value))
                                {
                                    ReportingUtility.ReportError(it, ErrorId.T0183, $"language='{language}' style='{name}' issue='{issueDescription}' element='{StartTagXml(ele)}'");
                                    valid = false;
                                }

                                // Check whether size properties use prohibited units
                                else if (s_styleSizeProperties.Contains(name))
                                {
                                    if (HasProhibitedUnitSuffix(value))
                                    {
                                        ReportingUtility.ReportError(it, ErrorId.T0183, $"language='{language}' style='{name}' issue='{cZoom}' element='{StartTagXml(ele)}'");
                                    }
                                }
                            }
                        }

                        // Check for prohibited class values
                        else if (attribute.Name.Equals("class", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] classes = attribute.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            foreach(var c in classes)
                            {
                                if (s_prohibitedClasses.TryGetValue(c, out issueDescription))
                                {
                                    ReportingUtility.ReportError(it, ErrorId.T0207, $"language='{language}' class='{c}' issue='{issueDescription}' element='{StartTagXml(ele)}'");
                                    valid = false;
                                }
                            }

                        }

                        // If href or src attribute see if the referenced attachment is present
                        if (attribute.Name.Equals("src", StringComparison.OrdinalIgnoreCase)
                            || attribute.Name.Equals("href", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!it.FfItem.FileExists(attribute.Value))
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0208, $"language='{language}' element='{StartTagXml(ele)}'");
                            }
                        }
                    }
                    while (attribute.MoveToNextAttribute());

                }
            }

            return valid;
        }

        private static bool ImgElementsHaveValidAltReference(ItemContext it, XPathNavigator contentElement, XPathNavigator html, bool brailleSupported)
        {
            bool success = true;
            foreach (XPathNavigator imgEle in html.Select("//img"))
            {
                success &= ImgElementHasValidAltReference(it, contentElement, imgEle, brailleSupported);
            }
            return success;
        }

        //<summary>This method takes a <img> element tag and determines whether
        //the provided <img> element contains a valid "alt" attribute </summary>
        //<param name="image"> The <img> tag to be validated </param>
        //IMPORTANT: The ALT attribute SHOULD NOT be directly in the HTML. Rather, it should have a reference
        //to the corresponding ApipAccessibility element that contains the alt text.
        //In fact, another test checks for direct alt references and makes sure they are NOT present.
        private static bool ImgElementHasValidAltReference(ItemContext it, XPathNavigator contentElement, XPathNavigator imgEle, bool brailleSupported)
        {
            bool foundId = false;
            bool foundReadAloud = false;
            bool foundBrailleText = !brailleSupported; // Suppress errors if braille not supported. The brailleSupported boolean value is dependent on if the braille type is non null.

            bool isEquationImage = false;               // flag if a the contentLinkInfo element is type "Equation".
            bool foundReadAloudAudioShortDesc = false;  // flag if readAloud has the child element audioShortDesc
            bool emptyReadAloudAudioShortDesc = true;   // flag if readAloud child element audioShortDesc is empty
            bool foundReadAloudTTSPro = false;          // flag if readAloud has the child element textToSpeechPronunciation 
            bool emptyReadAloudTTSPro = true;           // flag if readAloud child element textToSpeechPronunciation is empty
            bool foundReadAloudAudioText = false;       // flag if readAloud has the child element audioText
            bool emptyReadAloudAudioText = true;        // flag if readAloud child element audioText is empty
            bool foundBrailleTextString = false;        // flag if brailleText has the child element brailleTextString
            bool emptyBrailleTextString = true;         // flag if braillText child element brailleTextString is empty

            CheckAltReference(contentElement, imgEle, brailleSupported, ref foundId, ref foundReadAloud, ref foundBrailleText, 
                              ref foundReadAloudAudioShortDesc, ref foundReadAloudTTSPro, ref foundReadAloudAudioText, ref foundBrailleTextString, 
                              ref emptyReadAloudAudioShortDesc, ref emptyReadAloudTTSPro, ref emptyReadAloudAudioText, ref emptyBrailleTextString, ref isEquationImage);

            // If not found on the image element itself, check its parent
            if (!foundId || !foundReadAloud || !foundBrailleText)
            {
                var parentEle = imgEle.Clone();
                if (parentEle.MoveToParent())
                {
                    CheckAltReference(contentElement, imgEle, brailleSupported, ref foundId, ref foundReadAloud, ref foundBrailleText,
                                      ref foundReadAloudAudioShortDesc, ref foundReadAloudTTSPro, ref foundReadAloudAudioText, ref foundBrailleTextString,
                                      ref emptyReadAloudAudioShortDesc, ref emptyReadAloudTTSPro, ref emptyReadAloudAudioText, ref emptyBrailleTextString, ref isEquationImage);
                }
            }

            if (!foundId)
            {
                ReportingUtility.ReportError(it, ErrorId.T0018, $"Value: {StartTagXml(imgEle)}");
            }
            else
            {
                if (!foundReadAloud)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0021, $"Value: {StartTagXml(imgEle)}");
                }
                else
                {
                    if (isEquationImage)
                    {
                        if (foundReadAloudAudioShortDesc)
                        {
                            if (emptyReadAloudAudioShortDesc)
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0184, $"Value: {StartTagXml(imgEle)}");
                            }
                        }
                        else
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0185, $"Value: {StartTagXml(imgEle)}");
                        }
                    }
                    else // non equation image
                    {
                        if (foundReadAloudTTSPro)
                        {
                            if (emptyReadAloudTTSPro)
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0022, $"Value: {StartTagXml(imgEle)}");
                            }
                        }
                        else
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0186, $"Value: {StartTagXml(imgEle)}");
                        }
                        if (foundReadAloudAudioText)
                        {
                            if (emptyReadAloudAudioText)
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0020, $"Value: {StartTagXml(imgEle)}");
                            }
                        }
                        else
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0187, $"Value: {StartTagXml(imgEle)}");
                        }
                    }
                }
                
                if (!isEquationImage) // only check the brailleTextString for non equation images
                {
                    if (brailleSupported)
                    {
                        if (!foundBrailleText)
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0019, $"Value: {StartTagXml(imgEle)}");
                        }
                        else
                        {
                            if (foundBrailleTextString)
                            {
                                if (emptyBrailleTextString)
                                {
                                    ReportingUtility.ReportError(it, ErrorId.T0097, $"Value: {StartTagXml(imgEle)}");
                                }
                            }
                            else
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0188, $"Value: {StartTagXml(imgEle)}");
                            }
                        }
                    }
                }              
            }

            return foundId && foundReadAloud && foundBrailleText;
        }

        private static void CheckAltReference(XPathNavigator contentElement, XPathNavigator checkEle, bool brailleSupported, ref bool foundId, ref bool foundReadAloud, ref bool foundBrailleText, 
                                                ref bool foundReadAloudAudioShortDes, ref bool foundReadAloudTTSPro, ref bool foundReadAloudAudioText, ref bool foundBrailleTextString,
                                                ref bool emptyReadAloudAudioShortDesc, ref bool emptyReadAloudTTSPro, ref bool emptyReadAloudAudioText, ref bool emptyBrailleTextString, ref bool isEquationImage)
        {
            string id = checkEle.GetAttribute("id", string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                return;
            }
            foundId = true;

            // Look for an accessibility element that references this item
            var relatedEle = contentElement.SelectSingleNode($"apipAccessibility/accessibilityInfo/accessElement[contentLinkInfo/@itsLinkIdentifierRef='{id}']/relatedElementInfo");

            if (relatedEle != null)
            {
                // Additional checks within the <readAloud> and <brailleText> elements are required with the following rules:
                // readAloud
                //  If the image is an equation, the <audioShortDesc> child element must exist with a non null value
                //  If image is graphic, the <textToSpeechPronunciation> and <audioText> child elements must exist with non null values
                // brailleText
                //  If image is an equation, no check is required
                //  If image is graphic and brailleText is supported, the <brailleTextString> child element must exist with a non null value. use the referenced foundBrailleText value to determine if brailleText is supported

                // Determine if the accessibility element type is an equation image by evaluating the type attribute
                isEquationImage = contentElement.SelectSingleNode($"apipAccessibility/accessibilityInfo/accessElement[contentLinkInfo/@itsLinkIdentifierRef='{id}']/contentLinkInfo").GetAttribute("type", String.Empty).Equals("Equation") ? true : false;

                if (relatedEle.SelectSingleNode("readAloud") != null)
                {
                    foundReadAloud = true; // a readAloud element exists, however its child elements still need to be evaluated

                    var readAloudAudioShortDescEle = relatedEle.SelectSingleNode("readAloud/audioShortDesc");
                    var textToSpeechProEle = relatedEle.SelectSingleNode("readAloud/textToSpeechPronunciation");
                    var audioTextEle = relatedEle.SelectSingleNode("readAloud/audioText");

                    if (isEquationImage)
                    {
                        if (readAloudAudioShortDescEle != null)
                        {
                            foundReadAloudAudioShortDes = true;
                            if (readAloudAudioShortDescEle.Value.Length != 0) emptyReadAloudAudioShortDesc = false;
                        }
                    }
                    else
                    {
                        if (textToSpeechProEle != null)
                        {
                            foundReadAloudTTSPro = true;
                            if (textToSpeechProEle.Value.Length != 0) emptyReadAloudTTSPro = false;
                        }
                        if (audioTextEle != null)
                        {
                            foundReadAloudAudioText = true;
                            if (audioTextEle.Value.Length != 0) emptyReadAloudAudioText = false;
                        }
                    }
                }

                if (brailleSupported) { 
                    if (relatedEle.SelectSingleNode("brailleText") != null)
                    {
                        foundBrailleText = true;
                        var brailleTextStringEle = relatedEle.SelectSingleNode("brailleText/brailleTextString");

                        if (!isEquationImage) // only check for non equation images and if the item supports braille. 
                        {
                            if (brailleTextStringEle != null)
                            {
                                foundBrailleTextString = true;
                                if (brailleTextStringEle.Value.Length != 0) emptyBrailleTextString = false;
                            }
                        }
                    }
                }
            }
        }

        private static void ValidateTtsSilencingTags(ItemContext it, XPathNavigator contentElement, XPathNavigator html, bool brailleSupported)
        {
            var accessibilityInfo = contentElement.SelectSingleNode("apipAccessibility/accessibilityInfo");
            // Select all elements that have an ID attribute
            foreach (XPathNavigator ele in html.Select("//*[@id]"))
            {
                // See if the content has anything other than spaces and punctuation
                if (!StringHasText(ele.Value)) continue;

                string id = ele.GetAttribute("id", string.Empty);

                // Look for an accessibility element that references this item
                var relatedEle = accessibilityInfo?.SelectSingleNode($"accessElement[contentLinkInfo/@itsLinkIdentifierRef='{id}']/relatedElementInfo/readAloud/textToSpeechPronunciation");
                if (relatedEle != null && relatedEle.InnerXml.Length == 0)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0033, $"text='{ele.InnerXml}'");
                }

            }
        }

        private static bool StringHasText(string value)
        {
            foreach (char c in value)
            {
                if (!char.IsPunctuation(c) && !char.IsWhiteSpace(c) && !char.IsSymbol(c))
                {
                    return true;
                }
            }
            return false;
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

        private static string StartTagXml(XPathNavigator nav)
        {
            string result = nav.OuterXml;
            int gt = result.IndexOf('>');
            if (gt >= 0) result = result.Substring(0, gt + 1);

            return result;
        }

        static HashSet<string> s_acceptableHtmlElements = new HashSet<string>()
        {
            "a", "abbr", "acronym", "address", "area", "article", "aside", "audio", "b",
            "base", "bdi", "bdo", "blockquote", "body", "br", "button", "canvas", "caption",
            "center", "cite", "code", "col", "colgroup", "data", "datalist", "dd", "del",
            "details", "dfn", "dialog", "div", "dl", "dt", "em", "embed", "fieldset",
            "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6",
            "head", "header", "hr", "html", "i", "iframe", "img", "input", "ins", "kbd",
            "label", "legend", "li", "link", "main", "map", "mark", "meta", "meter", "nav",
            "noscript", "object", "ol", "optgroup", "option", "output", "p", "param",
            "picture", "pre", "progress", "q", "rp", "rt", "ruby", "s", "samp", "script",
            "section", "select", "small", "source", "span", "strong", "style", "sub",
            "summary", "sup", "svg", "table", "tbody", "td", "template", "textarea",
            "tfoot", "th", "thead", "time", "title", "tr", "track", "tt", "u", "ul", "var",
            "video", "wbr"
        };

    }
}