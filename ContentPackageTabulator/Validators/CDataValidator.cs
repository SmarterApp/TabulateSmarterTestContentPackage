using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using ContentPackageTabulator.Extensions;
using ContentPackageTabulator.Extractors;
using ContentPackageTabulator.Models;
using ContentPackageTabulator.Utilities;

namespace ContentPackageTabulator.Validators
{
    public static class CDataValidator
    {
        private static readonly char[] s_WhiteAndPunct =
        {
            '\t', '\n', '\r', ' ', '!', '"', '#', '$', '%', '&', '\'', '(',
            ')', '*', '+', ',', '-', '.', '/', ':', ';', '<', '=', '>', '?', '@', '[', '\\', ']', '^', '_', '`', '{',
            '|', '~'
        };

        public static bool IsValid(XCData cData, ItemContext itemContext,
            ErrorSeverity errorSeverity = ErrorSeverity.Degraded)
        {
            if (cData == null)
            {
                Console.WriteLine("Error: invalid input");
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

                var imgTagsValid = imgTags.Select(x => ImgElementHasValidAltReference(x, itemContext, errorSeverity)).ToList();

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
                Console.WriteLine($"Error: {ex.Message}");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded, ex.Message);
            }
            return false;
        }

        public static bool ElementsFreeOfColorAlterations(XElement rootElement, ItemContext itemContext,
            ErrorSeverity errorSeverity)
        {
            if (rootElement == null)
            {
                Console.WriteLine("Error: Unable to parse HTML content in item Cdata.");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded,
                    "Unable to parse HTML content in item Cdata.");
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
                    Console.WriteLine(
                        $"Item content has element that may interfere with color contrast. Element: {x.Name.LocalName} Pattern: {path}[@{attribute.ToLower()}]");
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                        "Item content has element that may interfere with color contrast.",
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
                var violations =
                    restrictedPatterns.Keys.Where(y => Regex.IsMatch(x.Style, restrictedPatterns[y])).ToList();
                if (violations.Any())
                {
                    isValid = false;
                    ReportingUtility.ReportError("css", itemContext, ErrorCategory.Item,
                        errorSeverity, "Item content has CSS style tags that may interfere with color contrast.",
                        $"Violation: [{violations.Aggregate((y, z) => $"{y},{z}")}] Element: {x.Element.Name.LocalName} Value: {x.Element}");
                }
            });

            return isValid;
        }

        public static List<CssElement> ExtractElementsWithCssStyling(XElement rootElement)
        {
            var elements = rootElement.ElementsByPathAndAttributeCaseInsensitive("//*", "style").ToList();
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
        public static bool ImgElementHasValidAltReference(XElement imageElement, ItemContext itemContext,
            ErrorSeverity errorSeverity)
        {
            if (imageElement == null)
            {
                Console.WriteLine("Error: Encountered unparsable image element");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                    "Encountered unparsable image element");
                return false;
            }
            if (!imageElement.HasAttributes)
            {
                Console.WriteLine($"Error: Image element contains no attributes. Value: {imageElement}");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                    "Image element contains no attributes", $"Value: {imageElement}");
                return false;
            }
            var idAttribute = imageElement.Attributes().Select(x =>
                new
                {
                    Name = x.Name.LocalName,
                    x.Value
                }).FirstOrDefault(x => x.Name.Equals("id"));
            if (idAttribute == null)
            {
                Console.WriteLine($"Error: Img element does not contain a valid id attribute. Value: {imageElement}");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                    "Img element does not contain a valid id attribute", $"Value: {imageElement}");
                return false;
            }
            if (string.IsNullOrEmpty(idAttribute.Value))
            {
                Console.WriteLine($"Error: Img tag's id attribute is not valid. Value: {imageElement}");
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                    "Img tag's id attribute is not valid",
                    $"Value: {imageElement}"
                );
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

        public static bool CDataGlossaryTagsAreNotIllegallyNested(XElement rootElement, ItemContext itemContext,
            ErrorSeverity errorSeverity)
        {
            var valid = true;
            var glossaryTags =
                rootElement.XPathSelectElements(
                    ".//span[@data-tag='word' and @data-tag-boundary='start']//span[@data-tag='word' and @data-tag-boundary='start']");
            glossaryTags.ToList().ForEach(x =>
            {
                Console.WriteLine($"Error: Glossary tag {x} is nested illegally within another span tag");
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
                    Console.WriteLine($"Error: Glossary start tag {x} does not have a required ID value");
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Wordlist, errorSeverity,
                        "Glossary start tag does not have a required ID value",
                        $"Tag: {x}");
                    return;
                }
                var siblings = x.NodesAfterSelf().ToList();

                // Ensure that the start tag has a matching sibling end tag before continuing
                if (siblings.All(y => !IsMatchingEndTag(y, id)))
                {
                    Console.WriteLine($"Error: Glossary start tag '{x}' does not have a matching sibling end tag");
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
                    Console.WriteLine($"Error: Glossary tag '{x}' does not have a value");
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
                        Console.WriteLine($"Error: Glossary tag {x} has an illegally nested value '{element.Value}'");
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
                        Console.WriteLine($"Error: Glossary tag {element ?? node} overlaps with another tag {x}");
                        ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                            "Glossary tag overlaps with another tag",
                            $"Tag: {element ?? node} Overlaps: {x}");
                        node = siblings.Count >= index ? siblings[index++] : null;
                        return;
                    }
                    // We found something that shouldn't be here
                    Console.WriteLine(
                        $"Error: Unrecognized element {element ?? node} encountered while processing siblings of {x}");
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
                Console.WriteLine($"Error: Glossary element '{element}' contains inappropriate value '{element.Value}'");
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
                    Console.WriteLine(
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
                    Console.WriteLine(
						"Term that is tagged for glossary is not tagged when it occurs elswhere in the item." +
						$" Term: {x} Element: {terms[x]}");
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, errorSeverity,
                        "Term that is tagged for glossary is not tagged when it occurs elswhere in the item.",
                        $"Term: {x} Element: {terms[x]}"
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

        // Returns the Wordlist ID
        public static string ValidateContentAndWordlist(ItemContext it, XNode xml)
        {
            // Get the wordlist ID
            var xp = $"/itemrelease/{(it.IsPassage ? "passage" : "item")}/resourceslist/resource[@type='wordList']/@id";
            var wordlistId = xml.XpEval(xp);

            // Process all CDATA (embedded HTML) sections in the content
            {
                var contentNode =
                    xml.SelectSingleNode($"/itemrelease/{(it.IsPassage ? "passage" : "item")}/content[@language='ENU']");
                if (contentNode == null)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                        "Item has no content element.");
                }
                else
                {
                    var glossaryTerms = CDataExtractor.ExtractCData((XElement) contentNode)
                        .ToList()
                        .Select(x => ValidateContentCData(it,
                            new XDocument().LoadXml($"<root>{x.Value}</root>"))).ToList();
                    var dictionary = new Dictionary<int, string>();
                    glossaryTerms.ForEach(x => x.Keys.ToList().ForEach(y =>
                    {
                        if (dictionary.ContainsKey(y))
                        {
                            dictionary[y] = x[y];
                        }
                        else
                        {
                            dictionary.Add(y, x[y]);
                        }
                    }));
                    if (string.IsNullOrEmpty(wordlistId))
                    {
                        if (dictionary.Keys.Any())
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Benign,
                                "Item has terms marked for glossary but does not reference a wordlist.");
                        }
                        return string.Empty;
                    }

                    ValidateWordlistVocabulary(wordlistId, it,
                        dictionary.Keys.ToList(),
                        dictionary.Values.ToList());
                }
            }

            return wordlistId;
        }

        public static IDictionary<int, string> ValidateContentCData(ItemContext it, XDocument html)
        {
            /* Word list references look like this:
            <span id="item_998_TAG_2" class="its-tag" data-tag="word" data-tag-boundary="start" data-word-index="1"></span>
            What
            <span class="its-tag" data-tag-ref="item_998_TAG_2" data-tag-boundary="end"></span>
            */
            var result = new Dictionary<int, string>();
            // Extract all wordlist references
            foreach (var o in html.SelectNodes("//span[@data-tag='word' and @data-tag-boundary='start']"))
            {
                var node = (XElement) o;

                // For a word reference, get attributes and look for the end tag
                var id = node.GetAttribute("id");
                if (string.IsNullOrEmpty(id))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                        "WordList reference lacks an ID");
                    continue;
                }
                var scratch = node.GetAttribute("data-word-index");
                int termIndex;
                if (!int.TryParse(scratch, out termIndex))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                        "WordList reference term index is not integer", "id='{0} index='{1}'", id, scratch);
                    continue;
                }

                var term = string.Empty;
                var snode = node.NextNode();
                for (;;)
                {
                    // If no more siblings but didn't find end tag, report.
                    if (snode == null)
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable,
                            "WordList reference missing end tag.", "id='{0}' index='{1}' term='{2}'", id, termIndex,
                            term);
                        break;
                    }

                    // Look for end tag
                    var enode = snode as XElement;
                    if (enode != null
                        && enode.GetAttribute("data-tag-boundary").Equals("end", StringComparison.Ordinal)
                        && enode.GetAttribute("data-tag-ref").Equals(id, StringComparison.Ordinal))
                    {
                        break;
                    }

                    // Collect term plain text
                    if (snode.NodeType == XmlNodeType.Text || snode.NodeType == XmlNodeType.SignificantWhitespace)
                    {
                        term += snode.InnerText();
                    }
                    snode = snode.Cast()?.FirstNode ?? (snode.NextNode ?? snode.Parent?.NextNode);
                }
                term = term.Trim(s_WhiteAndPunct);
                try
                {
                    if (result.ContainsKey(termIndex))
                    {
                        result[termIndex] = term;
                    }
                    else
                    {
                        result.Add(termIndex, term);
                    }
                }
                catch (Exception)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable,
                        "WordList reference ID matches another reference's ID", "id='{0} 'term='{2}'", termIndex, term,
                        term);
                }
            }

            return result;
        }

        public static void ValidateWordlistVocabulary(string wordlistId, ItemContext itemIt, List<int> termIndices,
            List<string> terms)
        {
            // Read the wordlist XML
            ItemContext it;
            if (!Tabulator.mIdToItemContext.TryGetValue(wordlistId, out it))
            {
                ReportingUtility.ReportError(itemIt, ErrorCategory.Item, ErrorSeverity.Degraded,
                    "Item references non-existent wordlist (WIT)", "wordlistId='{0}'", wordlistId);
                return;
            }
            var xml = new XDocument();
            if (!Tabulator.TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", out xml))
            {
                ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Severe, "Invalid wordlist file.",
                    Tabulator.LoadXmlErrorDetail);
                return;
            }

            // Sanity check
            if (!string.Equals(xml.XpEval("itemrelease/item/@id"), it.ItemId))
            {
                throw new InvalidDataException("Item id mismatch on pass 2");
            }

            // Create a dictionary of attachment files
            var attachmentFiles = new Dictionary<string, long>();
            foreach (var fi in it.FfItem.Files)
            {
                // If Audio or image file
                var extension = fi.Extension.ToLowerInvariant();
                if (!string.Equals(extension, ".xml", StringComparison.Ordinal))
                {
                    attachmentFiles.Add(fi.Name, fi.Length);
                }
            }

            // Create a hashset of all wordlist terms that are referenced by the item
            var referencedIndices = new HashSet<int>(termIndices);

            // Load up the list of wordlist terms
            var wordlistTerms = new List<string>();
            foreach (XNode kwNode in xml.SelectNodes("itemrelease/item/keywordList/keyword"))
            {
                // Get the term and its index
                var term = kwNode.XpEval("@text");
                var index = int.Parse(kwNode.XpEval("@index"));

                // Make sure the index is unique and add to the term list
                while (wordlistTerms.Count < index + 1)
                {
                    wordlistTerms.Add(string.Empty);
                }
                if (!string.IsNullOrEmpty(wordlistTerms[index]))
                {
                    ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Severe,
                        "Wordlist has multiple terms with the same index.", "index='{0}'", index);
                }
                else
                {
                    wordlistTerms[index] = term;
                }
            }

            // Keep track of term information for error checks   
            var attachmentToReference = new Dictionary<string, TermAttachmentReference>();

            // Enumerate all the terms in the wordlist (second pass)
            var ordinal = 0;
            foreach (XNode kwNode in xml.SelectNodes("/itemrelease/item/keywordList/keyword"))
            {
                ++ordinal;

                // Get the term and its index
                var term = kwNode.XpEval("@text");
                var index = int.Parse(kwNode.XpEval("@index"));

                // See if this term is referenced by the item.
                var termReferenced = referencedIndices.Contains(index);
                if (!termReferenced && Program.gValidationOptions.IsEnabled("uwt"))
                {
                    ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Benign,
                        "Wordlist term is not referenced by item.", "term='{0}' termIndex='{1}'", term, index);
                }

                // Find the attachment references and enumberate the translations
                var translationBitflags = 0;
                foreach (XNode htmlNode in kwNode.SelectNodes("html"))
                {
                    var listType = htmlNode.XpEval("@listType");
                    Tabulator.mTranslationCounts.Increment(listType);

                    var nTranslation = -1;
                    if (Tabulator.sExpectedTranslationsIndex.TryGetValue(listType, out nTranslation))
                    {
                        translationBitflags |= 1 << nTranslation;
                    }

                    // Get the embedded HTML
                    var html = htmlNode.InnerText();

                    var audioType = string.Empty;
                    long audioSize = 0;
                    var imageType = string.Empty;
                    long imageSize = 0;

                    // Look for an audio glossary entry
                    var match = Tabulator.sRxAudioAttachment.Match(html);
                    if (match.Success)
                    {
                        // Use RegEx to find the audio glossary entry in the contents.
                        var filename = match.Groups[1].Value;
                        ProcessGlossaryAttachment(filename, itemIt, it, index, listType, termReferenced, wordlistTerms,
                            attachmentFiles, attachmentToReference, ref audioType, ref audioSize);

                        // Check for dual types
                        if (string.Equals(Path.GetExtension(filename), ".ogg", StringComparison.OrdinalIgnoreCase))
                        {
                            filename = Path.GetFileNameWithoutExtension(filename) + ".m4a";
                            ProcessGlossaryAttachment(filename, itemIt, it, index, listType, termReferenced,
                                wordlistTerms, attachmentFiles, attachmentToReference, ref audioType, ref audioSize);
                        }
                        else if (string.Equals(Path.GetExtension(filename), ".m4a", StringComparison.OrdinalIgnoreCase))
                        {
                            filename = Path.GetFileNameWithoutExtension(filename) + ".ogg";
                            ProcessGlossaryAttachment(filename, itemIt, it, index, listType, termReferenced,
                                wordlistTerms, attachmentFiles, attachmentToReference, ref audioType, ref audioSize);
                        }

                        // If filename matches the naming convention, ensure that values are correct
                        var match2 = Tabulator.sRxAttachmentNamingConvention.Match(filename);
                        if (match2.Success)
                        {
                            // Sample attachment filename that follows the convention:
                            // item_116605_v1_116605_01btagalog_glossary_ogg_m4a.m4a

                            // Check both instances of the wordlist ID
                            if (!wordlistId.Equals(match2.Groups[1].Value, StringComparison.Ordinal)
                                && !wordlistId.Equals(match2.Groups[2].Value, StringComparison.Ordinal))
                            {
                                ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Degraded,
                                    "Wordlist attachment filename indicates wordlist ID mismatch.",
                                    "filename='{0}' filenameItemId='{1}' expectedItemId='{2}'", filename,
                                    match2.Groups[1].Value, wordlistId);
                            }

                            // Check that the wordlist term index matches
                            /* While most filename indices match. It's quite common for them not to match and still be the correct audio
                               Disabling this check because it's mostly false alarms.

                            int filenameIndex;
                            if (!int.TryParse(match2.Groups[3].Value, out filenameIndex)) filenameIndex = -1;
                            if (filenameIndex != index && filenameIndex != ordinal
                                && (filenameIndex >= wordlistTerms.Count || !string.Equals(wordlistTerms[filenameIndex], term, StringComparison.OrdinalIgnoreCase)))
                            {
                                ReportingUtility.ReportWitError(ItemIt, it, ErrorSeverity.Degraded, "Wordlist attachment filename indicates term index mismatch.", "filename='{0}' filenameIndex='{1}' expectedIndex='{2}'", filename, filenameIndex, index);
                            }
                            */

                            // Translate from language in the naming convention to listType value
                            var filenameListType = match2.Groups[4].Value.ToLower();
                            switch (filenameListType)
                            {
                                // Special cases
                                case "spanish":
                                    filenameListType = "esnGlossary";
                                    break;

                                case "tagalog":
                                case "atagalog":
                                case "btagalog":
                                case "ilocano":
                                case "atagal":
                                    filenameListType = "tagalGlossary";
                                    break;

                                case "apunjabi":
                                case "bpunjabi":
                                case "punjabiwest":
                                case "punjabieast":
                                    filenameListType = "punjabiGlossary";
                                    break;

                                // Conventional case
                                default:
                                    filenameListType = string.Concat(filenameListType.ToLower(), "Glossary");
                                    break;
                            }
                            if (!filenameListType.Equals(listType))
                            {
                                ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Degraded,
                                    "Wordlist audio filename indicates attachment language mismatch.",
                                    "filename='{0}' filenameListType='{1}' expectedListType='{2}'", filename,
                                    filenameListType, listType);
                            }
                        }
                    }

                    // Look for an image glossary entry
                    match = Tabulator.sRxImageAttachment.Match(html);
                    if (match.Success)
                    {
                        // Use RegEx to find the audio glossary entry in the contents.
                        var filename = match.Groups[1].Value;
                        ProcessGlossaryAttachment(filename, itemIt, it, index, listType, termReferenced, wordlistTerms,
                            attachmentFiles, attachmentToReference, ref imageType, ref imageSize);
                    }

                    // Folder,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize
                    if (Program.gValidationOptions.IsEnabled("gtr"))
                    {
                        Tabulator.mGlossaryReport.WriteLine(string.Join(",", it.Folder,
                            ReportingUtility.CsvEncode(it.ItemId), itemIt.ItemId,
                            index.ToString(), ReportingUtility.CsvEncodeExcel(term),
                            ReportingUtility.CsvEncode(listType), html.Length.ToString(),
                            audioType, audioSize.ToString(), imageType, imageSize.ToString(),
                            ReportingUtility.CsvEncode(html)));
                    }
                    else
                    {
                        Tabulator.mGlossaryReport.WriteLine(string.Join(",", it.Folder,
                            ReportingUtility.CsvEncode(it.ItemId), itemIt.ItemId,
                            index.ToString(), ReportingUtility.CsvEncodeExcel(term),
                            ReportingUtility.CsvEncode(listType), html.Length.ToString(),
                            audioType, audioSize.ToString(), imageType, imageSize.ToString()));
                    }
                }

                // Report any expected translations that weren't found
                if (termReferenced && translationBitflags != 0 &&
                    translationBitflags != Tabulator.sExpectedTranslationsBitflags)
                {
                    // Make a list of translations that weren't found
                    var missedTranslations = new List<string>();
                    for (var i = 0; i < Tabulator.sExpectedTranslations.Length; ++i)
                    {
                        if ((translationBitflags & (1 << i)) == 0)
                        {
                            missedTranslations.Add(Tabulator.sExpectedTranslations[i]);
                        }
                    }
                    ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Tolerable,
                        "Wordlist does not include all expected translations.", "term='{0}' missing='{1}'", term,
                        string.Join(", ", missedTranslations));
                }
            }

            var stemmer = new Stemmer();

            // Make sure terms match references
            for (var i = 0; i < termIndices.Count; ++i)
            {
                var index = termIndices[i];
                if (index >= wordlistTerms.Count || string.IsNullOrEmpty(wordlistTerms[index]))
                {
                    ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Benign,
                        "Item references non-existent wordlist term.", "text='{0}' termIndex='{1}'", terms[i], index);
                }
                else
                {
                    if (!stemmer.TermsMatch(terms[i], wordlistTerms[index]))
                    {
                        ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Degraded,
                            "Item text does not match wordlist term.", "text='{0}' term='{1}' termIndex='{2}'", terms[i],
                            wordlistTerms[index], index);
                    }
                }
            }

            // Report unreferenced attachments
            if (Program.gValidationOptions.IsEnabled("umf"))
            {
                foreach (var pair in attachmentFiles)
                {
                    if (!attachmentToReference.ContainsKey(pair.Key))
                    {
                        ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Benign,
                            "Unreferenced wordlist attachment file.", "filename='{0}'", pair.Key);
                    }
                }
            }
        }

        public static void ProcessGlossaryAttachment(string filename,
            ItemContext itemIt, ItemContext it, int termIndex, string listType, bool termReferenced,
            List<string> wordlistTerms, Dictionary<string, long> attachmentFiles,
            Dictionary<string, TermAttachmentReference> attachmentToTerm,
            ref string type, ref long size)
        {
            long fileSize = 0;
            if (!attachmentFiles.TryGetValue(filename, out fileSize))
            {
                // Look for case-insensitive match (file will not be found on Linux systems)
                // (This is a linear search but it occurs rarely so not a significant issue)
                string caseMismatchFilename = null;
                foreach (var pair in attachmentFiles)
                {
                    if (string.Equals(filename, pair.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        caseMismatchFilename = pair.Key;
                        break;
                    }
                }

                if (termReferenced)
                {
                    if (caseMismatchFilename == null)
                    {
                        ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Severe,
                            "Wordlist attachment not found.",
                            "filename='{0}' term='{1}' termIndex='{2}'", filename, wordlistTerms[termIndex], termIndex);
                    }
                    else
                    {
                        ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Degraded,
                            "Wordlist audio filename differs in capitalization (will fail on certain platforms).",
                            "referenceFilename='{0}' actualFilename='{1}' termIndex='{2}'", filename,
                            caseMismatchFilename, termIndex);
                    }
                }

                else if (Program.gValidationOptions.IsEnabled("mwa")) // Term not referenced
                {
                    if (caseMismatchFilename == null)
                    {
                        ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Benign,
                            "Wordlist attachment not found. Benign because corresponding term is not referenced.",
                            "filename='{0}' term='{1}' termIndex='{2}'", filename, wordlistTerms[termIndex], termIndex);
                    }
                    else
                    {
                        ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Benign,
                            "Wordlist attachment filename differs in capitalization. Benign because corresponding term is not referenced.",
                            "referenceFilename='{0}' actualFilename='{1}' termIndex='{2}'", filename,
                            caseMismatchFilename, termIndex);
                    }
                }
            }

            // See if this attachment has previously been referenced
            TermAttachmentReference previousTerm = null;
            if (attachmentToTerm.TryGetValue(filename, out previousTerm))
            {
                // Error if different terms (case insensitive)
                if (
                    !string.Equals(wordlistTerms[termIndex], wordlistTerms[previousTerm.TermIndex],
                        StringComparison.OrdinalIgnoreCase))
                {
                    ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Severe,
                        "Two different wordlist terms reference the same attachment.",
                        "filename='{0}' termA='{1}' termB='{2}' termIndexA='{3}' termIndexB='{4}",
                        filename, wordlistTerms[previousTerm.TermIndex], wordlistTerms[termIndex],
                        previousTerm.TermIndex, termIndex);
                }

                // Error if different listTypes (language or image)
                if (!string.Equals(listType, previousTerm.ListType, StringComparison.Ordinal))
                {
                    ReportingUtility.ReportWitError(itemIt, it, ErrorSeverity.Severe,
                        "Same wordlist attachment used for different languages or types.",
                        "filename='{0}' term='{1}' typeA='{2}' typeB='{3}' termIndexA='{4}' termIndexB='{5}",
                        filename, wordlistTerms[termIndex], previousTerm.ListType, listType, previousTerm.TermIndex,
                        termIndex);
                }
            }
            else
            {
                attachmentToTerm.Add(filename, new TermAttachmentReference(termIndex, listType, filename));
            }

            size += fileSize;
            var extension = Path.GetExtension(filename);
            if (extension.Length > 1)
            {
                extension = extension.Substring(1); // Remove dot from extension
            }
            if (string.IsNullOrEmpty(type))
            {
                type = extension.ToLower();
            }
            else
            {
                type = string.Concat(type, ";", extension.ToLower());
            }
        }

        private static XDocument LoadHtml(XElement content)
        {
            // Parse the HTML into an XML DOM
            return new XDocument().LoadXml($"<root>{content.Value}</root>");
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