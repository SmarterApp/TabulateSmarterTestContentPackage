namespace TabulateSmarterTestContentPackage.Models
{
    public enum BrailleSupport
    {
        // No braille support found
        NONE,
        // Old item, supporting EBAE with math content
        // nemeth (Equivalent to ECN)
        EBAE1,
        // Old item, supporting EBAE with no math content.
        // uncontracted, contracted (Equivalent to EXL, ECL)
        EBAE2,
        // New item, exclusively UEB without math content.
        // UXL, UCL
        UEB2,
        // New item, exclusively UEB with math content.
        // UXN, UXT, UCN, UCT
        UEB4,
        // Item supporting EBAE and UEB without math content.
        // EXL, ECL, UXL, UCL
        EBAE2_UEB2,
        // Item supporting EBAE and UEB with math content.
        // EXN, ECN, UXN, UXT, UCN, UCT
        EBAE2_UEB4,
        // Unexpected combination of braille files
        UNEXPECTED,
        // Metadata marks item as not braillable
        NOTBRAILLABLE
    }
}