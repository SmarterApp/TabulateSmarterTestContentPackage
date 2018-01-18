namespace TabulateSmarterTestContentPackage.Models
{
    public enum BrailleCode
    {
        //  Standard, Contracted, Math
        EXN, // EBAE, Uncontracted, Nemeth
        EXT, // EBAE, Uncontracted, UEB
        EXL, // EBAE, Uncontracted, None
        ECN, // EBAE, Contracted, Nemeth
        ECT, // EBAE, Contracted, UEB
        ECL, // EBAE, Contracted, None
        UXN, // UEB, Uncontracted, Nemeth
        UXT, // UEB, Uncontracted, UEB
        UXL, // UEB, Uncontracted, None
        UCN, // UEB, Contracted, Nemeth
        UCT, // UEB, Contracted, UEB
        UCL // UEB, Contracted, None

        // Legacy types are normalized using BrailleUtility.NormalizeBrailleFileType;
        // UNCONTRACTED, // Legacy equivalent to EXL
        // CONTRACTED, // Legacy equivalent to ECL
        // NEMETH // Legacy equivalent to ECN
    }
}