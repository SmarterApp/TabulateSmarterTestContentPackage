using System.Collections.Generic;
using System.Linq;
using ContentPackageTabulator.Models;

namespace ContentPackageTabulator.Utilities
{
    public static class BrailleUtility
    {
        // Uncontracted == EXL && Contracted == ECL
        // Nemeth == ECN
        public static IList<BrailleSupport> GetSupportByCode(IList<BrailleFile> files)
        {
            var result = new List<BrailleSupport>();
            if (ContainsCodes(files, BrailleCode.EXN, BrailleCode.ECN, BrailleCode.UXN, BrailleCode.UXT, BrailleCode.UCN,
                    BrailleCode.UCT)
                ||
                ContainsCodes(files, BrailleCode.EXN, BrailleCode.NEMETH, BrailleCode.UXN, BrailleCode.UXT,
                    BrailleCode.UCN,
                    BrailleCode.UCT))
            {
                result.Add(BrailleSupport.Both6);
            }
            if (ContainsCodes(files, BrailleCode.EXL, BrailleCode.ECL, BrailleCode.UXL, BrailleCode.UCL)
                || ContainsCodes(files, BrailleCode.UNCONTRACTED, BrailleCode.ECL, BrailleCode.UXL, BrailleCode.UCL)
                || ContainsCodes(files, BrailleCode.EXL, BrailleCode.CONTRACTED, BrailleCode.UXL, BrailleCode.UCL)
                ||
                ContainsCodes(files, BrailleCode.UNCONTRACTED, BrailleCode.CONTRACTED, BrailleCode.UXL, BrailleCode.UCL))
            {
                result.Add(BrailleSupport.Both4);
            }
            if (ContainsCodes(files, BrailleCode.UXN, BrailleCode.UXT, BrailleCode.UCN, BrailleCode.UCT))
            {
                result.Add(BrailleSupport.UEB4);
            }
            if (ContainsCodes(files, BrailleCode.UXL, BrailleCode.UCL))
            {
                result.Add(BrailleSupport.UEB2);
            }
            if (ContainsCodes(files, BrailleCode.UXL, BrailleCode.UCL))
            {
                result.Add(BrailleSupport.UEB2);
            }
            if (ContainsCodes(files, BrailleCode.UNCONTRACTED, BrailleCode.CONTRACTED)
                || ContainsCodes(files, BrailleCode.EXL, BrailleCode.CONTRACTED)
                || ContainsCodes(files, BrailleCode.UNCONTRACTED, BrailleCode.ECL)
                || ContainsCodes(files, BrailleCode.EXL, BrailleCode.ECL))
            {
                result.Add(BrailleSupport.EBAE2);
            }
            if (ContainsCodes(files, BrailleCode.NEMETH)
                || ContainsCodes(files, BrailleCode.ECN))
            {
                result.Add(BrailleSupport.EBAE1);
            }
            if (!result.Any())
            {
                result.Add(BrailleSupport.UNKNOWN);
            }
            return result;
        }

        public static bool ContainsCodes(IList<BrailleFile> files, params BrailleCode[] codes)
        {
            return codes.All(code => files.Any(x => x.Code == code));
        }
    }
}