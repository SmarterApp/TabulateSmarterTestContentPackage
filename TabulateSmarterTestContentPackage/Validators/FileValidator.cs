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

        static readonly char[] s_prohibitedFilenameChars = new char[] { '\'', '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

        // TODO: Potential enhancement would be to make sure every file is referenced in the item.
        // TODO: Consolidate attachment checks from braille and video tests
        // Checks for empty files, missing files, and for files that differ only in case.
        public static void Validate(ItemContext it, XmlDocument itemXml)
        {

            // Enumerate each content language
            var contentPath = !it.IsStimulus
                ? "itemrelease/item/content"
                : "itemrelease/passage/content";
            foreach(XmlElement content in itemXml.SelectNodes(contentPath))
            {
                var checkedFiles = new Dictionary<string, string>();
                string language = content.GetAttribute("language");

                // Enumerage each attachment
                foreach(XmlElement attachment in content.SelectNodes("attachmentlist/attachment"))
                {
                    string type = attachment.GetAttribute("type");
                    string filename = attachment.GetAttribute("file");

                    // Enumerate each source
                    int sourceCount = 0;
                    foreach(XmlElement source in attachment.SelectNodes("source"))
                    {
                        ++sourceCount;
                        ValidateFile(it, source.GetAttribute("src"), checkedFiles, type, language);
                    }

                    // If no sources, validate on the primary node
                    if (sourceCount == 0)
                    {
                        ValidateFile(it, attachment.GetAttribute("file"), checkedFiles, type, language);
                    }
                }

            }

            // Enumerate all files
            {
                var checkedFiles = new Dictionary<string, string>();

                foreach (FileFile file in it.FfItem.Files)
                {
                    // Check whether any prohibited character is in the filename
                    {
                        int n = file.Name.IndexOfAny(s_prohibitedFilenameChars);
                        if (n >= 0)
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0224, $"filename='{file.Name}' prohibitedCharacter='{file.Name[n]}'");
                        }
                    }

                    // Ensure file is not empty
                    if (file.Length == 0)
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0199, $"filename='{file.Name}'");
                    }

                    // Ensure that there aren't multiple filenames that differ only in case.
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
        }

        static void ValidateFile(ItemContext it, string filename, Dictionary<string, string> checkedFiles, string type, string language)
        {
            // Check whether a repeat, or differs only in case.
            string prevName;
            if (checkedFiles.TryGetValue(filename.ToLowerInvariant(), out prevName))
            {
                if (!string.Equals(filename, prevName, StringComparison.Ordinal))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0206, $"location='item.xml attachmentlist' filename='{filename}' prevFilename='{prevName}' type='{type}' language='{language}'");
                }
                else
                {
                    ReportingUtility.ReportError(it, ErrorId.T0222, $"location='item.xml attachmentlist' filename='{filename}' type='{type}' language='{language}'");
                }
            }
            else
            {
                checkedFiles.Add(filename.ToLowerInvariant(), filename);
            }

            FileFile ff;
            if (!it.FfItem.TryGetFile(filename, out ff))
            {
                ReportingUtility.ReportError(it, ErrorId.T0201, $"filename='{filename}' type='{type}'");
            }
            else if (!filename.Equals(ff.Name, StringComparison.Ordinal))
            {
                ReportingUtility.ReportError(it, ErrorId.T0206, $"location='item.xml attachmentlist' attachmentName='{filename}' physicalFile='{ff.Name}' type='{type}' language='{language}'");
            }

            switch (type)
            {
                case "PRN":
                    ValidateBraillePrn(it, ff);
                    break;
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
