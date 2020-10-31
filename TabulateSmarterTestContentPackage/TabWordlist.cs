using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
using TabulateSmarterTestContentPackage.Validators;

namespace TabulateSmarterTestContentPackage
{
    partial class Tabulator
    {
        [Flags]
        // The bitfields are based on 1, 2, 4, 8. The pattern needs to be followed
        enum GlossaryTypes : int
        {
            None         = 0,
            English      = 0x0001,
            Arabic       = 0x0002,
            Burmese      = 0x0004,
            Cantonese    = 0x0008,
            Filipino     = 0x0010,
            Hmong        = 0x0020,
            Korean       = 0x0040,
            Mandarin     = 0x0080,
            Punjabi      = 0x0100,
            Russian      = 0x0200,
            Somali       = 0x0400,
            Spanish      = 0x0800,
            Ukranian     = 0x1000,
            Vietnamese   = 0x2000,
            Illustration = 0x4000
        }

        // These are the keywords used in the XML files for the various languages
        // They must be listed in the same order as the bitfields in the enum ablve
        static string[] sKnownGlossaries =
        {
            "glossary", // English
            "arabicGlossary",
            "burmeseGlossary",
            "cantoneseGlossary",
            "tagalGlossary", // Filipino
            "hmongGlossary",
            "koreanGlossary",
            "mandarinGlossary",
            "punjabiGlossary",
            "russianGlossary",
            "somaliGlossary",
            "esnGlossary",  // Spanish
            "ukrainianGlossary",
            "vietnameseGlossary",
            "illustration"
        };

        // These letters must be in the same order as the enum bitfield and sKnownGlossaries. There are two 'S' characters: the first is for Somali, the second is for Spanish
        static char[] sKnownGlossaryLetters = { 'E', 'A', 'B', 'C', 'F', 'H', 'K', 'M', 'P', 'R', 'S', 'S', 'U', 'V', 'I' };

        static Dictionary<string, GlossaryTypes> sKnownGlossariesIndex;

        static GlossaryTypes sExpectedTranslatedGlossaries =
            GlossaryTypes.Arabic
            | GlossaryTypes.Burmese
            | GlossaryTypes.Cantonese
            | GlossaryTypes.Filipino
            | GlossaryTypes.Hmong
            | GlossaryTypes.Korean
            | GlossaryTypes.Mandarin
            | GlossaryTypes.Punjabi
            | GlossaryTypes.Russian
            | GlossaryTypes.Somali
            | GlossaryTypes.Spanish
            | GlossaryTypes.Ukranian
            | GlossaryTypes.Vietnamese
            | GlossaryTypes.Illustration;

        static GlossaryTypes sAllTranslatedGlossaries =
            GlossaryTypes.Arabic
            | GlossaryTypes.Burmese
            | GlossaryTypes.Cantonese
            | GlossaryTypes.Filipino
            | GlossaryTypes.Hmong
            | GlossaryTypes.Korean
            | GlossaryTypes.Mandarin
            | GlossaryTypes.Punjabi
            | GlossaryTypes.Russian
            | GlossaryTypes.Somali
            | GlossaryTypes.Spanish
            | GlossaryTypes.Ukranian
            | GlossaryTypes.Vietnamese
            | GlossaryTypes.Illustration;

        static void StaticInitWordlist()
        {
            sKnownGlossariesIndex = new Dictionary<string, GlossaryTypes>(sKnownGlossaries.Length);
            for (var i = 0; i < sKnownGlossaries.Length; ++i)
            {
                sKnownGlossariesIndex.Add(sKnownGlossaries[i], (GlossaryTypes)(1 << i));
            }
        }

        private void TabulateWordList(ItemIdentifier ii)
        {
            // Get the item context
            ItemContext it;
            if (!ItemContext.TryCreate(mPackage, ii, out it))
            {
                ReportingUtility.ReportError(ii, ErrorId.T0164);
                return;
            }

            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportingUtility.ReportError(it, ErrorId.T0107, LoadXmlErrorDetail);
                return;
            }

            // Load metadata
            var xmlMetadata = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, "metadata.xml", xmlMetadata))
            {
                ReportingUtility.ReportError(it, ErrorId.T0112, LoadXmlErrorDetail);
            }

            // Validate Version
            VersionValidator.Validate(it, xml, xmlMetadata);
            ii.Version = it.Version;

            // Count this wordlist
            ++mWordlistCount;

            // See if the wordlist has been referenced
            int refCount = mWordlistRefCounts.Count(it.ToString());
            if (refCount == 0 && !(mPackage is SingleItemPackage))
            {
                ReportingUtility.ReportError(it, ErrorId.T0089);
            }

            // Zero the counts
            int termcount = 0;
            int maxgloss = 0;
            int mingloss = int.MaxValue;
            int totalgloss = 0;

            // Enumerate all terms and count glossary entries
            foreach (XmlNode kwNode in xml.SelectNodes("itemrelease/item/keywordList/keyword"))
            {
                ++mGlossaryTermCount;
                ++termcount;

                // Count this instance of the term
                string term = kwNode.XpEval("@text");
                mTermCounts.Increment(term);

                int glosscount = 0;
                foreach (XmlNode htmlNode in kwNode.SelectNodes("html"))
                {
                    ++glosscount;
                }

                if (maxgloss < glosscount) maxgloss = glosscount;
                if (mingloss > glosscount) mingloss = glosscount;
                totalgloss += glosscount;
            }

            if (mingloss == int.MaxValue) mingloss = 0;

            //Folder,WIT_ID,RefCount,TermCount,MaxGloss,MinGloss,AvgGloss
            mWordlistReport.WriteLine(string.Join(",", CsvEncode(it.FolderDescription), it.BankKey.ToString(), it.ItemId.ToString(), refCount.ToString(), termcount.ToString(), maxgloss.ToString(), mingloss.ToString(), (termcount > 0) ? (((double)totalgloss) / ((double)termcount)).ToString("f2") : "0"));

            // Tabulation is complete, check for other errors.

            FileValidator.Validate(it, xml);

        }

        static readonly Regex sRxAudioAttachment = new Regex(@"<a[^>]*href=""([^""]*)""[^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        static readonly Regex sRxImageAttachment = new Regex(@"<img[^>]*src=""([^""]*)""[^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // Attachments don't have to follow the naming convention but they usually do. When they match then we compare values.
        // Sample: item_116605_v1_116605_01btagalog_glossary_ogg_m4a.f
        static readonly Regex sRxAttachmentNamingConvention = new Regex(@"^item_(\d+)_v\d+_(\d+)_(\d+)([a-zA-Z]+)_glossary(?:_ogg)?(?:_m4a)?(?:_ogg)?\.(?:ogg|m4a)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // Validate the wordlist vocabulary for a particular item.
        // Returns the aggregate translation Bitflags
        private GlossaryTypes ValidateWordlistVocabulary(string bankKey, string wordlistId, ItemContext itemIt, List<int> termIndices, List<string> terms)
        {
            // Make sure the wordlist exists
            ItemIdentifier ii = new ItemIdentifier(cItemTypeWordlist, bankKey, wordlistId);
            FileFolder ff;
            if (!mPackage.TryGetItem(ii, out ff))
            {
                if (!(mPackage is SingleItemPackage))
                {
                    ReportingUtility.ReportError(itemIt, ErrorId.T0040, "wordlistId='{0}'", wordlistId);
                }
                return 0;
            }

            // Read the wordlist XML
            var xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(ff, ii.FullId + ".xml", xml))
            {
                ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0107, LoadXmlErrorDetail);
                return 0;
            }

            // Make sure this is a wordlist
            if (!string.Equals(xml.XpEvalE("itemrelease/item/@type"), cItemTypeWordlist))
            {
                ReportingUtility.ReportError(itemIt, ErrorId.T0165, $"referencedId='{ii.ItemId}'");
                return 0;
            }

            // Sanity check
            if (!string.Equals(xml.XpEvalE("itemrelease/item/@id"), ii.ItemId.ToString()))
            {
                ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0166, $"wordListId='{xml.XpEval("itemrelease/item/@id")}' expected='{ii.ItemId}'");
                return 0;
            }

            // Add this to the wordlist queue (if not there already) and manage progress count
            if (mWordlistQueue.Add(ii))
            {
                if (mItemQueue.Contains(ii)) ++mTransferCount;
            };

            // Create a dictionary of attachment files
            Dictionary<string, long> attachmentFiles = new Dictionary<string, long>();
            foreach (FileFile fi in ff.Files)
            {
                // If Audio or image file
                var extension = fi.Extension.ToLowerInvariant();
                if (!string.Equals(extension, ".xml", StringComparison.Ordinal))
                {
                    attachmentFiles.Add(fi.Name, fi.Length);
                }
            }

            // Create a hashset of all wordlist terms that are referenced by the item
            HashSet<int> referencedIndices = new HashSet<int>(termIndices);

            // Load up the list of wordlist terms
            List<string> wordlistTerms = new List<string>();
            foreach (XmlNode kwNode in xml.SelectNodes("itemrelease/item/keywordList/keyword"))
            {
                // Get the term and its index
                string term = kwNode.XpEval("@text");
                int index = int.Parse(kwNode.XpEval("@index"));

                // Make sure the index is unique and add to the term list
                while (wordlistTerms.Count < index + 1) wordlistTerms.Add(string.Empty);
                if (!string.IsNullOrEmpty(wordlistTerms[index]))
                {
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0088, "index='{0}'", index);
                }
                else
                {
                    wordlistTerms[index] = term;
                }
            }

            // Keep track of term information for error checks   
            Dictionary<string, TermAttachmentReference> attachmentToReference = new Dictionary<string, TermAttachmentReference>();

            // Enumerate all the terms in the wordlist (second pass)
            int ordinal = 0;
            GlossaryTypes aggregateGlossariesFound = 0;
            foreach (XmlNode kwNode in xml.SelectNodes("itemrelease/item/keywordList/keyword"))
            {
                ++ordinal;

                // Get the term and its index
                string term = kwNode.XpEval("@text");
                int index = int.Parse(kwNode.XpEval("@index"));

                // See if this term is referenced by the item.
                bool termReferenced = referencedIndices.Contains(index);
                if (!termReferenced && Program.gValidationOptions.IsEnabled("uwt"))
                {
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0167, "term='{0}' termIndex='{1}'", term, index);
                }

                // Find the attachment references and enumberate the translations
                GlossaryTypes glossariesFound = 0;
                foreach (XmlNode htmlNode in kwNode.SelectNodes("html"))
                {
                    var listType = htmlNode.XpEval("@listType");
                    mTranslationCounts.Increment(listType);

                    if (sKnownGlossariesIndex.TryGetValue(listType, out GlossaryTypes gt))
                    {
                        glossariesFound |= gt;
                    }

                    // Get the embedded HTML
                    string html = htmlNode.InnerText;

                    string audioType = string.Empty;
                    long audioSize = 0;
                    string imageType = string.Empty;
                    long imageSize = 0;

                    // Look for an audio glossary entry
                    Match match = sRxAudioAttachment.Match(html);
                    if (match.Success)
                    {
                        // Use RegEx to find the audio glossary entry in the contents.
                        string filename = match.Groups[1].Value;
                        ProcessGlossaryAttachment(filename, itemIt, ii, index, listType, termReferenced, wordlistTerms, attachmentFiles, attachmentToReference, ref audioType, ref audioSize);

                        // Check for dual types
                        if (string.Equals(Path.GetExtension(filename), ".ogg", StringComparison.OrdinalIgnoreCase))
                        {
                            filename = Path.GetFileNameWithoutExtension(filename) + ".m4a";
                            ProcessGlossaryAttachment(filename, itemIt, ii, index, listType, termReferenced, wordlistTerms, attachmentFiles, attachmentToReference, ref audioType, ref audioSize);
                        }
                        else if (string.Equals(Path.GetExtension(filename), ".m4a", StringComparison.OrdinalIgnoreCase))
                        {
                            filename = Path.GetFileNameWithoutExtension(filename) + ".ogg";
                            ProcessGlossaryAttachment(filename, itemIt, ii, index, listType, termReferenced, wordlistTerms, attachmentFiles, attachmentToReference, ref audioType, ref audioSize);
                        }

                        // If filename matches the naming convention, ensure that values are correct
                        Match match2 = sRxAttachmentNamingConvention.Match(filename);
                        if (match2.Success)
                        {
                            // Sample attachment filename that follows the convention:
                            // item_116605_v1_116605_01btagalog_glossary_ogg_m4a.m4a

                            // Check both instances of the wordlist ID
                            if (!wordlistId.Equals(match2.Groups[1].Value, StringComparison.Ordinal)
                                && !wordlistId.Equals(match2.Groups[2].Value, StringComparison.Ordinal))
                            {
                                ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0084, "filename='{0}' filenameItemId='{1}' expectedItemId='{2}'", filename, match2.Groups[1].Value, wordlistId);
                            }

                            // Check that the wordlist term index matches
                            /* While most filename indices match. It's quite common for them not to match and still be the correct audio
                               Disabling this check because it's mostly false alarms.

                            int filenameIndex;
                            if (!int.TryParse(match2.Groups[3].Value, out filenameIndex)) filenameIndex = -1;
                            if (filenameIndex != index && filenameIndex != ordinal
                                && (filenameIndex >= wordlistTerms.Count || !string.Equals(wordlistTerms[filenameIndex], term, StringComparison.OrdinalIgnoreCase)))
                            {
                                ReportingUtility.ReportWitError(ItemIt, it, ErrorId.T0168, "filename='{0}' filenameIndex='{1}' expectedIndex='{2}'", filename, filenameIndex, index);
                            }
                            */

                            // Translate from language in the naming convention to listType value
                            string filenameListType = match2.Groups[4].Value.ToLower();
                            switch (filenameListType)
                            {
                                // Special cases
                                case "spanish":
                                    filenameListType = "esnGlossary";
                                    break;

                                case "tagalog":
                                case "atagalog":
                                case "btagalog":
                                case "ilocano":
                                case "atagal":
                                    filenameListType = "tagalGlossary";
                                    break;

                                case "apunjabi":
                                case "bpunjabi":
                                case "punjabiwest":
                                case "punjabieast":
                                    filenameListType = "punjabiGlossary";
                                    break;

                                // Conventional case
                                default:
                                    filenameListType = string.Concat(filenameListType.ToLower(), "Glossary");
                                    break;
                            }
                            if (!filenameListType.Equals(listType))
                            {
                                ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0087, "filename='{0}' filenameListType='{1}' expectedListType='{2}'", filename, filenameListType, listType);
                            }
                        }

                    }

                    // Look for an image glossary entry
                    match = sRxImageAttachment.Match(html);
                    if (match.Success)
                    {
                        // Use RegEx to find the illustration glossary entry in the contents.
                        string filename = match.Groups[1].Value;

                        ProcessGlossaryAttachment(filename, itemIt, ii, index, listType, termReferenced, wordlistTerms, attachmentFiles, attachmentToReference, ref imageType, ref imageSize);

                        if (!wordlistId.Equals(match.Groups[1].Value, StringComparison.Ordinal)
                         && !wordlistId.Equals(match.Groups[2].Value, StringComparison.Ordinal))
                        {
                            ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0084, "filename='{0}' filenameItemId='{1}' expectedItemId='{2}'", filename, match.Groups[1].Value, wordlistId);
                        }
                    }
                    else if (listType.Equals("illustration", StringComparison.Ordinal))
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0169, "term='{0}' index='{1}'", term, index);
                    }

                    // Report error if translated glossary lacks audio. This check should be for all glossary types, except Illustrations
                    if (gt != GlossaryTypes.Illustration) { 
                        if ((gt & sAllTranslatedGlossaries) != 0 && string.IsNullOrEmpty(audioType))
                        {
                            ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0075, "term='{0}' index='{1}'", term, index);
                        }
                    }

                    // Report error if Burmese translation includes Zawgyi characters
                    if (gt == GlossaryTypes.Burmese && HasZawgyiCharacters(html))
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0012, $"term='{term}' index='{index}' translation='{html}'");
                    }

                    string folderDescription = string.Concat(mPackage.Name, "/", ii.FolderName);

                    // Folder,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize
                    if (Program.gValidationOptions.IsEnabled("gtr"))
                        mGlossaryReport.WriteLine(string.Join(",", CsvEncode(folderDescription), ii.BankKey.ToString(), ii.ItemId.ToString(), itemIt.ItemId.ToString(), index.ToString(), CsvEncodeExcel(term), CsvEncode(listType), html.Length.ToString(), audioType, audioSize.ToString(), imageType, imageSize.ToString(), CsvEncode(html)));
                    else
                        mGlossaryReport.WriteLine(string.Join(",", CsvEncode(folderDescription), ii.BankKey.ToString(), ii.ItemId.ToString(), itemIt.ItemId.ToString(), index.ToString(), CsvEncodeExcel(term), CsvEncode(listType), html.Length.ToString(), audioType, audioSize.ToString(), imageType, imageSize.ToString()));
                }

                // Report any expected translations that weren't found
                if (termReferenced
                    && (glossariesFound & sExpectedTranslatedGlossaries) != 0 // at least one translated glossary
                    && (glossariesFound & sExpectedTranslatedGlossaries) != sExpectedTranslatedGlossaries) // not all translated glossaries
                {
                    // Make a list of translations that weren't found
                    string missedTranslations = (sExpectedTranslatedGlossaries & ~glossariesFound).ToString();
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0092, "term='{0}' missing='{1}'", term, missedTranslations);
                }

                aggregateGlossariesFound |= glossariesFound;
            }

            Porter.Stemmer stemmer = new Porter.Stemmer();

            // Make sure terms match references
            for (int i = 0; i < termIndices.Count; ++i)
            {
                int index = termIndices[i];
                if (index >= wordlistTerms.Count || string.IsNullOrEmpty(wordlistTerms[index]))
                {
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0041, "text='{0}' termIndex='{1}'", terms[i], index);
                }
                else
                {
                    if (!stemmer.TermsMatch(terms[i], wordlistTerms[index]))
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0043, "text='{0}' term='{1}' termIndex='{2}'", terms[i], wordlistTerms[index], index);
                    }
                }
            }

            // Report unreferenced attachments
            if (Program.gValidationOptions.IsEnabled("umf"))
            {
                foreach (var pair in attachmentFiles)
                {
                    if (!attachmentToReference.ContainsKey(pair.Key))
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0170, "filename='{0}'", pair.Key);
                    }
                }
            }

            // Validate all referenced attachments
            foreach(var pair in attachmentToReference)
            {
                ValidateMediaFile(itemIt, ii, ff, pair.Key, wordlistTerms[pair.Value.TermIndex]);
            }

            return aggregateGlossariesFound;
        }

        // This is kind of ugly with so many parameters but it's the cleanest way to handle this task that's repeated multiple times
        void ProcessGlossaryAttachment(string filename,
            ItemContext itemIt, ItemIdentifier ii, int termIndex, string listType, bool termReferenced,
            List<string> wordlistTerms, Dictionary<string, long> attachmentFiles, Dictionary<string, TermAttachmentReference> attachmentToTerm,
            ref string type, ref long size)
        {
            long fileSize = 0;
            if (!attachmentFiles.TryGetValue(filename, out fileSize))
            {
                // Look for case-insensitive match (file will not be found on Linux systems)
                // (This is a linear search but it occurs rarely so not a significant issue)
                string caseMismatchFilename = null;
                foreach (var pair in attachmentFiles)
                {
                    if (string.Equals(filename, pair.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        caseMismatchFilename = pair.Key;
                        break;
                    }
                }

                if (termReferenced)
                {
                    if (caseMismatchFilename == null)
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0085, "filename='{0}' term='{1}' termIndex='{2}'", filename, wordlistTerms[termIndex], termIndex);
                    }
                    else
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0086, "referenceFilename='{0}' actualFilename='{1}' termIndex='{2}'", filename, caseMismatchFilename, termIndex);
                    }
                }

                else if (Program.gValidationOptions.IsEnabled("mwa")) // Term not referenced
                {
                    if (caseMismatchFilename == null)
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0171, "filename='{0}' term='{1}' termIndex='{2}'", filename, wordlistTerms[termIndex], termIndex);
                    }
                    else
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0172, "referenceFilename='{0}' actualFilename='{1}' termIndex='{2}'", filename, caseMismatchFilename, termIndex);
                    }
                }
            }

            // See if this attachment has previously been referenced
            TermAttachmentReference previousTerm = null;
            if (attachmentToTerm.TryGetValue(filename, out previousTerm))
            {
                // Error if different terms (case insensitive)
                if (!string.Equals(wordlistTerms[termIndex], wordlistTerms[previousTerm.TermIndex], StringComparison.InvariantCultureIgnoreCase))
                {
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0077, "filename='{0}' termA='{1}' termB='{2}' termIndexA='{3}' termIndexB='{4}",
                        filename, wordlistTerms[previousTerm.TermIndex], wordlistTerms[termIndex], previousTerm.TermIndex, termIndex);
                }

                // Error if different listTypes (language or image)
                if (!string.Equals(listType, previousTerm.ListType, StringComparison.Ordinal))
                {
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorId.T0068, "filename='{0}' term='{1}' typeA='{2}' typeB='{3}' termIndexA='{4}' termIndexB='{5}",
                        filename, wordlistTerms[termIndex], previousTerm.ListType, listType, previousTerm.TermIndex, termIndex);
                }
            }
            else
            {
                attachmentToTerm.Add(filename, new TermAttachmentReference(termIndex, listType, filename));
            }

            size += fileSize;
            string extension = Path.GetExtension(filename);
            if (extension.Length > 1) extension = extension.Substring(1); // Remove dot from extension
            if (string.IsNullOrEmpty(type))
            {
                type = extension.ToLower();
            }
            else
            {
                type = string.Concat(type, ";", extension.ToLower());
            }
        }

        static string GlossStringFlags(GlossaryTypes glossaryTypes)
        {
            char[] ca = new char[sKnownGlossaryLetters.Length];
            for (int i=0; i < sKnownGlossaryLetters.Length; ++i)
            {
                ca[i] = ((glossaryTypes & (GlossaryTypes)(1 << i)) != 0) ? sKnownGlossaryLetters[i] : '-';
            }
            return new string(ca);
            /*
            int result = 0;
            for (int i = 0; i < 15; ++i)
            {
                if ((glossaryTypes & (GlossaryTypes)(1 << i)) != 0) ++result;
            }
            return (result == 0) ? string.Empty : result.ToString();
            */
        }

        const string c_ogg = ".ogg";
        const string c_m4a = ".m4a";
        const Int32 c_oggHeader = 0x5367674f;   // OggS in hex
        const Int32 c_m4aHeader = 0x70797466;   // ftyp in hex

        static void ValidateMediaFile(ItemContext it, ItemIdentifier ii, FileFolder ff, string filename, string term)
        {
            // Presently we only validate .ogg and .m4a file formats.
            // We have seen .m4a files named .ogg which caused problems with certain browsers.
            string extension = Path.GetExtension(filename).ToLowerInvariant();

            if (extension.Equals(c_ogg, StringComparison.Ordinal) ||
                extension.Equals(c_m4a, StringComparison.Ordinal))
            {
                Int32 header0;
                Int32 header4;
                FileFile fxz;
                if (ff.TryGetFile(filename, out fxz)) // Tolerate missing file; it's reported in ProcessGlossaryAttachment.
                {
                    using (var file = new BinaryReader(fxz.Open()))
                    {
                        header0 = file.ReadInt32();
                        header4 = file.ReadInt32();
                    }

                    string foundFormat = (header0 == c_oggHeader) ? c_ogg
                        : ((header4 == c_m4aHeader) ? c_m4a : "unknown");

                    if (!string.Equals(extension, foundFormat, StringComparison.Ordinal))
                    {
                        ReportingUtility.ReportWitError(it, ii, ErrorId.T0005, $"term = '{term}' filename='{filename}' expectedFormat='{extension}' actualFormat='{foundFormat}'");
                    }
                }
            }
        }

        const char c_minZawgyi = '\x1060';
        const char c_maxZawgyi = '\x1097';

        /// <summary>
        /// Characters in the range of 0x1060 to 0x1097 inclusive are used in Zawgyi-encoded Burmese
        /// but not in Unicode-encoded Burmese - unless certain dialects are included.
        /// Therefore, the presence of these characters is an indication that Burmese text is
        /// using the Zawgyi character set instead of the Unicode character set it should be using.
        /// </summary>
        /// <param name="s">String to check for Zawgyi characters</param>
        /// <returns>True if any Zawgyi characters were detected. Otherwise false.</returns>
        static bool HasZawgyiCharacters(string s)
        {
            foreach (char c in s)
            {
                if (c >= c_minZawgyi && c <= c_maxZawgyi) return true;
            }
            return false;
        }

        class TermAttachmentReference
        {
            public TermAttachmentReference(int termIndex, string listType, string filename)
            {
                TermIndex = termIndex;
                ListType = listType;
                Filename = filename;
            }

            public int TermIndex { get; private set; }
            public string ListType { get; private set; }
            public string Filename { get; private set; }
        }
    }
}
