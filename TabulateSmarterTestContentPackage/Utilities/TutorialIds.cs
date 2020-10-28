using System;
using System.Collections.Generic;
using System.Text;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Utilities
{
    static class TutorialIds
    {
        static Dictionary<string, int> s_ItemTypeToTutorialId = new Dictionary<string, int>()
        {
            { "ela-ebsr", 67861 },
            { "ela-gi", 67858 },
            { "ela-htq", 72860 },
            { "ela-mc", 67708 },
            { "ela-mi", 67859 },
            { "ela-ms", 67775 },
            { "ela-sa", 67865 },
            { "ela-ti", 67860 },
            { "ela-wer", 67864 },
            { "math-eq", 67863 },
            { "math-gi", 67858 },
            { "math-mc", 67708 },
            { "math-mi", 67859 },
            { "math-ms", 67775 },
            { "math-sa", 67866 },
            { "math-ti", 67860 }
        };

        /// <summary>
        /// Returns the expected tutorial ID for an item type or zero if the type is not listed.
        /// </summary>
        /// <param name="itemType">Assessment item type.</param>
        /// <returns>A tutorial ID</returns>
        public static int IdFromItemType(string subject, string interactionType)
        {
            int id;
            if (s_ItemTypeToTutorialId.TryGetValue(string.Concat(subject, "-", interactionType).ToLower(), out id))
            {
                return id;
            }
            return 0;
        }

        public static void ValidateTutorialId(ItemIdentifier ii, string subject, string tutorialId)
        {

            int expectedId = IdFromItemType(subject, ii.ItemType);
            if (!int.TryParse(tutorialId, out int tid)
                || tid != expectedId)
            {
                ReportingUtility.ReportError(ii, ErrorId.T0196, $"subject='{subject}' itemType='{ii.ItemType}', tutorialId='{tutorialId}', expectedTutorialId='{expectedId}'");
            }
        }
    }
}
