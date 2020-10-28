using System;
using System.Xml;
using System.IO;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Extractors;
using TabulateSmarterTestContentPackage.Utilities;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace TabulateSmarterTestContentPackage.Validators
{
    static class RubricValidator
    {
        // SHA1 Hash Digests of known empty rubric templates
        static HashSet<HashValue> s_EmptyRubricTemplates = new HashSet<HashValue>()
        {
            // SHA1 Hashes of known Empty Templates in item bank as of 2020-10-22
            new HashValue("bbf7aa1330e0e7dc31cc91eb48c6e236fe951539"),
            new HashValue("3fc4e355a4667294ebb77fb3876e184d0c45260a"),
            new HashValue("a0b4ad36bdf76f1efab42ba2545566ebacc2ba43"),
            new HashValue("d77b35d025f9de7b0a23750c4454b1f17edfc515"),
            new HashValue("a0fbcc21926a8a40fad6f9221168ed944bbb7619"),
            new HashValue("ba7a6db8fafb67a9f466859c523f1ab586c8aa94"),
            new HashValue("d7d2420a018d056a84b16c47d965dce5c08c3ed1"),
            new HashValue("8669e636abf71d3877ebe97a22868776ab049fdf"),
            new HashValue("cac6011ff89b6744698c4d4874d58b5523e9b55b"),
            new HashValue("caf273fcfbf3de3fa1e4cbcc449427c565ea6be0"),
            new HashValue("df9e52e51e64b3cc6ec882ce6b2e825d8e9734e5"),
            new HashValue("490dd587ff7adb7fc015a3ff375ceebb8b13b224"),
            new HashValue("f77baaf0d40cf265d1ac6f74d1ab595ecbd6f0d6"),
            new HashValue("d1a6c7a6007990de8dd594fea529da3873cedb52"),
            new HashValue("35ac40d9c270530c1c29ae097537c70eb8225ca5"),
            new HashValue("a8bdb83a4c665467368c5367db285c2f3d9f8b17"),
            new HashValue("08c387281eece146ef2a9917672ac61a8070e1fc"),
            new HashValue("9e00612131d190d3baeefec093c515a2c34075af"),
            new HashValue("bf4d09493abc9dbbc98f604dea1af6972276aade"),
            new HashValue("a8f803f2128ac2b6ef642f7aa6a912b28c7700b9")
        };

        public static void Validate(ItemContext it, XmlDocument xml, bool exportRubrics, string exportRubricPath)
        {
            using (var rubricStream = new MemoryStream())
            {
                if (!RubricExtractor.ExtractRubric(xml, rubricStream))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0067);
                }
                else
                {
                    rubricStream.Position = 0;
                    var hash = HashValue.ComputeHash<SHA1CryptoServiceProvider>(rubricStream);

                    // See if the rubric is an empty template
                    if (s_EmptyRubricTemplates.Contains(hash))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0198, $"rubricHash={hash}");
                    }

                    // Export the rubric if specified
                    if (exportRubrics)
                    {
                        try
                        {
                            string rubricFn = Path.Combine(exportRubricPath, $"rubric-{it.BankKey}-{it.ItemId}.html");
                            using (var outStream = new FileStream(rubricFn, FileMode.Create, FileAccess.Write, FileShare.Read))
                            {
                                rubricStream.Position = 0;
                                rubricStream.CopyTo(outStream);
                            }
                        }
                        catch (Exception err)
                        {
                            ReportingUtility.ReportError(it, err);
                        }
                    }

                }
            }

        }
    }
}
