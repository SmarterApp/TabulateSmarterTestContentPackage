using System;
using System.Text.RegularExpressions;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Utilities
{
    public static class BrailleUtility
    {
        const BrailleFormCode cBoth6 = BrailleFormCode.EXN | BrailleFormCode.ECN | BrailleFormCode.UXN | BrailleFormCode.UXT | BrailleFormCode.UCN | BrailleFormCode.UCT;
        const BrailleFormCode cBoth4 = BrailleFormCode.EXL | BrailleFormCode.ECL | BrailleFormCode.UXL | BrailleFormCode.UCL;
        const BrailleFormCode cUEB4 = BrailleFormCode.UXN | BrailleFormCode.UXT | BrailleFormCode.UCN | BrailleFormCode.UCT;
        const BrailleFormCode cUEB2 = BrailleFormCode.UXL | BrailleFormCode.UCL;
        const BrailleFormCode cEBAE2 = BrailleFormCode.EXL | BrailleFormCode.ECL;
        const BrailleFormCode cEBAE1 = BrailleFormCode.ECN;

        public static BrailleSupport GetSupportByCode(BrailleFormCode code)
        {
            switch (code)
            {
                case cBoth6:
                    return BrailleSupport.EBAE2_UEB4;
                case cBoth4:
                    return BrailleSupport.EBAE2_UEB2;
                case cUEB4:
                    return BrailleSupport.UEB4;
                case cUEB2:
                    return BrailleSupport.UEB2;
                case cEBAE2:
                    return BrailleSupport.EBAE2;
                case cEBAE1:
                    return BrailleSupport.EBAE1;
                case BrailleFormCode.NONE:
                    return BrailleSupport.NONE;
                default:
                    return BrailleSupport.UNEXPECTED;
            }
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
            switch(value)
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