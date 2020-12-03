using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.XPath;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
using System.Diagnostics;


namespace TabulateSmarterTestContentPackage.Validators
{
    static class GaxValidator
    {
        const string c_backgroundImageXpath = "/Question/QuestionPart/ImageSpec/FileSpec";
        const string c_interactiveImageXpath = "/Question/QuestionPart/HotSpots/Regions/Region/Event/Image/@src";

        public static void Validate(ItemContext it, IXPathNavigable gaxXml, string filename)
        {
            var root = gaxXml.CreateNavigator();

            // Same file may be referenced more than once so we first collect the names
            var files = new Dictionary<string, string>();

            foreach(XPathNavigator node in root.Select(c_backgroundImageXpath))
            {
                files[node.Value] = "background";
            }
            foreach (XPathNavigator node in root.Select(c_interactiveImageXpath))
            {
                files[node.Value] = "interactive";
            }

            foreach(var pair in files)
            {
                FileFile ff;
                if (!it.FfItem.TryGetFile(pair.Key, out ff))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0221, $"imageName='{pair.Key}' gaxName='{filename}' refType='{pair.Value}'");
                }

                // Check for upper/lower case match
                else if (!ff.Name.Equals(pair.Key, StringComparison.Ordinal))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0206, $"gaxFile='{filename}' reference='{pair.Key}' filename='{ff.Name}'");
                }
            }
        }

    }
}
