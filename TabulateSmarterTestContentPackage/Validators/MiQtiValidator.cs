using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.XPath;
using System.Diagnostics;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;

namespace TabulateSmarterTestContentPackage.Validators
{
    static class MiQtiValidator
    {
        const string unboldedChoiceXpath = "//*[not(local-name()='strong')]/p[not(@class='languagedivider')" +
            " and not(*[local-name()='strong'])]/ancestor::simpleAssociableChoice";

        public static void Validate(ItemContext it, string language, string qtiContent)
        {
            // Parse as xml
            XPathDocument xmlDoc;
            try
            {
                using (var reader = new System.IO.StringReader(qtiContent))
                {
                    xmlDoc = new XPathDocument(reader);
                }
            }
            catch (Exception err)
            {
                ReportingUtility.ReportError(it, ErrorId.T0218, $"language='{language}' node='content/qti' error='{err.Message}'");
                return;
            }

            // Look for any instance of an unbolded heading in the match
            foreach(XPathNavigator nav in xmlDoc.CreateNavigator().Select(unboldedChoiceXpath))
            {
                ReportingUtility.ReportError(it, ErrorId.T0219, $"language='{language}' identifier='{nav.GetAttribute("identifier", string.Empty)}'");
            }
        }
    }
}
