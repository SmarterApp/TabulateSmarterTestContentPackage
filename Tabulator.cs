using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace TabulateSmarterTestContentPackage
{
    class Tabulator
    {
        static readonly UTF8Encoding sUtf8NoBomEncoding = new UTF8Encoding(false, true);
        static NameTable sXmlNt;
        static XmlNamespaceManager sXmlNs;

        static Tabulator()
        {
            sXmlNt = new NameTable();
            sXmlNs = new XmlNamespaceManager(sXmlNt);
            sXmlNs.AddNamespace("sa", "http://www.smarterapp.org/ns/1/assessment_item_metadata");
        }

        // Filenames
        const string cSummaryReportFn = "SummaryReport.txt";
        const string cTextGlossaryReportFn = "TextGlossaryReport.csv";
        const string cAudioGlossaryReportFn = "AudioGlossaryReport.csv";
        const string cItemReportFn = "ItemReport.csv";
        const string cErrorReportFn = "ErrorReport.txt";

        string mRootPath;
        int mErrorCount = 0;
        int mItemCount = 0;
        int mWordlistCount = 0;
        int mGlossaryTermCount = 0;
        int mGlossaryM4aCount = 0;
        int mGlossaryOggCount = 0;
        Dictionary<string, int> mTypeCounts = new Dictionary<string,int>();
        Dictionary<string, int> mTermCounts = new Dictionary<string, int>();
        Dictionary<string, int> mTranslationCounts = new Dictionary<string, int>();
        Dictionary<string, int> mM4aTranslationCounts = new Dictionary<string, int>();
        Dictionary<string, int> mOggTranslationCounts = new Dictionary<string, int>();
        Dictionary<string, int> mRubricCounts = new Dictionary<string, int>();

        TextWriter mTextGlossaryReport;
        TextWriter mAudioGlossaryReport;
        TextWriter mItemReport;
        TextWriter mErrorReport;

        public Tabulator(string rootPath)
        {
            mRootPath = rootPath;
        }

        public void Tabulate()
        {
            if (!File.Exists(Path.Combine(mRootPath, "imsmanifest.xml"))) throw new ArgumentException("Not a valid content package path. File imsmanifest.xml not found!");
            Console.WriteLine("Tabulating " + mRootPath);

            try
            {
                {
                    string errorReportPath = Path.Combine(mRootPath, cErrorReportFn);
                    if (File.Exists(errorReportPath)) File.Delete(errorReportPath);
                }
                mTextGlossaryReport = new StreamWriter(Path.Combine(mRootPath, cTextGlossaryReportFn), false, sUtf8NoBomEncoding);
                mTextGlossaryReport.WriteLine("Folder,WIT_ID,Index,Term,Language,Length");
                mAudioGlossaryReport = new StreamWriter(Path.Combine(mRootPath, cAudioGlossaryReportFn));
                mAudioGlossaryReport.WriteLine("Folder,WIT_ID,Index,Term,Language,Encoding,Size");
                mItemReport = new StreamWriter(Path.Combine(mRootPath, cItemReportFn));
                mItemReport.WriteLine("Folder,ItemId,ItemType,Subject,Grade,Rubric,AsmtType,Standard,Claim,Target,ASL,BrailleEmbedded,BrailleFile,Translation");

                DirectoryInfo diItems = new DirectoryInfo(Path.Combine(mRootPath, "Items"));
                foreach (DirectoryInfo diItem in diItems.EnumerateDirectories())
                {
                    try
                    {
                        TabulateItem(diItem);
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine();
#if DEBUG
                        Console.WriteLine(err.ToString());
#else
                        Console.WriteLine(err.Message);
#endif
                        Console.WriteLine();
                        ReportError(diItem, err.ToString());
                    }
                }

                using (StreamWriter summaryReport = new StreamWriter(Path.Combine(mRootPath, cSummaryReportFn), false, sUtf8NoBomEncoding))
                {
                    SummaryReport(summaryReport);
                }

                // Report aggregate results to the console
                SummaryReport(Console.Out);
                Console.WriteLine();

                /*
                Console.WriteLine("Glossary Term Counts:");
                mTermCounts.Dump();
                Console.WriteLine();
                */
            }
            finally
            {
                if (mItemReport != null)
                {
                    mItemReport.Dispose();
                    mItemReport = null;
                }
                if (mAudioGlossaryReport != null)
                {
                    mAudioGlossaryReport.Dispose();
                    mAudioGlossaryReport = null;
                }
                if (mTextGlossaryReport != null)
                {
                    mTextGlossaryReport.Dispose();
                    mTextGlossaryReport = null;
                }
                if (mErrorReport != null)
                {
                    mErrorReport.Dispose();
                    mErrorReport = null;
                }
            }
        }

        private void TabulateItem(DirectoryInfo diItem)
        {
            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            xml.Load(Path.Combine(diItem.FullName, diItem.Name + ".xml"));
            string itemType = xml.XpEval("itemrelease/item/@format");
            if (itemType == null) itemType = xml.XpEval("itemrelease/item/@type");
            if (itemType == null) throw new InvalidDataException("Item type not found");
            string itemId = xml.XpEval("itemrelease/item/@id");
            if (itemId == null) throw new InvalidDataException("Item id not found");

            // Add to the item count and the type count
            ++mItemCount;
            mTypeCounts.Increment(itemType);

            switch (itemType)
            {
                case "EBSR":        // Evidence-Based Selected Response
                case "eq":          // Equation
                case "er":          // Extended-Response
                case "gi":          // Grid Item (graphic)
                case "htq":         // Hot Text (QTI)
                case "mc":          // Multiple Choice
                case "mi":          // Match Interaction
                case "ms":          // Multi-Select
                case "nl":          // Natural Language
                case "sa":          // Short Answer
                case "SIM":         // Simulation
                case "ti":          // Table Interaction
                case "wer":         // Writing Extended Response
                    TabulateInteraction(diItem, xml, itemId, itemType);
                    break;

                case "wordList":    // Word List (Glossary)
                    TabulateWordList(diItem, xml, itemId);
                    break;

                case "pass":        // Passage
                case "tut":         // Tutorial
                    break;  // Ignore for the moment

                default:
                    ReportError(diItem, "Unexpected item type: " + itemType);
                    break;
            }

            if (string.Equals(itemType, "wordList", StringComparison.Ordinal))
            {
                TabulateWordList(diItem, xml, itemId);
            }
        }

        void TabulateInteraction(DirectoryInfo diItem, XmlDocument xml, string itemId, string itemType)
        {
            string metadataPath = Path.Combine(diItem.FullName, "metadata.xml");
            if (!File.Exists(metadataPath)) throw new InvalidDataException("Metadata file not found: " + metadataPath);
            XmlDocument xmlMetadata = new XmlDocument(sXmlNt);
            xmlMetadata.Load(Path.Combine(diItem.FullName, "metadata.xml"));

            // Folder
            Debug.Assert(diItem.FullName.StartsWith(mRootPath));
            string folder = diItem.FullName.Substring(mRootPath.Length);

            // Subject
            string subject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            // Grade
            string grade = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:IntendedGrade", sXmlNs);
            
            // Rubric
            string rubric = string.Empty;
            {
                // Look for machineRubric element
                string machineFilename = xml.XpEval("itemrelease/item/MachineRubric/@filename");
                if (machineFilename != null)
                {
                    rubric = Path.GetExtension(machineFilename).ToLower();
                    if (rubric.Length > 0) rubric = rubric.Substring(1);

                    if (!File.Exists(Path.Combine(diItem.FullName, machineFilename))) ReportError(diItem, "Item specifies machine rubric '{0}' but file was not found.");
                }

                // Try answer key element
                if (string.IsNullOrEmpty(rubric))
                {
                    XmlElement xmlEle = xml.SelectSingleNode("itemrelease/item/attriblist/attrib[@attid='itm_att_Answer Key']") as XmlElement;
                    if (xmlEle != null)
                    {
                        rubric = "AnswerKeyProperty";
                    }
                }

                // Todo: Check the metadata value for ScoringEngine and tabulate permutation
                // match with what we've done so far. Then add validation code.

                if (string.IsNullOrEmpty(rubric)) ReportUnexpectedFiles(diItem, "Machine Rubric", "*.qrx");
            }
            mRubricCounts.Increment(rubric);

            // AssessmentType (PT or CAT)
            string assessmentType = string.Equals(xmlMetadata.XpEvalE("metadata/smarterAppMetadata/PerformanceTaskComponentItem"), "Y", StringComparison.OrdinalIgnoreCase) ? "PT" : "CAT";
            
            // Standard, Claim and Target
            string standard;
            string claim;
            string target;
            StandardFromMetadata(xmlMetadata, out standard, out claim, out target);
            if (string.IsNullOrEmpty(standard))
            {
                ReportError(diItem, "No PrimaryStandard specified in metadata.");
            }

            // ASL
            string asl = string.Empty;
            {
                bool aslFound = CheckForAttachment(diItem, xml, "ASL", "MP4");
                if (aslFound) asl = "MP4";
                if (!aslFound) ReportUnexpectedFiles(diItem, "ASL video", "item_{0}_ASL*", itemId);

                bool aslInMetadata = string.Equals(xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AccessibilityTagsASLLanguage", sXmlNs), "Y", StringComparison.OrdinalIgnoreCase);
                if (aslInMetadata && !aslFound) ReportError(diItem, "Item metadata specifies ASL but no ASL in item.");
                if (!aslInMetadata && aslFound) ReportError(diItem, "Item has ASL but not indicated in the metadata.");
            }

            // BrailleEmbedded
            string brailleEmbedded = "No";
            {
                XmlElement xmlEle = xml.SelectSingleNode("itemrelease/item/content//brailleText") as XmlElement;
                if (xmlEle != null && xmlEle.HasChildNodes) brailleEmbedded = "Yes";
            }

            // BrailleFile
            string brailleFile = string.Empty;
            {
                bool brfFound = CheckForAttachment(diItem, xml, "BRF", "BRF");
                if (brfFound) brailleFile = "BRF";
                if (!brfFound) ReportUnexpectedFiles(diItem, "Braille BRF", "item_{0}_*.brf", itemId);

                bool prnFound = CheckForAttachment(diItem, xml, "PRN", "PRN");
                if (prnFound)
                {
                    if (brailleFile.Length > 0) brailleFile = string.Concat(brailleFile, " ", "PRN");
                    else brailleFile = "PRN";
                }
                if (!prnFound) ReportUnexpectedFiles(diItem, "Braille PRN", "item_{0}_*.prn", itemId);
            }

            // Translation
            string translation = string.Empty;
            {               
                // Find non-english content and the language value
                HashSet<string> languages = new HashSet<string>();
                foreach (XmlElement xmlEle in xml.SelectNodes("itemrelease/item/content"))
                {
                    string language = xmlEle.GetAttribute("language").ToLower();

                    // The spec says that languages should be in RFC 5656 format.
                    // However, the items use ENU for English and ESN for Spanish.
                    // Neither of these are compliant with RFC 5656.
                    // Meanwhile, the metadata file uses eng for English and spa for Spanish which,
                    // at least abides the spec which says that ISO-639-2 should be used.
                    // (Note that ISO-639-2 codes are included in RFC 5656).
                    switch (language)
                    {
                        case "enu":
                            language = "eng";
                            break;
                        case "esn":
                            language = "spa";
                            break;
                    }

                    // Add to hashset
                    languages.Add(language.ToLower());

                    // If not english, add to result
                    if (!string.Equals(language, "eng", StringComparison.Ordinal))
                    {
                        translation = (translation.Length > 0) ? string.Concat(translation, " ", language) : language;
                    }

                    // See if metadata agrees
                    XmlNode node = xmlMetadata.SelectSingleNode(string.Concat("metadata/sa:smarterAppMetadata/sa:Language[. = '", language, "']"), sXmlNs);
                    if (node == null) ReportError(diItem, "Item content includes '{0}' language but metadata does not have a corresponding <Language> entry.", language);
                }

                // Now, search the metadata for translations and make sure all exist in the content
                foreach(XmlElement xmlEle in xmlMetadata.SelectNodes("metadata/sa:smarterAppMetadata/sa:Language", sXmlNs))
                {
                    string language = xmlEle.InnerText;
                    if (!languages.Contains(language))
                    {
                        ReportError(diItem, "Item metadata indicates '{0}' language but item content does not include that language.", language);
                    }
                }
            }

            // Folder,ItemId,ItemType,Subject,Grade,Rubric,AsmtType,Standard,Claim,Target,ASL,BrailleEmbedded,BrailleFile,Translation
            mItemReport.WriteLine(string.Join(",", CsvEncode(folder), CsvEncode(itemId), CsvEncode(itemType), CsvEncode(subject), CsvEncode(grade), CsvEncode(rubric), CsvEncode(assessmentType), CsvEncode(standard), CsvEncodeExcel(claim), CsvEncodeExcel(target), CsvEncode(asl), CsvEncode(brailleEmbedded), CsvEncode(brailleFile), CsvEncode(translation)));
        }

        bool CheckForAttachment(DirectoryInfo diItem, XmlDocument xml, string attachType, string expectedExtension)
        {
            XmlElement xmlEle = xml.SelectSingleNode(string.Concat("itemrelease/item/content/attachmentlist/attachment[@type='", attachType, "']")) as XmlElement;
            if (xmlEle != null)
            {
                string filename = xmlEle.GetAttribute("file");
                if (string.IsNullOrEmpty(filename))
                {
                    ReportError(diItem, "Attachment of type '{0}' missing file attribute.", attachType);
                    return false;
                }
                if (!File.Exists(Path.Combine(diItem.FullName, filename)))
                {
                    ReportError(diItem, "Dangling Reference: Item specifies '{0}' attachment '{1}' but file does not exist.", attachType, filename);
                    return false;
                }

                string extension = Path.GetExtension(filename);
                if (extension.Length > 0) extension = extension.Substring(1); // Strip leading "."
                if (!string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase))
                {
                    ReportError(diItem, "Attachment of type '{0}' has extension '{1}', expected '{2}'. Filename='{3}'.", attachType, extension, expectedExtension, filename);
                }
                return true;
            }
            return false;
        }

        void ReportUnexpectedFiles(DirectoryInfo diItem, string attachDescription, string pattern, params object[] args)
        {
            foreach (FileInfo file in diItem.GetFiles(string.Format(pattern, args)))
            {
                ReportError(diItem, "Item does not specify {0} but file '{1}' found.", attachDescription, file.Name);
            }
        }

        /* 
         * Locate and parse the standard, claim, and target from the metadata
         * 
         * Claim and target are specified in one of the following formats:
         * SBAC-ELA-v1 (there is only one alignment for ELA, this is used for delivery)
         *     Claim|Assessment Target|Common Core Standard
         * SBAC-MA-v6 (Math, based on the blueprint hierarchy, primary alignment and does not go to standard level, THIS IS USED FOR DELIVERY, should be the same as SBAC-MA-v4)
         *     Claim|Content Category|Target Set|Assessment Target
         * SBAC-MA-v5 (Math, based on the content specifications hierarchy secondary alignment to the standard level)
         *     Claim|Content Domain|Target|Emphasis|Common Core Standard
         * SBAC-MA-v4 (Math, based on the content specifications hierarchy primary alignment to the standard level)
         *     Claim|Content Domain|Target|Emphasis|Common Core Standard
         */
        private class StandardCoding
        {
            public StandardCoding(string publication, int claimPart, int targetPart)
            {
                Publication = publication;
                ClaimPart = claimPart;
                TargetPart = targetPart;
            }

            public string Publication;
            public int ClaimPart;
            public int TargetPart;
        }

        private static readonly StandardCoding[] sStandardCodings = new StandardCoding[]
        {
            new StandardCoding("SBAC-ELA-v1", 0, 1),
            new StandardCoding("SBAC-MA-v6", 0, 2),
            new StandardCoding("SBAC-MA-v5", 0, 2),
            new StandardCoding("SBAC-MA-v4", 0, 2)
        };

        static void StandardFromMetadata(XmlDocument xmlMetadata, out string standard, out string claim, out string target)
        {
            // Try each coding
            foreach(StandardCoding coding in sStandardCodings)
            {
                string std = xmlMetadata.XpEval(string.Concat("metadata/sa:smarterAppMetadata/sa:StandardPublication[sa:Publication='", coding.Publication, "']/sa:PrimaryStandard"), sXmlNs);
                if (std != null)
                {
                    if (!std.StartsWith(string.Concat(coding.Publication, ":"), StringComparison.Ordinal))
                        throw new InvalidDataException(string.Format("Standard aligment with publication '{0}' has invalid value '{1}.", coding.Publication, std));

                    string[] parts = std.Substring(coding.Publication.Length + 1).Split('|');
                    standard = std;
                    claim = (parts.Length > coding.ClaimPart) ? parts[coding.ClaimPart] : string.Empty;
                    target = (parts.Length > coding.TargetPart) ? parts[coding.TargetPart] : string.Empty;
                    return;
                }
            }

            standard = string.Empty;
            claim = string.Empty;
            target = string.Empty;
        }

        static readonly Regex sRxParseAudiofile = new Regex(@"Item_(\d+)_v(\d+)_(\d+)_(\d+)([a-zA-Z]+)_glossary_", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private void TabulateWordList(DirectoryInfo diItem, XmlDocument xml, string itemId)
        {
            Debug.Assert(diItem.FullName.StartsWith(mRootPath));
            string folder = CsvEncode(diItem.FullName.Substring(mRootPath.Length));
            
            List<string> terms = new List<string>();
            ++mWordlistCount;
            foreach (XmlNode kwNode in xml.SelectNodes("itemrelease/item/keywordList/keyword"))
            {
                ++mGlossaryTermCount;
                string term = kwNode.XpEval("@text");
                int index = int.Parse(kwNode.XpEval("@index"));
                mTermCounts.Increment(term);

                while (terms.Count < index + 1) terms.Add(string.Empty);
                terms[index] = term;

                foreach(XmlNode htmlNode in kwNode.SelectNodes("html"))
                {
                    string language = htmlNode.XpEval("@listType");
                    mTranslationCounts.Increment(language);

                    // Folder,WIT_ID,Index,Term,Language,Length
                    mTextGlossaryReport.WriteLine(string.Join(",", folder, CsvEncode(itemId), index.ToString(), CsvEncode(term), CsvEncode(language), htmlNode.InnerXml.Length.ToString()));
                }
            }

            // Tablulate m4a audio translations
            foreach (FileInfo fi in diItem.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                // If Audio file
                string extension = fi.Extension.Substring(1).ToLower();
                if (string.Equals(extension, "m4a", StringComparison.Ordinal) || string.Equals(extension, "ogg", StringComparison.Ordinal))
                {
                    Match match = sRxParseAudiofile.Match(fi.Name);
                    if (match.Success)
                    {
                        string language = match.Groups[5].Value;
                        int index = int.Parse(match.Groups[4].Value);

                        if (index == 0 || index >= terms.Count)
                        {
                            ReportError(diItem, "Audio file {0} has index {1} with no matching term.", fi.Name, index);
                            continue;
                        }

                        string term = terms[index];

                        if (string.Equals(extension, "m4a", StringComparison.Ordinal))
                        {
                            mM4aTranslationCounts.Increment(language);
                            ++mGlossaryM4aCount;
                        }
                        else
                        {
                            mOggTranslationCounts.Increment(language);
                            ++mGlossaryOggCount;
                        }

                        // Folder,WIT_ID,Index,Term,Language,Encoding,Size
                        mAudioGlossaryReport.WriteLine(String.Join(",", folder, CsvEncode(itemId), index.ToString(), CsvEncode(term), CsvEncode(language), CsvEncode(extension), fi.Length.ToString()));
                    }
                    else
                    {
                        ReportError(diItem, "Audio Glossary Filename in unrecognized format: ", fi.Name);
                    }
                }
            }
        }

        void SummaryReport(TextWriter writer)
        {
            if (mErrorCount != 0) writer.WriteLine("Errors: {0}", mErrorCount);
            writer.WriteLine("Items: {0}", mItemCount);
            writer.WriteLine("Word Lists: {0}", mWordlistCount);
            writer.WriteLine("Glossary Terms: {0}", mGlossaryTermCount);
            writer.WriteLine("Unique Glossary Terms: {0}", mTermCounts.Count);
            writer.WriteLine("Glossary m4a Audio: {0}", mGlossaryM4aCount);
            writer.WriteLine("Glossary ogg Audio: {0}", mGlossaryOggCount);
            writer.WriteLine();
            writer.WriteLine("Item Type Counts:");
            mTypeCounts.Dump(writer);
            writer.WriteLine();
            writer.WriteLine("Translation Counts:");
            mTranslationCounts.Dump(writer);
            writer.WriteLine();
            writer.WriteLine("M4a Translation Counts:");
            mM4aTranslationCounts.Dump(writer);
            writer.WriteLine();
            writer.WriteLine("Ogg Translation Counts:");
            mOggTranslationCounts.Dump(writer);
            writer.WriteLine();
            writer.WriteLine("Rubric Counts:");
            mRubricCounts.Dump(writer);
            writer.WriteLine();
        }

        void ReportError(string msg)
        {
            if (mErrorReport == null)
            {
                mErrorReport = new StreamWriter(Path.Combine(mRootPath, cErrorReportFn), false, sUtf8NoBomEncoding);
            }
            mErrorReport.Write(msg);
            if (msg[msg.Length - 1] != '\n') mErrorReport.WriteLine();
            mErrorReport.WriteLine();
            ++mErrorCount;
        }

        void ReportError(DirectoryInfo diItem, string msg)
        {
            ReportError(string.Concat(diItem.Name, "\r\n", msg));

        }

        void ReportError(DirectoryInfo diItem, string msg, params object[] args)
        {
            ReportError(diItem, string.Format(msg, args));
        }

        private static readonly char[] cCsvEscapeChars = {',', '"', '\'', '\r', '\n'};

        static string CsvEncode(string text)
        {
            if (text.IndexOfAny(cCsvEscapeChars) < 0) return text;
            return string.Concat("\"", text.Replace("\"", "\"\""), "\"");
        }

        static string CsvEncodeExcel(string text)
        {
            return string.Concat("=\"", text.Replace("\"", "\"\""), "\"");
        }

    }

    static class TabulatorHelp
    {
        public static string XpEval(this XmlNode doc, string xpath, XmlNamespaceManager xmlns = null)
        {
            XmlNode node = doc.SelectSingleNode(xpath, xmlns);
            if (node == null) return null;
            return node.InnerText;
        }

        public static string XpEvalE(this XmlNode doc, string xpath, XmlNamespaceManager xmlns = null)
        {
            XmlNode node = doc.SelectSingleNode(xpath, xmlns);
            if (node == null) return string.Empty;
            return node.InnerText;
        }

        public static void Increment(this Dictionary<string, int> dict, string key)
        {
            int count;
            if (!dict.TryGetValue(key, out count)) count = 0;
            dict[key] = count + 1;
        }

        public static void Dump(this Dictionary<string, int> dict, TextWriter writer)
        {
            foreach (var pair in dict)
            {
                writer.WriteLine("  {0}: {1}", pair.Key, pair.Value);
            }
        }
    }
}
