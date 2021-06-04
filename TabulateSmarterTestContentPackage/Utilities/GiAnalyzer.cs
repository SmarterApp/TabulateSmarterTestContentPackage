using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
using System.IO;

namespace TabulateSmarterTestContentPackage
{
    static class GiAnalyzer
    {
        public static string GetGiSubtype(ItemContext it, XmlDocument xml)
        {
            // Find the .gax filename
            var filename = xml.XpEval("/itemrelease/item/RendererSpec/@filename");
            if (string.IsNullOrEmpty(filename))
            {
                ReportingUtility.ReportError(it, ErrorId.T0228, $"<RendererSpec> not found.");
                return null;
            }

            // Read the GAX
            FileFile ffGax;
            if (!it.FfItem.TryGetFile(filename, out ffGax))
            {
                ReportingUtility.ReportError(it, ErrorId.T0228, $"GAX file not found '{filename}'");
                return null;
            }

            var gax = new XmlDocument();
            using (StreamReader reader = new StreamReader(ffGax.Open(), Encoding.UTF8, true, 1024, false))
            {
                try
                {
                    gax.Load(reader);
                }
                catch (Exception err)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0228, $"GAX file invalid XML '{err.Message}'");
                    return null;
                }
            }

            bool hasHotspot = null != gax.SelectSingleNode("/Question/QuestionPart/HotSpots/Regions/Region/Event");

            if (hasHotspot) Console.WriteLine($"{it}: HotSpot");

            return null;
        }

    }
}
