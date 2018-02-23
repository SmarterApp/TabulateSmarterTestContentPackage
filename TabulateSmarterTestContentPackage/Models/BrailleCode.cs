using System;
namespace TabulateSmarterTestContentPackage.Models
{
    [Flags]
    public enum BrailleFormCode : int
    {
        NONE = 0,
        //  Standard, Contracted, Math
        EXN = 0x0001, // EBAE, Uncontracted, Nemeth
        // EXT = 0x0002, // EBAE, Uncontracted, UEB (not a valid combination)
        EXL = 0x0004, // EBAE, Uncontracted, None
        ECN = 0x0008, // EBAE, Contracted, Nemeth
        // ECT = 0x0010, // EBAE, Contracted, UEB (not a valid combination)
        ECL = 0x0020, // EBAE, Contracted, None
        UXN = 0x0040, // UEB, Uncontracted, Nemeth
        UXT = 0x0080, // UEB, Uncontracted, UEB
        UXL = 0x0100, // UEB, Uncontracted, None
        UCN = 0x0200, // UEB, Contracted, Nemeth
        UCT = 0x0400, // UEB, Contracted, UEB
        UCL = 0x0800 // UEB, Contracted, None

        // Legacy types are normalized using BrailleUtility.NormalizeBrailleFileType;
        // UNCONTRACTED, // Legacy equivalent to EXL
        // CONTRACTED, // Legacy equivalent to ECL
        // NEMETH // Legacy equivalent to ECN
    }
}