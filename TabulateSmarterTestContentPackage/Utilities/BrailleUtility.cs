using System.Collections.Generic;
using System.Linq;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Utilities
{
    public static class BrailleUtility
    {
        public static IList<BrailleSupport> GetSupportByCode(IList<BrailleFile> files)
        {
            var result = new List<BrailleSupport>();
            if (ContainsCodes(files, BrailleCode.EXN, BrailleCode.ECN, BrailleCode.UXN, BrailleCode.UXT, BrailleCode.UCN,
                BrailleCode.UCT))
            {
                result.Add(BrailleSupport.Both6);
            }
            if (ContainsCodes(files, BrailleCode.EXL, BrailleCode.ECL, BrailleCode.UXL, BrailleCode.UCL))
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
            if (ContainsCodes(files, BrailleCode.UNCONTRACTED, BrailleCode.CONTRACTED))
            {
                result.Add(BrailleSupport.EBAE2);
            }
            if (ContainsCodes(files, BrailleCode.NEMETH))
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