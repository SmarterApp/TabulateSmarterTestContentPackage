namespace TabulateSmarterTestContentPackage.Models
{
    public enum BrailleSupport
    {
        // Item supporting EBAE and UEB with math content.
        // EXN, ECN, UXN, UXT, UCN, UCT
        Both6,
        // Item supporting EBAE and UEB without math content.
        // EXL, ECL, UXL, UCL
        Both4,
        // New item, exclusively UEB with math content.
        // UXN, UXT, UCN, UCT
        UEB4,
        // New item, exclusively UEB without math content.
        // UXL, UCL
        UEB2,
        // Old item, supporting EBAE with no math content.
        // uncontracted, contracted (Equivalent to EXL, ECL)
        EBAE2,
        // Old item, supporting EBAE with math content
        // nemeth (Equivalent to ECN)
        EBAE1,
        UNKNOWN,
        NOTBRAILLABLE
    }
}