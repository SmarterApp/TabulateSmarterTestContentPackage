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

            // Hotspot
            bool hasHotspot = null != gax.SelectSingleNode("/Question/QuestionPart/HotSpots/Regions/Region/Event");

            // Graphing
            bool hasGraph = false;
            foreach(var button in gax.XpEvalE(" /Question/QuestionPart/Options/ShowButtons")
                .Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                switch (button.Trim().ToLowerInvariant())
                {
                    case "arrow":
                    case "arrw2":
                    case "circle":
                    case "connect":
                    case "dash":
                    case "point":
                        hasGraph = true;
                        break;
                }
            }

            // DragAndDrop
            bool hasDragAndDrop = null != gax.SelectSingleNode("/Question/QuestionPart/ObjectMenuIcons/IconSpec");

            Console.WriteLine($"{it}: {(hasHotspot ? "HS" : "  ")} {(hasGraph ? "GR" : "  ")} {(hasDragAndDrop ? "DD" : "  ")}");

            return null;
        }

    }
}
