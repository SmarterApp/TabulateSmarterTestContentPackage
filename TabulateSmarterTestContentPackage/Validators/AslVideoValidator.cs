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
        public static void Validate(ItemContext it, IXPathNavigable xml, int englishCharacterCount, StatAccumulator accumulator)
        {
            var attachmentFilename = FileUtility.GetAttachmentFilename(it, xml, "ASL");
            if (string.IsNullOrEmpty(attachmentFilename)) return;

            ValidateFilename(attachmentFilename, it);

            FileFile file;
            if (!it.FfItem.TryGetFile(attachmentFilename, out file)) return;

            double videoSeconds;
            using (var stream = file.Open())
            {
                videoSeconds = Mp4VideoUtility.GetDuration(stream) / 1000.0;
            }
            if (videoSeconds <= 0.0) return;

            double secondToCountRatio = videoSeconds / englishCharacterCount;

            var highStandard = TabulatorSettings.AslMean +
                                TabulatorSettings.AslStandardDeviation * TabulatorSettings.AslToleranceInStdev;
            var lowStandard = TabulatorSettings.AslMean -
                                TabulatorSettings.AslStandardDeviation * TabulatorSettings.AslToleranceInStdev;

            if (secondToCountRatio > highStandard
                || secondToCountRatio < lowStandard)
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                    "ASL video length doesn't correlate with text length; possible mismatch.",
                    $"videoSeconds={videoSeconds:F3} characterCount={englishCharacterCount} ratio={secondToCountRatio:F3} meanRatio={TabulatorSettings.AslMean} tolerance={TabulatorSettings.AslToleranceInStdev*TabulatorSettings.AslStandardDeviation:F3}");
            }

            accumulator.AddDatum(secondToCountRatio);
        }

        public static int GetContentLengthInCharacters(XmlDocument xmlDocument)
        {
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
            return characterCount ?? 0;
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