using System;
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
    public static class AslVideoValidator
    {
        public static void Validate(FileFolder baseFolder, ItemContext itemContext, XDocument xDocument)
        {
            var attachmentFile = FileUtility.GetAttachmentFilename(itemContext, xDocument, "ASL");
            FileFolder ffItems;
            if (baseFolder.TryGetFolder("Items", out ffItems))
            {
                if (!string.IsNullOrEmpty(attachmentFile))
                {
                    ValidateFilename(attachmentFile, itemContext);
                    try
                    {
                        double videoSeconds = Mp4VideoUtility.GetDuration(
                                                  Path.Combine(((FsFolder) ffItems).mPhysicalPath,
                                                      itemContext.FfItem.Name,
                                                      attachmentFile)) / 1000;
                        var cData = CDataExtractor.ExtractCData(xDocument
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
                        var highStandard = ConfigurationOptions.AslMean +
                                           ConfigurationOptions.AslStandardDeviation * ConfigurationOptions.AslTolerance;
                        var lowStandard = ConfigurationOptions.AslMean -
                                          ConfigurationOptions.AslStandardDeviation * ConfigurationOptions.AslTolerance;
                        if (secondToCountRatio > highStandard
                            || secondToCountRatio < lowStandard)
                        {
                            ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded,
                                "ASL enabled element's video length to character count ratio is too far from the mean value",
                                $"Video Length (seconds): {videoSeconds} Character Count: {characterCount} Ratio: {secondToCountRatio} " +
                                $"Standard Deviation Tolerance: {ConfigurationOptions.AslTolerance} Standard Deviation: {ConfigurationOptions.AslStandardDeviation} " +
                                $"Mean: {ConfigurationOptions.AslMean}");
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
            else
            {
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe,
                    "Unable to load item directory");
            }
        }

        private static void ValidateFilename(string fileName, ItemContext itemContext)
        {
            const string pattern =
                @"(([Ss][Tt][Ii][Mm])|([Pp][Aa][Ss][Ss][Aa][Gg][Ee])|([Ii][Tt][Ee][Mm]))_(\d+)_ASL_STEM\.[Mm][Pp]4";
            var matches = Regex.Matches(fileName, pattern).Cast<Match>().ToList();
            if (Regex.IsMatch(fileName, pattern))
            {
                if (itemContext.IsPassage &&
                    matches[0].Groups[1].Value.Equals("passage", StringComparison.OrdinalIgnoreCase))
                {
                    // Should be stim, but is passage
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Benign,
                        "ASL video filename for stim is titled as 'passsage' instead of 'stim'", $"Filename: {fileName}");
                }
                if (!matches[0].Groups[5].Value.Equals(itemContext.ItemId, StringComparison.OrdinalIgnoreCase))
                {
                    // Incorrect ItemId
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe,
                        "ASL video filename contains an incorrect ID",
                        $"Filename: {fileName} Expected ID: {itemContext.ItemId}");
                }
                if (itemContext.IsPassage &&
                    matches[0].Groups[1].Value.Equals("item", StringComparison.OrdinalIgnoreCase))
                {
                    // Item video in stim
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe,
                        "ASL video filename indicates item, but base folder is a stim", $"Filename: {fileName}");
                }
                else if (!itemContext.IsPassage &&
                         (matches[0].Groups[1].Value.Equals("stim", StringComparison.OrdinalIgnoreCase)
                          || matches[0].Groups[1].Value.Equals("passage", StringComparison.OrdinalIgnoreCase)))
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