using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Xml.XPath;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;

namespace TabulateSmarterTestContentPackage.Validators
{
    static class MathMlValidator
    {
        const string c_mathMlNamespace = "http://www.w3.org/1998/Math/MathML";

        const ErrorId c_locationUnknown = ErrorId.None;
        const ErrorId c_locationNoReference = ErrorId.T0215;    // Not referenced
        const ErrorId c_locationStudentFacing = ErrorId.T0216;  // In Student-Facing Content
        const ErrorId c_locationEducatorFacing = ErrorId.T0217; // In Educator-Facing Content

        /* Each of the following XPath expressions searches for an error in the MathML
         * text. The corresponding messages have XPath expressions wrapped in braces.
         * The message expressions are evaluated relative to the XPath node that matched
         * the error expression. The simplest form being {.} which simply corresponds
         * to the element itself. The inner text of the found element will be inserted
         * into the message in place of the expression.
         */

        // Xpath for hyphens contained in mtext
        const string c_mtextHyphenXpath = "//*[local-name()='mtext' and contains(text(),'-')]";

        const string c_mtextHyphenMessage = "mtext content includes hyphen '<mtext>{.}</mtext>'";

        // Xpath for a decimal separator wrapped in operator (mo) tag
        const string c_moDecimalXpath =
            "//m:mo[text()='.']" +
            "[preceding-sibling::m:mn[text()=number()]]" +
            "[following-sibling::m:mn[text()=number()]]";

        const string c_moDecimalMessage =
            "decimal separator in an operator tag: " +
            "'{./preceding-sibling::m:mn[1]}{.}{./following-sibling::m:mn[1]}' is written like " +
            "'<mn>{./preceding-sibling::m:mn[1]}</mn><mo>{.}</mo><mn>{./following-sibling::m:mn[1]}</mn>'";

        // Xpath for a place value separator wrapped in operator (mo) tag
        const string c_moPlaceValueXpath =
            "//m:mo[text()=',']" +
            "[preceding-sibling::m:mn[text()=number()]]" +
            "[following-sibling::m:mn[text()=number() and string-length()=3]]";

        const string c_moPlaceValueMessage =
            "place-value separator in an operator tag: " +
            "'{./preceding-sibling::m:mn[1]}{.}{./following-sibling::m:mn[1]}' is written like " +
            "'<mn>{./preceding-sibling::m:mn[1]}</mn><mo>{.}</mo><mn>{./following-sibling::m:mn[1]}</mn>'";

        // Xpath for MathML identifiers that should NOT be italic - this path explicitly includes some symbols, but those symbols should actually be contained in <mo> tags
        const string c_italicsNotPermittedXpath =
            "//m:mi[not(@mathvariant='normal') and text()='.']" +
            "|//m:mi[not(@mathvariant='normal') and text()=',']" +
            "|//m:mi[not(@mathvariant='normal') and text()='?']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u25A1']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u00F7']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u00B0']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u2220']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u25CB']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u0394']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u2013']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u2264']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u2265']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u2022']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u003C']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u003E']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u03C0']" +
            "|//m:mi[not(@mathvariant='normal') and text()='\u2212']";

        const string c_italicsNotPermittedMessage = "symbol should not be italicized: '{.}'";

        // Xpath for single-character MathML identifier tags with non-italic text - the path below
        // excludes some symbols, but those symbols should actually be contained in <mo> tags
        const string c_italicsRequiredXpath =
            "//m:mi[@mathvariant='normal' and string-length(text())=1 " +
            "and not(text()='\u25A1') " +
            "and not(text()='.') " +
            "and not(text()=',') " +
            "and not(text()='?') " +
            "and not(text()='$') " +
            "and not(text()='\u00F7') " +
            "and not(text()='\u00B0') " +
            "and not(text()='\u2220') " +
            "and not(text()='\u25CB') " +
            "and not(text()='\u0394') " +
            "and not(text()='\u2013') " +
            "and not(text()='\u2264') " +
            "and not(text()='\u2265') " +
            "and not(text()='\u2022') " +
            "and not(text()='\u003C') " +
            "and not(text()='\u003E') " +
            "and not(text()='\u03C0') " +
            "and not(text()='\u2212')]";

        const string c_italicsRequiredMessage = "text should be italicized: '{.}'";

        // Xpath for characters that we do not expect in function names, variables, and symbolic constants
        const string c_variableNotPermittedXpath = "//m:mi[contains(text(), '\u03A0')]";

        const string c_variableNotPermittedMessage = "unexpected character in name or symbol: '{.}'";

        // Xpath for phantom text
        const string c_phantomXpath = "//*[local-name()='mphantom']";

        const string c_phantomMessage = "phantom text: '<mphantom>{.}</mphantom>'";

        static readonly string[,] s_tests = new string[,]
        {
            { c_mtextHyphenXpath,  c_mtextHyphenMessage },
            { c_moDecimalXpath,  c_moDecimalMessage },
            { c_moPlaceValueXpath, c_moPlaceValueMessage },
            { c_italicsNotPermittedXpath, c_italicsNotPermittedMessage },
            { c_italicsRequiredXpath, c_italicsRequiredMessage },
            { c_variableNotPermittedXpath, c_variableNotPermittedMessage },
            { c_phantomXpath, c_phantomMessage }
        };

        static readonly System.Xml.XmlNamespaceManager s_nsmgr;

        static MathMlValidator()
        {
            s_nsmgr = new System.Xml.XmlNamespaceManager(Tabulator.XmlNt);
            s_nsmgr.AddNamespace("m", c_mathMlNamespace);
        }

        public static void Validate(ItemContext it, IXPathNavigable mathXml, string filename, IXPathNavigable itemXml)
        {
            ErrorId location = c_locationUnknown;
            string language = null;

            var root = mathXml.CreateNavigator();
            for(int i=0; i<s_tests.GetLength(0); ++i)
            {
                foreach (XPathNavigator node in root.Select(s_tests[i, 0], s_nsmgr))
                {
                    string msg = XPathFormatMessage(node, s_tests[i, 1]);

                    // Determine where this is referenced in the item
                    LocateMathMlReference(filename, itemXml, ref location, ref language);

                    // Report the error
                    ReportingUtility.ReportError(it, location,
                        $"In '{filename}' lang '{language}', {msg}");
                }
            }

            static string XPathFormatMessage(XPathNavigator node, string text)
            {
                var sb = new StringBuilder();
                int i = 0;
                for (; ; )
                {
                    int b1 = text.IndexOf('{', i);
                    if (b1 <= 0) break;
                    int b2 = text.IndexOf('}', b1);
                    if (b2 <= 0) break;

                    sb.Append(text, i, b1-i);
                    string xp = text.Substring(b1 + 1, b2 - b1 - 1);

                    object eval = node.Evaluate(xp, s_nsmgr);
                    var xpni = eval as XPathNodeIterator;
                    if (xpni != null)
                    {
                        if (xpni.MoveNext())
                            sb.Append(xpni.Current.InnerXml);
                    }
                    else if (eval != null)
                    {
                        sb.Append(eval.ToString());
                    }

                    i = b2 + 1;
                }
                sb.Append(text, i, text.Length-i);

                return sb.ToString();
            }

            const string c_studentHtmlCDataXPath = "/itemrelease/*[local-name()='item' or local-name()='passage']/content/stem|/itemrelease/item/content/optionlist/option/val|/itemrelease/item/keywordList/keyword/html";
            const string c_educatorHtmlCDataXPath = "/itemrelease/item/content/rubriclist/rubric/val|/itemrelease/item/content/rationaleoptlist/rationale/val|/itemrelease/item/content/rubriclist/samplelist/sample/samplecontent";
            const string c_ancestorLanguageXPath = "ancestor::content/@language";
            const string c_anyLanguage = "any";

            // Reports the first location the item is referenced
            static void LocateMathMlReference(string filename, IXPathNavigable itemXml, ref ErrorId location, ref string language)
            {
                // If it has already been determined, just return
                if (location != c_locationUnknown) return;

                string rootname = System.IO.Path.GetFileNameWithoutExtension(filename);
                var itemRoot = itemXml.CreateNavigator();

                // Check each student-facing section
                foreach (XPathNavigator htmlNode in itemRoot.Select(c_studentHtmlCDataXPath))
                {
                    // Found a reference
                    if (htmlNode.Value.Contains(rootname, StringComparison.OrdinalIgnoreCase))
                    {
                        location = c_locationStudentFacing;
                        language = htmlNode.SelectSingleNode(c_ancestorLanguageXPath)?.Value ?? c_anyLanguage;
                        return;
                    }
                }

                // Check each educator-facing section
                foreach (XPathNavigator htmlNode in itemRoot.Select(c_educatorHtmlCDataXPath))
                {
                    // Found a reference
                    if (htmlNode.Value.Contains(rootname, StringComparison.OrdinalIgnoreCase))
                    {
                        location = c_locationEducatorFacing;
                        language = htmlNode.SelectSingleNode(c_ancestorLanguageXPath)?.Value ?? c_anyLanguage;
                        return;
                    }
                }

                location = c_locationNoReference;
                language = c_anyLanguage;
            }

        }
    }
}
