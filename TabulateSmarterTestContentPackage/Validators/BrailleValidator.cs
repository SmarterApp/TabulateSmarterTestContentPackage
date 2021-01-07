using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;

namespace TabulateSmarterTestContentPackage.Validators
{

    static class BrailleValidator
    {
        // Expected Braille form patterns
        const BrailleFormCode cBoth6 = BrailleFormCode.EXN | BrailleFormCode.ECN | BrailleFormCode.UXN | BrailleFormCode.UXT | BrailleFormCode.UCN | BrailleFormCode.UCT;
        const BrailleFormCode cBoth4 = BrailleFormCode.EXL | BrailleFormCode.ECL | BrailleFormCode.UXL | BrailleFormCode.UCL;
        const BrailleFormCode cUEB4 = BrailleFormCode.UXN | BrailleFormCode.UXT | BrailleFormCode.UCN | BrailleFormCode.UCT;
        const BrailleFormCode cUEB2 = BrailleFormCode.UXL | BrailleFormCode.UCL;
        const BrailleFormCode cEBAE2 = BrailleFormCode.EXL | BrailleFormCode.ECL;
        const BrailleFormCode cEBAE1 = BrailleFormCode.ECN;

        // Permitted Braille forms by subject
        const BrailleFormCode cPermittedMath = BrailleFormCode.ECN | BrailleFormCode.EXN | BrailleFormCode.UCN | BrailleFormCode.UXN | BrailleFormCode.UCT | BrailleFormCode.UXT;
        const BrailleFormCode cPermittedEla = BrailleFormCode.ECL | BrailleFormCode.EXL | BrailleFormCode.UCL | BrailleFormCode.UXL;

        // Required Braille forms by subject
        const BrailleFormCode cRequredMath = BrailleFormCode.UCN | BrailleFormCode.UXN | BrailleFormCode.UCT | BrailleFormCode.UXT;
        const BrailleFormCode cRequiredEla = BrailleFormCode.UCL | BrailleFormCode.UCL;

        /// <summary>
        /// Validate braille content.
        /// </summary>
        /// <returns>The braille type for the Item or Stimulus report.</returns>
        public static string Validate(ItemContext it, XmlDocument xml, XmlDocument xmlMetadata)
        {
            // Retrieve and parse braille type metadata
            var brailleTypeMeta = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:BrailleType", Tabulator.XmlNsMgr);

            BrailleFileType brailleFileType = BrailleFileType.NONE;
            BrailleFormCode allForms = BrailleFormCode.NONE;
            BrailleFormCode allTranscriptForms = BrailleFormCode.NONE;

            // Enumerate all of the braille attachments
            {
                var type = it.IsStimulus ? "passage" : "item";
                var attachmentXPath = $"itemrelease/{type}/content[@language='ENU']/attachmentlist/attachment";
                var processedIds = new List<string>();

                foreach (XmlElement xmlEle in xml.SelectNodes(attachmentXPath))
                {
                    // All attachments must have an ID and those IDs must be unique within their item
                    var id = xmlEle.GetAttribute("id");
                    if (string.IsNullOrEmpty(id))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0138);
                    }
                    else if (processedIds.Contains(id))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0015, $"ID: {id}");
                    }
                    else
                    {
                        processedIds.Add(id);
                    }

                    // Get attachment type and check if braille
                    var attachType = xmlEle.GetAttribute("type");
                    if (string.IsNullOrEmpty(attachType))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0004);
                        continue;
                    }
                    BrailleFileType attachmentType;
                    if (!Enum.TryParse(attachType, out attachmentType))
                    {
                        continue; // Not braille attachment
                    }

                    // === From here forward we are only dealing with Braille attachments and the error messages reflect that ===

                    // Ensure that we are using consistent types
                    if (brailleFileType != BrailleFileType.NONE && brailleFileType != attachmentType)
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0139, $"previousType='{brailleFileType}' foundType='{attachmentType}'");
                    }
                    brailleFileType = attachmentType;

                    // Check that the file exists
                    var filename = xmlEle.GetAttribute("file");
                    if (string.IsNullOrEmpty(filename))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0140, "attachType='{0}'", attachType);
                        continue;
                    }
                    if (!it.FfItem.FileExists(filename))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0008, "attachType='{0}' Filename='{1}'", attachType, filename);
                    }

                    // Check the extension
                    var extension = Path.GetExtension(filename);
                    if (extension.Length > 0) extension = extension.Substring(1); // Strip leading "."
                    if (!string.Equals(extension, attachType, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0141, "extension='{0}' expected='{1}' filename='{2}'", extension, attachType, filename);
                    }

                    // Get and parse the subtype (if any) - This is the Braille Form Code (e.g. EXN, UXT)
                    var wholeSubtype = xmlEle.GetAttribute("subtype") ?? string.Empty;
                    bool isTranscript = wholeSubtype.EndsWith("_transcript", StringComparison.OrdinalIgnoreCase);
                    var subtype = isTranscript ? wholeSubtype.Substring(0, wholeSubtype.Length - 11) : wholeSubtype;
                    if (Program.gValidationOptions.IsEnabled("dbc") && subtype.StartsWith("TDS_BT_"))
                    {
                        subtype = subtype.Substring(7);
                        ReportingUtility.ReportError(it, ErrorId.T0142, $"prefix='TDS_BT_' subtype='{wholeSubtype}'");
                    }
                    BrailleFormCode attachmentFormCode;
                    if (!TryParseBrailleFormCode(subtype, out attachmentFormCode))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0007, $"subtype='{wholeSubtype}'");
                    }

                    // Accumulate the type
                    if (!isTranscript)
                    {
                        if ((allForms & attachmentFormCode) != 0)
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0143, $"brailleForm='{attachmentFormCode}'");
                        }
                        allForms |= attachmentFormCode;
                    }
                    else
                    {
                        if ((allTranscriptForms & attachmentFormCode) != 0)
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0143, $"brailleForm='{attachmentFormCode}_transcript'");
                        }
                        allTranscriptForms |= attachmentFormCode;
                    }

                    // Check for naming convention match
                    {
                        bool fnIsStim;
                        int fnItemId;
                        BrailleFormCode fnFormCode;
                        bool fnIsTranscript;
                        BrailleFileType fnFileType;
                        bool fnUsesAirConvention;
                        if (!TryParseBrailleFileNamingConvention(filename, out fnIsStim, out fnItemId, out fnFormCode, out fnIsTranscript, out fnFileType, out fnUsesAirConvention))
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0144, $"filename='{filename}'");
                        }
                        else
                        {
                            if (fnIsStim != it.IsStimulus)
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0145, $"value='{(fnIsStim ? "stim" : "item")}' expected='{(it.IsStimulus ? "stim" : "item")}' filename='{filename}'");
                            }

                            // ItemId
                            if (fnItemId != it.ItemId)
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0010, $"value='{fnItemId}' expected='{it.ItemId}' Filename='{filename}'");
                            }

                            // Form Code
                            if (fnFormCode != attachmentFormCode)
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0009, $"value='{fnFormCode}' expected='{attachmentFormCode}' filename='{filename}'");
                            }

                            // Check whether this is a transcript
                            if (fnIsTranscript != isTranscript)
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0146, $"value='{(fnIsTranscript ? "transcript" : string.Empty)}' expected='{(isTranscript ? "transcript" : string.Empty)}' filename='{filename}'");
                            }

                            if (fnFileType != attachmentType)
                            // Must match the type listed
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0147, $"extension='{fnFileType}' expected='{attachmentType}' filename='{filename}'");
                            }

                            /* TODO: This error occurs on all pre-UEB content. May enable in a later release.
                            if (fnUsesAirConvention)
                            {
                                ReportingUtility.ReportError("dbc", it, ErrorCategory.Item, ErrorSeverity.Benign,
                                    "Braille embossing filename uses deprecated naming convention",
                                    $"filename='{filename}'");
                            }
                            */
                        } // If naming convention parse success
                    } // Scope of naming convention validation

                } // foreach braille attachment
            } // scope for braille attachment enumeration

            // Check for consistency between body forms and transcript forms
            if (allTranscriptForms != BrailleFormCode.NONE && allTranscriptForms != allForms)
            {
                ReportingUtility.ReportError(it, ErrorId.T0148, $"transcriptForms='{allTranscriptForms}' stemForms='{allForms}'");
            }

            // Get the braille support code from what was found
            var brailleSupport = GetBrailleSupportByCode(allForms);

            string result;
            // Check for match with metadata
            // Metadata SHOULD take precedence over contents in the report. However, content may extend detail.
            if (string.Equals(brailleTypeMeta, "Not Braillable", StringComparison.OrdinalIgnoreCase))
            {
                if (allForms != BrailleFormCode.NONE)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0149, $"brailleTypes='{allForms}'");
                }
                brailleSupport = BrailleSupport.NOTBRAILLABLE;
                result = brailleSupport.ToString();
            }
            else if (string.IsNullOrEmpty(brailleTypeMeta))
            {
                if (allForms != BrailleFormCode.NONE)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0053, $"brailleTypes='{allForms.ToString()}'");
                }
                brailleSupport = BrailleSupport.NONE;
                result = string.Empty;
            }
            else if (brailleFileType == BrailleFileType.NONE)
            {
                ReportingUtility.ReportError(it, ErrorId.T0150, $"metadata='{brailleTypeMeta}'");
                result = string.Empty;
            }
            else
            {
                result = $"{brailleFileType}_{brailleSupport}";
                if (!brailleTypeMeta.StartsWith($"{brailleFileType}", StringComparison.OrdinalIgnoreCase))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0203, $"metadata='{brailleTypeMeta}' content='{result}'");
                }
            }


            // Retrieve the subject
            var subject = xml.XpEvalE($"itemrelease/{(it.IsStimulus ? "passage" : "item")}/attriblist/attrib[@attid='itm_item_subject']/val");

            // Verify the requred and acceptable braille file patterns
            if (allForms != BrailleFormCode.NONE)
            {
                BrailleFormCode permittedForms;
                BrailleFormCode requiredForms;
                switch (subject.ToLowerInvariant())
                {
                    case "math":
                        permittedForms = cPermittedMath;
                        requiredForms = cRequredMath;
                        break;
                    case "ela":
                        permittedForms = cPermittedEla;
                        requiredForms = cRequiredEla;
                        break;
                    default:
                        permittedForms = BrailleFormCode.AllEBAE | BrailleFormCode.AllUEB;
                        requiredForms = BrailleFormCode.NONE;
                        break;
                }

                var notPermitted = allForms & ~permittedForms;
                if (notPermitted != BrailleFormCode.NONE)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0204, $"formCode='{notPermitted}' subject='{subject}'");
                }

                var requriedNotPresent = requiredForms & ~allForms;
                if (requriedNotPresent != BrailleFormCode.NONE)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0205, $"formCode='{requriedNotPresent}' subject='{subject}'");
                }
            }

            return result;
        }

        public static BrailleSupport GetBrailleSupportByCode(BrailleFormCode code)
        {
            // Codes are for the expected patterns. If there's a mismatch, report the next best match.
            // Listed in priority order.
            if (code == BrailleFormCode.NONE) return BrailleSupport.NONE;
            if ((code & cBoth6) == cBoth6) return BrailleSupport.EBAE2_UEB4;
            if ((code & cBoth4) == cBoth4) return BrailleSupport.EBAE2_UEB2;
            if ((code & cUEB4) == cUEB4) return BrailleSupport.UEB4;
            if ((code & cUEB2) == cUEB2) return BrailleSupport.UEB2;
            if ((code & cEBAE2) == cEBAE2) return BrailleSupport.EBAE2;
            if ((code & cEBAE1) == cEBAE1) return BrailleSupport.EBAE1;
            return BrailleSupport.UNEXPECTED;
        }

        public static int BitCount(BrailleFormCode code)
        {
            uint n = (uint)code;
            int count = 0;
            while (n != 0)
            {
                if ((n & 0x0001) != 0) ++count;
                n = n >> 1;
            }
            return count;
        }

        public static string GetAirFormatMetadataByCode(BrailleFileType fileType, BrailleSupport code)
        {
            string form;
            switch (code)
            {
                case BrailleSupport.EBAE2_UEB4:
                case BrailleSupport.EBAE2_UEB2:
                    form = "EBAE_UEB";
                    break;
                case BrailleSupport.UEB4:
                case BrailleSupport.UEB2:
                    form = "UEB";
                    break;
                case BrailleSupport.EBAE2:
                case BrailleSupport.EBAE1:
                    form = "EBAE";
                    break;
                default:
                    form = code.ToString();
                    break;
            }
            return string.Concat(fileType.ToString(), "_", form);
        }

        public static bool TryParseBrailleFormCode(string value, out BrailleFormCode result)
        {
            if (value == null)
            {
                result = BrailleFormCode.NONE; // Default
                return false;
            }
            value = value.ToUpper();

            // Map legacy file types
            switch (value)
            {
                case "UNCONTRACTED":
                    result = BrailleFormCode.EXL;
                    return true;
                case "CONTRACTED":
                    result = BrailleFormCode.ECL;
                    return true;
                case "NEMETH":
                    result = BrailleFormCode.ECN;
                    return true;
                default:
                    return Enum.TryParse(value, out result);
            }
        }

        static Regex s_rxBrailleConvention = new Regex(@"(stim|item|passage)_(\d+)_(enu)_([a-z]{3})(_transcript)?\.(brf|prn)", RegexOptions.IgnoreCase);
        static Regex s_rxBrailleConventionAir = new Regex(@"(stim|item|passage)_(\d+)_(enu)_(uncontracted|contracted|nemeth|[a-z]{3})(_ueb)?(_transcript)?\.(brf|prn)", RegexOptions.IgnoreCase);
        static Regex s_rxBrailleConventionAir2 = new Regex(@"(stim|item|passage)_(\d+)_(enu)_(brf|prn)_(ebae|ueb)_(uncontracted|contracted)_(nemeth_|ueb_math_|)([a-z]{3})(_transcript)?\.(brf|prn)", RegexOptions.IgnoreCase);

        public static bool TryParseBrailleFileNamingConvention(string filename, out bool isStim, out int itemId, out BrailleFormCode formCode, out bool isTranscript, out BrailleFileType fileType, out bool usesAirConvention)
        {
            var match = s_rxBrailleConvention.Match(filename);
            if (match.Success)
            {
                isStim = !string.Equals(match.Groups[1].Value, "item", StringComparison.OrdinalIgnoreCase);
                itemId = int.Parse(match.Groups[2].Value);
                TryParseBrailleFormCode(match.Groups[4].Value, out formCode); // Sets value to None if it fails.
                isTranscript = string.Equals(match.Groups[5].Value, "_transcript", StringComparison.OrdinalIgnoreCase);
                if (!Enum.TryParse(match.Groups[6].Value.ToUpper(), out fileType))
                {
                    fileType = BrailleFileType.NONE;
                }
                usesAirConvention = false;
                return true;
            }

            match = s_rxBrailleConventionAir.Match(filename);
            if (match.Success)
            {
                isStim = !string.Equals(match.Groups[1].Value, "item", StringComparison.OrdinalIgnoreCase);
                itemId = int.Parse(match.Groups[2].Value);
                TryParseBrailleFormCode(match.Groups[4].Value, out formCode); // Sets value to None if it fails.
                if (string.Equals(match.Groups[5].Value, "_ueb", StringComparison.OrdinalIgnoreCase))
                {
                    switch (formCode)
                    {
                        case BrailleFormCode.ECL:
                            formCode = BrailleFormCode.UCL;
                            break;
                        case BrailleFormCode.ECN:
                            formCode = BrailleFormCode.UCN;
                            break;
                        case BrailleFormCode.EXL:
                            formCode = BrailleFormCode.UXL;
                            break;
                        case BrailleFormCode.EXN:
                            formCode = BrailleFormCode.UXN;
                            break;
                    }
                }
                isTranscript = string.Equals(match.Groups[6].Value, "_transcript", StringComparison.OrdinalIgnoreCase);
                if (!Enum.TryParse(match.Groups[7].Value.ToUpper(), out fileType))
                {
                    fileType = BrailleFileType.NONE;
                }
                usesAirConvention = true;
                return true;
            }

            match = s_rxBrailleConventionAir2.Match(filename);
            if (match.Success)
            {
                isStim = !string.Equals(match.Groups[1].Value, "item", StringComparison.OrdinalIgnoreCase);
                itemId = int.Parse(match.Groups[2].Value);
                TryParseBrailleFormCode(match.Groups[8].Value, out formCode); // Sets value to None if it fails.
                isTranscript = string.Equals(match.Groups[9].Value, "_transcript", StringComparison.OrdinalIgnoreCase);
                if (!Enum.TryParse(match.Groups[10].Value.ToUpper(), out fileType))
                {
                    fileType = BrailleFileType.NONE;
                }
                usesAirConvention = true;
                return true;
            }

            isStim = false;
            itemId = 0;
            formCode = BrailleFormCode.NONE;
            isTranscript = false;
            fileType = BrailleFileType.NONE;
            usesAirConvention = false;
            return false;
        }
    }
}
