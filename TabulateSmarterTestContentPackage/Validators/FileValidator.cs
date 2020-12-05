using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;

namespace TabulateSmarterTestContentPackage.Validators
{

    /// <summary>
    /// Validates all files in the item.
    /// </summary>
    static class FileValidator
    {
        // TODO: Potential enhancement would be to make sure every file is referenced in the item.
        // TODO: Consolidate attachment checks from braille and video tests
        // Checks for empty files, missing files, and for files that differ only in case.
        public static void Validate(ItemContext it, XmlDocument xml)
        {
            var checkedFiles = new Dictionary<string, string>();

            // Enumerate all attachments and do type-specific validation
            var attachmentSourcePath = !it.IsStimulus
                ? "itemrelease/item/content/attachmentlist/attachment"
                : "itemrelease/passage/content/attachmentlist/attachment";
            foreach(XmlElement ele in xml.SelectNodes(attachmentSourcePath))
            {
                string type = ele.GetAttribute("type");
                string filename = ele.GetAttribute("file");

                // Attachments may be repeated across languages (ENU and ESN)
                // Check whether attachments differ in case
                {
                    string prevName;
                    if (checkedFiles.TryGetValue(filename.ToLowerInvariant(), out prevName))
                    {
                        if (!string.Equals(filename, prevName, StringComparison.Ordinal))
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0206, $"location='item.xml attachmentlist' filename='{filename}' prevFilename='{prevName}'");
                        }

                        continue; // Don't do the other tests. They've already been performed on this file
                    }
                    else
                    {
                        checkedFiles.Add(filename.ToLowerInvariant(), filename);
                    }
                }

                FileFile ff;
                if (!it.FfItem.TryGetFile(filename, out ff))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0201, $"filename='{filename}' type='{type}'");
                    continue;
                }

                switch (type)
                {
                    case "PRN":
                        ValidateBraillePrn(it, ff, filename);
                        break;
                }
            }

            // Enumerate all files
            foreach (FileFile file in it.FfItem.Files)
            {
                // Ensure file is not empty
                if (file.Length == 0)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0199, $"filename='{file.Name}'");
                }

                // Ensure filename matches attachment name (when present) in case and that
                // there aren't multiple filenames that differe only in case.
                string prevName;
                if (checkedFiles.TryGetValue(file.Name.ToLowerInvariant(), out prevName))
                {
                    if (!string.Equals(file.Name, prevName, StringComparison.Ordinal))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0206, $"filename='{file.Name}' prevFilename='{prevName}'");
                    }
                }
                else
                {
                    checkedFiles.Add(file.Name.ToLowerInvariant(), file.Name);
                }
            }
        }

        static readonly byte[] s_expectedPrnHeader = { 0x1B, 0x04 };

        static void ValidateBraillePrn(ItemContext it, FileFile ff, string filename)
        {
            byte[] prnHeader;
            using (var stream = ff.Open())
            {
                prnHeader = new byte[s_expectedPrnHeader.Length];
                if (stream.Read(prnHeader, 0, 2) != s_expectedPrnHeader.Length
                    || !prnHeader.SequenceEqual(s_expectedPrnHeader))
                {
                     ReportingUtility.ReportError(it, ErrorId.T0202, $"filename='{filename}' expected='{HashValue.ToHex(s_expectedPrnHeader)}' found='{HashValue.ToHex(prnHeader)}'");
                }
            }
        }

    }
}
