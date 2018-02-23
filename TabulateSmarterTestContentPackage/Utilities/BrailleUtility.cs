using System;
using System.Collections.Generic;
using System.Linq;
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
            if (code == BrailleFormCode.NONE)
            {
                return BrailleSupport.NONE;
            }
            if ((code & cBoth6) == cBoth6)
            {
                return BrailleSupport.Both6;
            }
            if ((code & cBoth4) == cBoth4)
            {
                return BrailleSupport.Both4;
            }
            if ((code & cUEB4) == cUEB4)
            {
                return BrailleSupport.UEB4;
            }
            if ((code & cUEB2) == cUEB2)
            {
                return BrailleSupport.UEB2;
            }
            if ((code & cEBAE2) == cEBAE2)
            {
                return BrailleSupport.EBAE2;
            }
            if ((code & cEBAE1) == cEBAE1)
            {
                return BrailleSupport.EBAE1;
            }
            return BrailleSupport.UNEXPECTED;
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
    }
}