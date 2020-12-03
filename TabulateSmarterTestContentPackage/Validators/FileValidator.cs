using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
using System.IO;

namespace TabulateSmarterTestContentPackage.Validators
{

    /// <summary>
    /// Validates all files in the item.
    /// </summary>
    static class FileValidator
    {
        const string c_mathMlRoot = "math";
        const string c_mathMlNamespace = "http://www.w3.org/1998/Math/MathML";

        // TODO: Potential enhancement would be to make sure every file is referenced in the item.
        // TODO: Consolidate attachment checks from braille and video tests
        // Checks for empty files, missing files, and for files that differ only in case.
        public static void Validate(ItemContext it, XmlDocument itemXml)
        {
            var checkedFiles = new Dictionary<string, string>();

            // Enumerate all attachments and do type-specific validation
            var attachmentSourcePath = !it.IsStimulus
                ? "itemrelease/item/content/attachmentlist/attachment"
                : "itemrelease/passage/content/attachmentlist/attachment";
            foreach(XmlElement ele in itemXml.SelectNodes(attachmentSourcePath))
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
                        ValidateBraillePrn(it, ff);
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

                // Ensure filename matches attachment name (when present) in upper/lower case and that
                // there aren't multiple filenames that differ only in case.
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

                // Extension-specific validation
                switch (Path.GetExtension(file.Name).ToLowerInvariant())
                {
                    case ".xml":
                    case ".eax":
                    case ".qrx":
                    case ".gax":
                        // Don't bother testing if the item or metadata file as those are validated separately.
                        if (Path.GetFileNameWithoutExtension(file.Name).Equals(it.FullId, StringComparison.OrdinalIgnoreCase)
                            || file.Name.Equals("metadata.xml", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                        ValidateXmlFile(it, file, itemXml);
                        break;
                }
            }
        }

        static readonly byte[] s_expectedPrnHeader = { 0x1B, 0x04 };

        static void ValidateBraillePrn(ItemContext it, FileFile ff)
        {
            byte[] prnHeader;
            using (var stream = ff.Open())
            {
                prnHeader = new byte[s_expectedPrnHeader.Length];
                if (stream.Read(prnHeader, 0, 2) != s_expectedPrnHeader.Length
                    || !prnHeader.SequenceEqual(s_expectedPrnHeader))
                {
                     ReportingUtility.ReportError(it, ErrorId.T0202, $"filename='{ff.Name}' expected='{HashValue.ToHex(s_expectedPrnHeader)}' found='{HashValue.ToHex(prnHeader)}'");
                }
            }
        }

        static void ValidateXmlFile(ItemContext it, FileFile ff, XmlDocument itemXml)
        {
            XPathDocument xmlFile;
            try
            {
                using (Stream stream = ff.Open())
                {
                    xmlFile = new XPathDocument(stream);
                }
            }
            catch (Exception err)
            {
                ReportingUtility.ReportError(it, ErrorId.T0214, $"filename='{ff.Name}' error='{err.Message}'");
                return;
            }

            // Validate gax file
            if (Path.GetExtension(ff.Name).Equals(".gax", StringComparison.OrdinalIgnoreCase))
            {
                GaxValidator.Validate(it, xmlFile, ff.Name);
            }

            // See if this is MathMl
            var nav = xmlFile.CreateNavigator();
            nav.MoveToFirstChild();
            //Debug.WriteLine($"Filename: {ff.Name} XML Root: {nav.LocalName}");
            if (nav.LocalName.Equals(c_mathMlRoot, StringComparison.Ordinal)
                && nav.NamespaceURI.Equals(c_mathMlNamespace, StringComparison.Ordinal))
            {
                MathMlValidator.Validate(it, xmlFile, ff.Name, itemXml);
            }

        }

    }
}
