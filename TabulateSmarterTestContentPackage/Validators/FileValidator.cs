using System;
using System.Collections.Generic;
using System.Text;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;

namespace TabulateSmarterTestContentPackage.Validators
{

    /// <summary>
    /// Validates all files in the item.
    /// </summary>
    static class FileValidator
    {
        // TODO: Potential enhancement would be to make sure every file is referenced in the item
        // Presently only checks for empty files.
        public static void Validate(ItemContext it)
        {
            foreach (FileFile file in it.FfItem.Files)
            {
                if (file.Length == 0)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0199, $"filename='{file.Name}'");
                }
            }
        }

    }
}
