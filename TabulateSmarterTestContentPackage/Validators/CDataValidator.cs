using System;
using System.Collections.Generic;
using System.Xml.XPath;
using System.Xml;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
using System.IO;

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

            ElementsFreeOfProhibitedAttributes(it, htmlNav);
        }

        static bool ElementsFreeOfProhibitedAttributes(ItemContext it, XPathNavigator root)
        {
            bool valid = true;
            XPathNavigator ele = root.Clone();
            while (ele.MoveToFollowing(XPathNodeType.Element))
            {
                if (s_prohibitedElements.TryGetValue(ele.Name, out string interferesWith))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                        $"Item content has element that may interfere with {interferesWith}.", $"element='{StartTagXml(ele)}'");
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
                                $"Item content has attribute that may interfere with {interferesWith}.", $"attribute='{attribute.Name}' element='{StartTagXml(ele)}'");
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
                                    if (!value.Equals("transparent", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value))
                                    {
                                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                            $"Item content has style property that may interfere with color contrast.", $"style='{name}' element='{StartTagXml(ele)}'");
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
                                                $"Item content has style property that may interfere with zoom.", $"style='{name}' element='{StartTagXml(ele)}'");
                                        }
                                    }
                                }

                                // Check for prohibited style properties
                                else if (s_prohibitedStyleProperties.TryGetValue(name, out interferesWith) && !string.IsNullOrEmpty(value))
                                {
                                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                        $"Item content has style property that may interfere with {interferesWith}.", $"style='{name}' element='{StartTagXml(ele)}'");
                                    valid = false;
                                }

                                // Check whether size properties use prohibited units
                                else if (s_styleSizeProperties.Contains(name))
                                {
                                    if (HasProhibitedUnitSuffix(value))
                                    {
                                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                            $"Item content has style property that may interfere with zoom.", $"style='{name}' element='{StartTagXml(ele)}'");
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
        private static bool ImgElementHasValidAltReference(ItemContext it, XPathNavigator contentElement, XPathNavigator imgEle, bool brailleSupported)
        {
            bool foundId = false;
            bool foundReadAloud = false;
            bool foundBrailleText = !brailleSupported; // Suppress errors if braille not supported. The brailleSupported boolean value is dependent on if the braille type is non null.

            bool isEquationImage = false;               // flag if a the contentLinkInfo element is type "Equation".
            bool foundReadAloudAudioShortDesc = false;  // flag if readAloud has the child element audioShortDesc
            bool foundReadAloudTTSPro = false;          // flag if readAloud has the child element textToSpeechPronunciation 
            bool foundReadAloudAudioText = false;       // flag if readAloud has the child element audioText
            bool foundBrailleTextString = false;        // flag if brailleText has the child element brailleTextString

            CheckAltReference(contentElement, imgEle, brailleSupported, ref foundId, ref foundReadAloud, ref foundBrailleText, ref foundReadAloudAudioShortDesc, ref foundReadAloudTTSPro, ref foundReadAloudAudioText, ref foundBrailleTextString, ref isEquationImage);

            // If not found on the image element itself, check its parent
            if (!foundId || !foundReadAloud || !foundBrailleText)
            {
                var parentEle = imgEle.Clone();
                if (parentEle.MoveToParent())
                {
                    CheckAltReference(contentElement, parentEle, brailleSupported, ref foundId, ref foundReadAloud, ref foundBrailleText, ref foundReadAloudAudioShortDesc, ref foundReadAloudTTSPro, ref foundReadAloudAudioText, ref foundBrailleTextString, ref isEquationImage);
                }
            }

            if (!foundId)
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                    "Img element does not contain an id attribute necessary to provide alt text.", $"Value: {StartTagXml(imgEle)}");
            }
            else
            {
                if (!foundReadAloud)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                        "Img element does not reference alt text for text-to-speech (no corresponding readAloud element).", $"Value: {StartTagXml(imgEle)}");
                }
                else
                {
                    if (!foundReadAloudAudioShortDesc && isEquationImage) // this error should only apply to image elements for equations
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                            "Img element for an equation resource does not have an audioShortDesc child element within the readAloud element.", $"Value: {StartTagXml(imgEle)}");
                    }
                    if (!isEquationImage)
                    {
                        if (!foundReadAloudTTSPro) // this error should only apply to image elements that are not equations
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                "Img element for a graphic resource does not have a textToSpeechPronunciation child element within the readAloud element.", $"Value: {StartTagXml(imgEle)}");
                        }
                        if (!foundReadAloudAudioText) // this error should only apply to image element that are not equations
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                "Img element for a graphic resource does not have a audioText child element within the readAloud element.", $"Value: {StartTagXml(imgEle)}");
                        }
                    }
                }
                
                if (brailleSupported)
                {
                    if (!foundBrailleText)
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                            "Img element does not reference alt text for braille presentation (no corresponding brailleText element).", $"Value: {StartTagXml(imgEle)}");
                    }
                    else
                    {
                        if (!foundBrailleTextString && !isEquationImage)
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                "Img element for a graphic resource does not have a brailleTextString child element within the brailleText element.", $"Value: {StartTagXml(imgEle)}");
                        }
                    }
                }                
            }

            return foundId && foundReadAloud && foundBrailleText;
        }

        private static void CheckAltReference(XPathNavigator contentElement, XPathNavigator checkEle, bool brailleSupported, ref bool foundId, ref bool foundReadAloud, ref bool foundBrailleText, 
                                                ref bool foundReadAloudAudioShortDes, ref bool foundReadAloudTTSPro, ref bool foundReadAloudAudioText, ref bool foundBrailleTextString, ref bool isEquationImage)
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
                            if (readAloudAudioShortDescEle.Value != null) foundReadAloudAudioShortDes = true;
                        } 
                    }
                    else
                    {
                        if (textToSpeechProEle != null)
                        {
                            if (textToSpeechProEle.Value != null) foundReadAloudTTSPro = true;
                        }
                        if (audioTextEle != null)
                        {
                            if (audioTextEle.Value != null) foundReadAloudAudioText = true;
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
                                if (brailleTextStringEle.Value != null) foundBrailleTextString = true;
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
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable, "Item has improper TTS Silencing Tag", $"text='{ele.InnerXml}'");
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

    }
}