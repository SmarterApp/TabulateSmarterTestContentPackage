using System.Collections.Generic;
using System.Linq;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Utilities
{
    public static class BrailleUtility
    {
        public static BrailleSupport GetSupportByCode(IList<BrailleFile> files)
        {
            if (files.Count == 0)
            {
                return BrailleSupport.NONE;
            }
            if (ContainsCodes(files, BrailleCode.EXN, BrailleCode.ECN, BrailleCode.UXN, BrailleCode.UXT, BrailleCode.UCN, BrailleCode.UCT))
            {
                return BrailleSupport.Both6;
            }
            if (ContainsCodes(files, BrailleCode.EXL, BrailleCode.ECL, BrailleCode.UXL, BrailleCode.UCL))
            {
                return BrailleSupport.Both4;
            }
            if (ContainsCodes(files, BrailleCode.UXN, BrailleCode.UXT, BrailleCode.UCN, BrailleCode.UCT))
            {
                return BrailleSupport.UEB4;
            }
            if (ContainsCodes(files, BrailleCode.UXL, BrailleCode.UCL))
            {
                return BrailleSupport.UEB2;
            }
            if (ContainsCodes(files, BrailleCode.UXL, BrailleCode.UCL))
            {
                return BrailleSupport.UEB2;
            }
            if (ContainsCodes(files, BrailleCode.EXL, BrailleCode.ECL))
            {
                return BrailleSupport.EBAE2;
            }
            if (ContainsCodes(files, BrailleCode.ECN))
            {
                return BrailleSupport.EBAE1;
            }
            return BrailleSupport.UNEXPECTED;
        }

        public static bool ContainsCodes(IList<BrailleFile> files, params BrailleCode[] codes)
        {
            return codes.All(code => files.Any(x => x.Code == code));
        }

        public static string NormalizeBrailleFileType(string brailleFileType)
        {
            if (brailleFileType == null) return string.Empty;
            brailleFileType = brailleFileType.ToUpper();

            // Map legacy file types
            switch(brailleFileType)
            {
                case "UNCONTRACTED":
                    return "EXL";
                case "CONTRACTED":
                    return "ECL";
                case "NEMETH":
                    return "ECN";
                default:
                    return brailleFileType;
            }
        }
    }
}