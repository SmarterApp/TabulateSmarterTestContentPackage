using System;
using System.IO;
using System.Linq;
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
        public static void Validate(FileFolder baseFolder, ItemContext itemContext, XmlDocument xmlDocument)
        {
            var attachmentFile = FileUtility.GetAttachmentFilename(itemContext, xmlDocument, "ASL");
            FileFolder ffItems;
            if (baseFolder.TryGetFolder("Items", out ffItems))
            {
                if (!string.IsNullOrEmpty(attachmentFile))
                {
                    try
                    {
                        double videoSeconds = Mp4VideoUtility.GetDuration(
                                                  Path.Combine(((FsFolder) ffItems).mPhysicalPath,
                                                      itemContext.FfItem.Name,
                                                      attachmentFile)) / 1000;
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
                                           (TabulatorSettings.AslStandardDeviation * TabulatorSettings.AslTolerance);
                        var lowStandard = TabulatorSettings.AslMean -
                                          (TabulatorSettings.AslStandardDeviation * TabulatorSettings.AslTolerance);
                        if (secondToCountRatio > highStandard
                            || secondToCountRatio < lowStandard)
                        {
                            ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Degraded,
                                $"ASL enabled element's video length ({videoSeconds}) to character count ({characterCount}) ratio ({secondToCountRatio}) falls more than " +
                                $"{TabulatorSettings.AslTolerance} standard deviations ({TabulatorSettings.AslStandardDeviation}) from " +
                                $"the mean value ({TabulatorSettings.AslMean}).");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
                else
                {
                    ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe,
                                "Unable to locate valid video file for item", attachmentFile ?? "Attachment filename does not exist");
                }
            }
            else
            {
                ReportingUtility.ReportError(itemContext, ErrorCategory.Item, ErrorSeverity.Severe,
                                "Unable to load item directory");
            }
        }
    }
}
