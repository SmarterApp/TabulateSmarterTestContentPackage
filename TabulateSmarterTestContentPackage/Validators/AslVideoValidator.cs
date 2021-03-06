using System;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
using System.Collections.Generic;
using System.IO;

namespace TabulateSmarterTestContentPackage.Validators
{
    public static class AslVideoValidator
    {
        const int c_minVideoFileSize = 128;
        const long c_maxVideoKbps = 400;
        const long c_minVideoKbps = 100;

        public static void Validate(ItemContext it, IXPathNavigable xml)
        {

            var attachmentFilename = FileUtility.GetAttachmentFilename(it, xml, "ASL");

            // so far, the attachmentFilename is the value from the <attachment> element. What needs to be checked is the following:
            // 1. does the attachmentFilename match the value in the <source> element src attribute value?
            // 2. are there two <source> elements?
            // 3. do the files in the <source> elements exist?

            if (string.IsNullOrEmpty(attachmentFilename)) {
                ReportingUtility.ReportError(it, ErrorId.T0173);            
                return;
            }

            var attachmentSourcePath = !it.IsStimulus
                                        ? "itemrelease/item/content/attachmentlist/attachment[@type='ASL']"
                                        : "itemrelease/passage/content/attachmentlist/attachment[@type='ASL']";
            var xmlEle = xml.CreateNavigator().SelectSingleNode(attachmentSourcePath);
            var aslFileNames = new List<string>();
            if (xmlEle.MoveToFirstChild())
            {
                do
                {
                    string filename = xmlEle.GetAttribute("src", string.Empty);
                    string extension = Path.GetExtension(filename).Substring(1);
                    string type = xmlEle.GetAttribute("type", string.Empty);
                    int n = type.IndexOf(';');  // Sometimes there's a codec appendix
                    if (n >= 0) type = type.Substring(0, n).Trim();                      
                    if (!string.Equals(type, "video/" + extension))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0213, $"filename='{filename}' type-'{type}'");
                    }
                    aslFileNames.Add(filename);
                }
                while (xmlEle.MoveToNext());
            }
            else
            {
                ReportingUtility.ReportError(it, ErrorId.T0174, $"filename='{attachmentFilename}'");
                return;
            }
            
            // check if there are two source elements
            if (aslFileNames.Count != 2)
            {
                ReportingUtility.ReportError(it, ErrorId.T0106, $"expected='2' found='{aslFileNames.Count}'");
                return;
            }

            if (!aslFileNames.Contains(attachmentFilename))
            {
                System.Diagnostics.Debug.WriteLine(attachmentFilename);
                foreach(var src in aslFileNames)
                {
                    System.Diagnostics.Debug.WriteLine(src);
                }
                System.Diagnostics.Debug.WriteLine(string.Empty);
            }

            // Validate each of the files
            bool mp4Found = false;
            bool webmFound = false;
            long duration = 0; // in milliseconds
            long mp4Size = 0;
            long webmSize = 0;
            foreach (string currentSource in aslFileNames)
            {
                // Check the filename
                ValidateFilename(currentSource, it);

                if (Path.GetExtension(currentSource).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                    mp4Found = true;
                if (Path.GetExtension(currentSource).Equals(".webm", StringComparison.OrdinalIgnoreCase))
                    webmFound = true;

                // Make sure it exists
                FileFile ft;
                if (!it.FfItem.TryGetFile(currentSource, out ft))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0176, $"filename='{currentSource}'");
                }
                else if (ft.Length < c_minVideoFileSize)
                {
                     ReportingUtility.ReportError(it, ErrorId.T0197, $"filename='{currentSource}'");
                }
                else
                {
                    switch (Path.GetExtension(ft.Name).ToLowerInvariant())
                    {
                        case ".mp4":
                            mp4Size = ft.Length;
                            using (var stream = ft.Open())
                            {
                                duration = Mp4VideoUtility.GetDuration(stream);
                            }
                            break;

                        case ".webm":
                            webmSize = ft.Length;
                            break;
                    }
                }
            }

            // Report if either type was not found
            if (!mp4Found) ReportingUtility.ReportError(it, ErrorId.T0174, $"type='.mp4' filename='{attachmentFilename}'");
            if (!webmFound) ReportingUtility.ReportError(it, ErrorId.T0174, $"type='.webm' filename='{attachmentFilename}'");

            // Check bitrates
            ValidateBitrate(it, mp4Size, duration, ".mp4", attachmentFilename);
            ValidateBitrate(it, webmSize, duration, ".mp4", attachmentFilename);
        }

        private static void ValidateBitrate(ItemContext it, long size, long duration, string extension, string filename)
        {
            if (size <= 0 || duration <= 0) return; // If size or duration is invalid, another error has already been reported
            long kbps = ((size * 8000L) / duration) / 1024L; // duration is in milliseconds 1024 to get kbps
            System.Diagnostics.Debug.WriteLine($"{kbps}kbps");
            if (kbps < c_minVideoKbps || kbps > c_maxVideoKbps)
            {
                ReportingUtility.ReportError(it, ErrorId.T0225,
                    $"filename='{Path.GetFileNameWithoutExtension(filename)}{extension}' duration={duration / 1000}s bitrate={kbps}kbps. Expected bitrate between {c_minVideoKbps}kbps and {c_maxVideoKbps}kbps");
            }
        }

        private static void ValidateFilename(string fileName, ItemContext itemContext)
        {
            const string pattern = @"((?:stim)|(?:passage)|(?:item))_(\d+)_ASL_STEM\.((?:mp4)|(?:webm))";
            const string reportPattern = @"<stim OR item OR passage>_<stim OR item ID>_ASL_STEM.<mp4 OR webm>";

            var match = Regex.Match(fileName, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (!match.Groups[2].Value.Equals(itemContext.ItemId.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    // Incorrect ItemId
                    ReportingUtility.ReportError(itemContext, ErrorId.T0177, $"filename='{fileName}' expectedId='{itemContext.ItemId}'");
                }
                if (itemContext.IsStimulus &&
                    match.Groups[1].Value.Equals("item", StringComparison.OrdinalIgnoreCase))
                {
                    // Item video in stim
                    ReportingUtility.ReportError(itemContext, ErrorId.T0178, $"filename='{fileName}'");
                }
                else if (!itemContext.IsStimulus &&
                         (match.Groups[1].Value.Equals("stim", StringComparison.OrdinalIgnoreCase)
                          || match.Groups[1].Value.Equals("passage", StringComparison.OrdinalIgnoreCase)))
                {
                    // Stim video in an item
                    ReportingUtility.ReportError(itemContext, ErrorId.T0179, $"filename='{fileName}'");
                }
            }
            else
            {
                ReportingUtility.ReportError(itemContext, ErrorId.T0180, $"filename='{fileName}' pattern='{reportPattern}'");
            }
        }
    }
}