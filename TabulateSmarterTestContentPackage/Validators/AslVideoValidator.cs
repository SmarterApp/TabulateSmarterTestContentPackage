using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using TabulateSmarterTestContentPackage.Extensions;
using TabulateSmarterTestContentPackage.Extractors;
using TabulateSmarterTestContentPackage.Mappers;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;

namespace TabulateSmarterTestContentPackage.Validators
{
    public static class AslVideoValidator
    {
        public static void Validate(ItemContext itemContext, XmlDocument xmlDocument)
        {
            var attachmentFile = FileUtility.GetAttachmentFilename(itemContext, xmlDocument, "ASL");

            if (!string.IsNullOrEmpty(attachmentFile))
            {
                ValidateFilename(attachmentFile, itemContext);
                try
                {
                    double videoSeconds = -1.0;
                    using (var stream = itemContext.FfItem.GetFile(attachmentFile).Open())
                    {
                        videoSeconds = Mp4VideoUtility.GetDuration(stream) / 1000.0;
                    }
                    var cData = CDataExtractor.ExtractCData(xmlDocument.MapToXDocument()
                        .XPathSelectElement("itemrelease/item/content[@language='ENU']/stem"))?.FirstOrDefault();
                    int? characterCount = 0;
                    if (cData != null)
                    {
                        var cDataSection = new XDocument().LoadXml($"<root>{cData.Value}</root>");
                        characterCount = cDataSection.DescendantNodes()
                            .Where(x => x.NodeType == XmlNodeType.Text)
                            .Sum(x => x.ToString().Length);
                    }
                    if (characterCount == null || characterCount == 0)
                    {
                        ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded,
                            "ASL enabled element does not contain an english language stem");
                        return;
                    }
                    var secondToCountRatio = videoSeconds / characterCount;
                    var highStandard = TabulatorSettings.AslMean +
                                       TabulatorSettings.AslStandardDeviation * TabulatorSettings.AslTolerance;
                    var lowStandard = TabulatorSettings.AslMean -
                                      TabulatorSettings.AslStandardDeviation * TabulatorSettings.AslTolerance;
                    if (secondToCountRatio > highStandard
                        || secondToCountRatio < lowStandard)
                    {
                        ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded,
                            "ASL enabled item's video length doesn't correlate with text length - likely mismatch.",
                            $"Video Length (seconds): {videoSeconds} Character Count: {characterCount} Ratio: {secondToCountRatio} " +
                            $"Standard Deviation Tolerance: {TabulatorSettings.AslTolerance} Standard Deviation: {TabulatorSettings.AslStandardDeviation} " +
                            $"Mean: {TabulatorSettings.AslMean}");
                    }
                }
                catch (Exception ex)
                {
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe,
                        "An error occurred when attempting to process an ASL video"
                        , $"Filename: {attachmentFile} Exception: {ex.Message}");
                }
            }
            else
            {
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe,
                    "Unable to locate valid video file for item",
                    attachmentFile ?? "Attachment filename does not exist");
            }
        }

        private static void ValidateFilename(string fileName, ItemContext itemContext)
        {
            const string pattern = @"((stim)|(passage)|(item))_(\d+)_ASL.*\.mp4";
            var match = Regex.Match(fileName, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (itemContext.IsStimulus &&
                    match.Groups[1].Value.Equals("passage", StringComparison.OrdinalIgnoreCase))
                {
                    // Should be stim, but is passage
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Benign,
                        "ASL video filename for stim is titled as 'passsage' instead of 'stim'", $"Filename: {fileName}");
                }
                if (!match.Groups[5].Value.Equals(itemContext.ItemId.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    // Incorrect ItemId
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe,
                        "ASL video filename contains an incorrect ID",
                        $"Filename: {fileName} Expected ID: {itemContext.ItemId}");
                }
                if (itemContext.IsStimulus &&
                    match.Groups[1].Value.Equals("item", StringComparison.OrdinalIgnoreCase))
                {
                    // Item video in stim
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe,
                        "ASL video filename indicates item, but base folder is a stim", $"Filename: {fileName}");
                }
                else if (!itemContext.IsStimulus &&
                         (match.Groups[1].Value.Equals("stim", StringComparison.OrdinalIgnoreCase)
                          || match.Groups[1].Value.Equals("passage", StringComparison.OrdinalIgnoreCase)))
                {
                    // Stim video in an item
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe,
                        "ASL video filename indicates stim, but base folder is a item", $"Filename: {fileName}");
                }
            }
            else
            {
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded,
                    "ASL video filename does not match expected pattern", $"Filename: {fileName} Pattern: {pattern}");
            }
        }
    }
}