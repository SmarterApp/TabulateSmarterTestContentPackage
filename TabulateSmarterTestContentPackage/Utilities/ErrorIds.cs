using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Utilities
{
    static class ErrorConstants
    {
        public static readonly ItemIdentifier ManifestItemId = new ItemIdentifier("item", 0, 0);
    }

    public enum ErrorId : int
    {
        None = 0
    }
}