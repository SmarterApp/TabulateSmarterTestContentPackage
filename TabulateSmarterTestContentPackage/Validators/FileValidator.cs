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
        // Presently only checks for empty files.
        public static void Validate(ItemContext it, XmlDocument xml)
        {
            var checkedFiles = new HashSet<string>();

            // Enumerate all attachments and do type-specific validation
            var attachmentSourcePath = !it.IsStimulus
                ? "itemrelease/item/content/attachmentlist/attachment"
                : "itemrelease/passage/content/attachmentlist/attachment";
            foreach(XmlElement ele in xml.SelectNodes(attachmentSourcePath))
            {
                string type = ele.GetAttribute("type");
                string filename = ele.GetAttribute("file");

                // Attachments may be repeated across languages (ENU and ESN)
                if (!checkedFiles.Add(filename))
                {
                    continue;   // File already checked
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

            // Enumerate all files and ensure they are empty
            foreach (FileFile file in it.FfItem.Files)
            {
                if (file.Length == 0)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0199, $"filename='{file.Name}'");
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
