using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
using TabulateSmarterTestContentPackage.Validators;

namespace TabulateSmarterTestContentPackage
{

    // Someday we may figure out how to separate this from the giant Tabulator class but not today.
    partial class Tabulator
    {
        static HashSet<string> cRubricElements = new HashSet<string>(new string[]
        {
            "rubriclist",
            "rationaleoptlist",
            "concept",
            "es",
            "himi"
        });

        void ValidateContentAndWordlist(ItemContext it, XmlDocument xml, bool brailleSupported, SmarterApp.ContentSpecId primaryStandard,
            out string rWordlistId, out int rEnglishCharacterCount, out Tabulator.GlossaryTypes rAggregateGlossaryTypes)
        {
            // Compose lists of referenced term Indices and Names
            var termIndices = new List<int>();
            var terms = new List<string>();
            var nonTermTokens = new StringBuilder();

            int englishCharacterCount = 0;

            // Process all CDATA (embedded HTML) sections in the content
            {
                // There may be multiple content sections - one per language/presentation
                var contentElements = xml.SelectNodes(it.IsStimulus ? "itemrelease/passage/content" : "itemrelease/item/content");
                if (contentElements.Count == 0)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0151);
                }
                else
                {
                    // For each content section
                    foreach (XmlNode contentElement in contentElements)
                    {
                        string language = contentElement.XpEvalE("@language");

                        // For each element in the content section
                        foreach (XmlNode content in contentElement.ChildNodes)
                        {
                            // Only process elements that are not rubrics
                            if (content.NodeType != XmlNodeType.Element) continue;
                            if (cRubricElements.Contains(content.Name)) continue;

                            // Validate all CDATA elements (that are not in rubrics)
                            foreach (var node in new XmlSubtreeEnumerable(content))
                            {
                                if (node.NodeType == XmlNodeType.CDATA)
                                {
                                    var html = ContentValidator.LoadHtml(it, node);
                                    ContentValidator.ValidateGlossaryTags(it, termIndices, terms, html);

                                    // Tokenize the text in order to check for untagged glossary terms
                                    if (language.Equals("ENU", StringComparison.OrdinalIgnoreCase))
                                    {
                                        englishCharacterCount += ContentValidator.TokenizeNonGlossaryText(nonTermTokens, html);
                                    }

                                    // Perform other CDATA validation
                                    // (Includes styles, img tags, etc)
                                    CDataValidator.ValidateItemContent(it, contentElement, html, brailleSupported, language, primaryStandard);
                                }
                            }
                        }
                    }
                }
            }

            // Report any glossary terms that have untagged instances.
            if (Program.gValidationOptions.IsEnabled("ugt"))
            {
                string ntTokens = nonTermTokens.ToString();
                foreach (var term in terms)
                {
                    if (ntTokens.IndexOf(ContentValidator.Tokenize(term)) >= 0)
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0074, $"term='{term}'");
                    }
                }
            }

            // Get the wordlist ID (and check for multiple instances)
            string wordlistId = string.Empty;
            string wordlistBankkey = string.Empty;
            string xp = it.IsStimulus
                ? "itemrelease/passage/resourceslist/resource[@type='wordList']"
                : "itemrelease/item/resourceslist/resource[@type='wordList']";

            // Retrieve each wordlist and check it against the referenced terms
            foreach (XmlElement xmlRes in xml.SelectNodes(xp))
            {
                string witId = xmlRes.GetAttribute("id");
                string witBankkey = xmlRes.GetAttribute("bankkey");
                if (string.IsNullOrEmpty(witId) || string.IsNullOrEmpty(witBankkey))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0152);
                }
                else
                {
                    if (!string.IsNullOrEmpty(wordlistId))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0153);
                    }
                    else
                    {
                        wordlistId = witId;
                        wordlistBankkey = witBankkey;
                    }

                    // Count this reference
                    var witIdx = new ItemIdentifier(cItemTypeWordlist, witBankkey, witId);
                    mWordlistRefCounts.Increment(witIdx.ToString());
                }
            }

            Tabulator.GlossaryTypes aggregateGlossaryTypes = Tabulator.GlossaryTypes.None;

            if (string.IsNullOrEmpty(wordlistId))
            {
                if (termIndices.Count > 0)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0034);
                }
                wordlistId = string.Empty;
            }
            else
            {
                aggregateGlossaryTypes = ValidateWordlistVocabulary(wordlistBankkey, wordlistId, it, termIndices, terms);
            }

            rWordlistId = wordlistId;
            rEnglishCharacterCount = englishCharacterCount;
            rAggregateGlossaryTypes = aggregateGlossaryTypes;
        } // ValidateContentAndWordlist

    }

    static class ContentValidator
    {
        static readonly char[] s_WhiteAndPunct = { '\t', '\n', '\r', ' ', '!', '"', '#', '$', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/', ':', ';', '<', '=', '>', '?', '@', '[', '\\', ']', '^', '_', '`', '{', '|', '~' };

        public  static void ValidateGlossaryTags(ItemContext it, IList<int> termIndices, IList<string> terms, XmlDocument html)
        {
            /* Word list references look like this:
            <span id="item_998_TAG_2" class="its-tag" data-tag="word" data-tag-boundary="start" data-word-index="1"></span>
            What
            <span class="its-tag" data-tag-ref="item_998_TAG_2" data-tag-boundary="end"></span>
            */

            // Extract all wordlist references
            foreach (XmlElement node in html.SelectNodes("//span[@data-tag='word' and @data-tag-boundary='start']"))
            {

                // For a word reference, get attributes and look for the end tag
                var id = node.GetAttribute("id");
                if (string.IsNullOrEmpty(id))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0154);
                    continue;
                }
                var scratch = node.GetAttribute("data-word-index");
                int termIndex;
                if (!int.TryParse(scratch, out termIndex))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0091, "id='{0} index='{1}'", id, scratch);
                    continue;
                }

                var term = string.Empty;
                var snode = node.NextNode();
                for (;;)
                {
                    // If no more siblings but didn't find end tag, report.
                    if (snode == null)
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0090, "id='{0}' index='{1}' term='{2}'", id, termIndex, term);
                        break;
                    }

                    // Look for end tag
                    XmlElement enode = snode as XmlElement;
                    if (enode != null
                        && enode.GetAttribute("data-tag-boundary").Equals("end", StringComparison.Ordinal)
                        && enode.GetAttribute("data-tag-ref").Equals(id, StringComparison.Ordinal))
                    {
                        break;
                    }

                    // Check for a nested or overlapping glossary tag
                    if (enode != null
                        && enode.GetAttribute("data-tag").Equals("word", StringComparison.Ordinal)
                        && enode.GetAttribute("data-tag-boundary").Equals("start", StringComparison.Ordinal))
                    {
                        var otherId = enode.GetAttribute("id");
                        ReportingUtility.ReportError(it, ErrorId.T0017, $"glossaryId1='{id}' glossaryId2='{otherId}'");
                    }

                    // Collect term plain text
                    if (snode.NodeType == XmlNodeType.Text || snode.NodeType == XmlNodeType.SignificantWhitespace)
                    {
                        term += snode.Value;
                    }

                    snode = snode.NextNode();
                }
                term = term.Trim(s_WhiteAndPunct);
                termIndices.Add(termIndex);
                terms.Add(term);
            }
        } // ValidateGlossaryTags

        public static XmlDocument LoadHtml(ItemContext it, XmlNode content)
        {
            // Parse the HTML into an XML DOM
            XmlDocument html = null;
            try
            {
                var settings = new Html.HtmlReaderSettings
                {
                    CloseInput = true,
                    EmitHtmlNamespace = false,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                    IgnoreInsignificantWhitespace = true
                };
                using (var reader = new Html.HtmlReader(new StringReader(content.InnerText), settings))
                {
                    html = new XmlDocument();
                    html.Load(reader);
                }
            }
            catch (Exception err)
            {
                ReportingUtility.ReportError(it, ErrorId.T0155, "context='{0}' error='{1}'", GetXmlContext(content), err.Message);
            }
            return html;
        }

        /// <summary>
        /// Tokenizes the non-glossary text and counts the number of characters in the text
        /// </summary>
        /// <param name="sb">StringBuilder into which the tokenized text is loaded.</param>
        /// <param name="html">An XmlDocument containing the parsed HTML text to be tokenized and counted.</param>
        /// <returns>The number of text characters (not including tags) in the text.</returns>
        public static int TokenizeNonGlossaryText(StringBuilder sb, XmlDocument html)
        {
            XmlNode node = html.FirstChild;
            int inWordRef = 0;
            int characterCount = 0;
            while (node != null)
            {
                XmlElement element = node as XmlElement;

                // If beginning of a word reference
                if (element != null
                    && element.GetAttribute("data-tag").Equals("word", StringComparison.Ordinal)
                    && element.GetAttribute("data-tag-boundary").Equals("start", StringComparison.Ordinal))
                {
                    if (inWordRef == 0)
                    {
                        // insert placeholder (that shouldn't match anything in actual text)
                        Tokenize(sb, "cqcqcq");
                    }
                    ++inWordRef;
                }

                // Look for end tag
                if (element != null
                    && element.GetAttribute("data-tag-boundary").Equals("end", StringComparison.Ordinal)
                    && inWordRef > 0)
                {
                    --inWordRef;
                }

                if (inWordRef == 0 && node.NodeType == XmlNodeType.Text)
                {
                    Tokenize(sb, node.Value);
                }

                if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.SignificantWhitespace || node.NodeType == XmlNodeType.Whitespace)
                {
                    characterCount += node.Value.Length;
                }

                node = node.NextNode();
            }

            return characterCount;
        }

        static void Tokenize(StringBuilder sb, string content)
        {
            // Ensure that a space delimiter exists
            if (sb.Length == 0 || sb[sb.Length - 1] != ' ') sb.Append(' ');

            int i = 0;
            while (i < content.Length)
            {
                // Skip non-word characters
                while (i < content.Length)
                {
                    char c = content[i];
                    if (char.IsLetterOrDigit(c) || c == '\'') break;
                    ++i;
                }

                if (i >= content.Length) break;

                // Transfer all word characters
                while (i < content.Length)
                {
                    char c = content[i];
                    if (!char.IsLetterOrDigit(c) && c != '\'') break;
                    sb.Append(char.ToLowerInvariant(c));
                    ++i;
                }

                // Append a space
                sb.Append(' ');
            }
        }

        public static string Tokenize(string content)
        {
            StringBuilder sb = new StringBuilder();
            Tokenize(sb, content);
            return sb.ToString();
        }

        static string GetXmlContext(XmlNode node)
        {
            string context = string.Empty;
            while (node != null && node.NodeType != XmlNodeType.Document)
            {
                context = string.Concat("/", node.Name, context);
                node = node.ParentNode;
            }
            return context;
        }

    }
}
