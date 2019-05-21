using System;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
using System.Collections.Generic;

namespace TabulateSmarterTestContentPackage.Validators
{
    public static class AslVideoValidator
    {
        public static void Validate(ItemContext it, IXPathNavigable xml, int englishCharacterCount, StatAccumulator accumulator)
        {
            
            var attachmentFilename = FileUtility.GetAttachmentFilename(it, xml, "ASL");

            // so far, the attachmentFilename is the value from the <attachment> element. What needs to be checked is the following:
            // 1. does the attachmentFilename match the value in the <source> element src attribute value?
            // 2. are there two <source> elements?
            // 3. do the files in the <source> elements exist?

            if (string.IsNullOrEmpty(attachmentFilename)) {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                    "ASL video file name missing from the item attachment 'file' attribute.");            
                return;
            }

            var attachmentSourcePath = !it.IsStimulus
                                        ? "itemrelease/item/content/attachmentlist/attachment[@type='ASL']"
                                        : "itemrelease/passage/content/attachmentlist/attachment[@type='ASL']";
            var xmlEle = xml.CreateNavigator().SelectSingleNode(attachmentSourcePath);
            var aslFileNames = new List<string>();
            if (xmlEle.HasChildren)
            {                
                xmlEle.MoveToFirstChild();
                aslFileNames.Add(xmlEle.GetAttribute("src", ""));
                while (xmlEle.MoveToNext())
                {
                    aslFileNames.Add(xmlEle.GetAttribute("src", ""));
                }
            }
            else
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                    "ASL video files are missing from the attachment source list.", $"Filename: {attachmentFilename}");
                return;
            }
            
            // check if there are two source elements
            if (aslFileNames.Count != 2)
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                    "ASL video files must have 2 file references.", $"expected: 2 found: {aslFileNames.Count}");
                return;
            }

            // check if the MP4 file name in the <attachment> element matches at least one of the <source> elements
            if (!aslFileNames.Contains(attachmentFilename))
            {            
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                    "ASL video file name attribute value not found in source element.", $"Expected file name: {attachmentFilename}");                                               
            }

            // check if the file exists
            foreach(string currentSource in aslFileNames)
            {
                if (!it.FfItem.FileExists(currentSource))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                        "ASL video file is missing.", $"Filename: {currentSource}");
                }
            }
            
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