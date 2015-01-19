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
        const string cErrorReportFn = "ErrorReport.csv";

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
        string mErrorReportPath;
        TextWriter mErrorReport;
        string mSummaryReportPath;

        public Tabulator(string rootPath)
        {
            mRootPath = Path.GetFullPath(rootPath);
        }

        // Tabulate a package in the specified directory
        public void TabulateOne()
        {
            try
            {
                Initialize(mRootPath);
                TabulatePackage(mRootPath);
            }
            finally
            {
                Conclude();
            }
        }

        // Individually tabulate each package in subdirectories
        public void TabulateEach()
        {
            DirectoryInfo diRoot = new DirectoryInfo(mRootPath);

            foreach(DirectoryInfo diPackageFolder in diRoot.GetDirectories())
            {
                if (File.Exists(Path.Combine(diPackageFolder.FullName, "imsmanifest.xml")))
                {
                    try
                    {
                        Initialize(diPackageFolder.FullName);
                        TabulatePackage(diPackageFolder.FullName);
                    }
                    finally
                    {
                        Conclude();
                    }
                }
            }
        }

        // Tabulate packages in subdirectories and aggregate the results
        public void TabulateAggregate()
        {
            DirectoryInfo diRoot = new DirectoryInfo(mRootPath);
            try
            {
                Initialize(mRootPath);

                foreach (DirectoryInfo diPackageFolder in diRoot.GetDirectories())
                {
                    if (File.Exists(Path.Combine(diPackageFolder.FullName, "imsmanifest.xml")))
                    {
                        TabulatePackage(diPackageFolder.FullName);
                    }
                }
            }
            finally
            {
                Conclude();
            }
        }

        // Initialize all files and collections for a tabulation run
        private void Initialize(string reportFolderPath)
        {
            mErrorReportPath = Path.Combine(reportFolderPath, cErrorReportFn);
            if (File.Exists(mErrorReportPath)) File.Delete(mErrorReportPath);

            mSummaryReportPath = Path.Combine(reportFolderPath, cSummaryReportFn);
            if (File.Exists(mSummaryReportPath)) File.Delete(mSummaryReportPath);

            mTextGlossaryReport = new StreamWriter(Path.Combine(reportFolderPath, cTextGlossaryReportFn), false, sUtf8NoBomEncoding);
            mTextGlossaryReport.WriteLine("Folder,WIT_ID,Index,Term,Language,Length");

            mAudioGlossaryReport = new StreamWriter(Path.Combine(reportFolderPath, cAudioGlossaryReportFn));
            mAudioGlossaryReport.WriteLine("Folder,WIT_ID,Index,Term,Language,Encoding,Size");

            mItemReport = new StreamWriter(Path.Combine(reportFolderPath, cItemReportFn));
            mItemReport.WriteLine("Folder,ItemId,ItemType,Subject,Grade,Rubric,AsmtType,Standard,Claim,Target,ASL,BrailleEmbedded,BrailleFile,Translation");

            mTypeCounts.Clear();
            mTermCounts.Clear();
            mTranslationCounts.Clear();
            mM4aTranslationCounts.Clear();
            mOggTranslationCounts.Clear();
            mRubricCounts.Clear();
        }

        private void Conclude()
        {
            try
            {
                using (StreamWriter summaryReport = new StreamWriter(mSummaryReportPath, false, sUtf8NoBomEncoding))
                {
                    SummaryReport(summaryReport);
                }

                // Report aggregate results to the console
                SummaryReport(Console.Out);
                Console.WriteLine();
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

        public void TabulatePackage(string packageFolderPath)
        {
            if (!File.Exists(Path.Combine(packageFolderPath, "imsmanifest.xml"))) throw new ArgumentException("Not a valid content package path. File imsmanifest.xml not found!");
            Console.WriteLine("Tabulating " + packageFolderPath);

            DirectoryInfo diItems = new DirectoryInfo(Path.Combine(packageFolderPath, "Items"));
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
                    ReportError(new ItemContext(this, diItem, null, null), ErrCat.Exception, err.ToString());
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

            ItemContext it = new ItemContext(this, diItem, itemId, itemType);

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
                //case "nl":          // Natural Language
                case "sa":          // Short Answer
                case "SIM":         // Simulation
                case "ti":          // Table Interaction
                case "wer":         // Writing Extended Response
                    TabulateInteraction(it, xml);
                    break;

                case "wordList":    // Word List (Glossary)
                    TabulateWordList(it, xml);
                    break;

                case "pass":        // Passage
                case "tut":         // Tutorial
                    break;  // Ignore for the moment

                default:
                    ReportError(it, ErrCat.Unsupported, "Unexpected item type: " + itemType);
                    break;
            }
        }

        void TabulateInteraction(ItemContext it, XmlDocument xml)
        {
            string metadataPath = Path.Combine(it.DiItem.FullName, "metadata.xml");
            if (!File.Exists(metadataPath)) throw new InvalidDataException("Metadata file not found: " + metadataPath);
            XmlDocument xmlMetadata = new XmlDocument(sXmlNt);
            xmlMetadata.Load(Path.Combine(it.DiItem.FullName, "metadata.xml"));

            // Subject
            string subject = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_item_subject']/val");
            string metaSubject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            if (string.IsNullOrEmpty(subject))
            {
                ReportError(it, ErrCat.Attribute, "Missing subject in item attributes (itm_att_Grade).");
                subject = metaSubject;
                if (string.IsNullOrEmpty(subject))
                    ReportError(it, ErrCat.Metadata, "Missing subject in item metadata.");
            }
            else
            {
                if (!string.Equals(subject, metaSubject, StringComparison.Ordinal))
                    ReportError(it, ErrCat.Metadata, "Item indicates subject '{0} but metadata indicates subject '{1}'.", subject, metaSubject);
            }

            // Grade
            string grade = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_att_Grade']/val");
            string metaGrade = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:IntendedGrade", sXmlNs);
            if (string.IsNullOrEmpty(grade))
            {
                ReportError(it, ErrCat.Attribute, "Missing grade in item attributes (itm_att_Grade).");
                grade = metaGrade;
                if (string.IsNullOrEmpty(grade))
                    ReportError(it, ErrCat.Metadata, "Missing grade in item metadata.");
            }
            else
            {
                if (!string.Equals(grade, metaGrade, StringComparison.Ordinal))
                    ReportError(it, ErrCat.Metadata, "Item indicates grade '{0} but metadata indicates grade '{1}'.", grade, metaGrade);
            }
            
            // Rubric
            string rubric = string.Empty;
            {
                string answerKeyValue = string.Empty;
                XmlElement xmlEle = xml.SelectSingleNode("itemrelease/item/attriblist/attrib[@attid='itm_att_Answer Key']") as XmlElement;
                if (xmlEle != null)
                {
                    answerKeyValue = xmlEle.XpEvalE("val");
                }

                string machineRubricType = string.Empty;
                string machineRubricFilename = xml.XpEval("itemrelease/item/MachineRubric/@filename");
                if (machineRubricFilename != null)
                {
                    machineRubricType = Path.GetExtension(machineRubricFilename).ToLower();
                    if (machineRubricType.Length > 0) machineRubricType = machineRubricType.Substring(1);
                    if (!File.Exists(Path.Combine(it.DiItem.FullName, machineRubricFilename)))
                        ReportError(it, ErrCat.Rubric, "Item specifies machine rubric '{0}' but file was not found.");
                }

                string metadataScoringEngine = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:ScoringEngine", sXmlNs);

                // Count the rubric types
                mRubricCounts.Increment(string.Concat(it.ItemType, " '", xmlEle.XpEvalE("val"), "' ", machineRubricType ));

                // Rubric type is dictated by item type
                bool usesMachineRubric = false;
                string metadataExpected = null;
                switch (it.ItemType)
                {
                    case "mc":      // Multiple Choice
                        rubric = "Embedded";
                        metadataExpected = "Automatic with Key";
                        if (answerKeyValue.Length != 1 || answerKeyValue[0] < 'A' || answerKeyValue[0] > 'Z')
                            ReportError(it, ErrCat.Rubric, "Unexpected MC answer key value: '{0}'", answerKeyValue);
                        break;

                    case "ms":      // Multi-select
                        rubric = "Embedded";
                        metadataExpected = "Automatic with Key(s)";
                        {
                            string[] parts = answerKeyValue.Split(',');
                            bool validAnswer = parts.Length > 0;
                            foreach(string answer in parts)
                            {
                                if (answer.Length != 1 || answer[0] < 'A' || answer[0] > 'Z') validAnswer = false;
                            }
                            if (!validAnswer) ReportError(it, ErrCat.Rubric, "Unexpected MS answer key value: '{0}'", answerKeyValue);
                        }
                        break;

                    case "EBSR":    // Evidence-based selectd response
                        rubric = "Embedded";
                        usesMachineRubric = true;
                        metadataExpected = "Automatic with Key(s)";
                        if (answerKeyValue.Length != 1 || answerKeyValue[0] < 'A' || answerKeyValue[0] > 'Z')
                            ReportError(it, ErrCat.Rubric, "Unexpected MC answer key: '{0}'", answerKeyValue);
                        break;
                    // TODO: Add check for part 1 of EBSR (in "itm_att_Item Format")

                    case "eq":          // Equation
                    case "gi":          // Grid Item (graphic)
                    case "htq":         // Hot Text (in wrapped-QTI format)
                    case "mi":          // Match Interaction
                    case "ti":          // Table Interaction
                        metadataExpected = (machineRubricFilename != null) ? "Automatic with Machine Rubric" : "HandScored";
                        usesMachineRubric = true;
                        rubric = machineRubricType;
                        if (!string.Equals(answerKeyValue, it.ItemType.ToUpper()))
                            ReportError(it, ErrCat.Rubric, "Answer key attribute is '{0}', expected '{1}'.", answerKeyValue, it.ItemType.ToUpper());
                        break;

                    case "er":          // Extended-Response
                    case "sa":          // Short Answer
                    case "wer":         // Writing Extended Response
                        metadataExpected = "HandScored";
                        if (!string.Equals(answerKeyValue, it.ItemType.ToUpper()))
                            ReportError(it, ErrCat.Rubric, "Answer key attribute is '{0}', expected '{1}'.", answerKeyValue, it.ItemType.ToUpper());
                        break;

                    default:
                        ReportError(it, ErrCat.Unsupported, "Validation of rubrics for type '{0}' is not supported.", it.ItemType);
                        break;
                }

                if (metadataExpected != null && !string.Equals(metadataScoringEngine, metadataExpected, StringComparison.Ordinal))
                {
                    if (string.Equals(metadataScoringEngine, metadataExpected, StringComparison.OrdinalIgnoreCase))
                        ReportError(it, ErrCat.Metadata, "Capitalization error in ScoringEngine metadata. Found '{0}', expected '{1}'.", metadataScoringEngine, metadataExpected);
                    else
                        ReportError(it, ErrCat.Metadata, "Incorrect ScoringEngine metadata for type '{0}'. Found '{1}', expected '{2}'.", it.ItemType, metadataScoringEngine, metadataExpected);
                }

                if (!string.IsNullOrEmpty(machineRubricFilename) && !usesMachineRubric)
                    ReportError(it, ErrCat.Rubric, "Unexpected machine rubric found for item type '{0}': {1}", it.ItemType, machineRubricFilename);

                // Check for unreferenced machine rubrics
                foreach(FileInfo fi in it.DiItem.EnumerateFiles("*.qrx"))
                {
                    if (machineRubricFilename == null || !string.Equals(fi.Name, machineRubricFilename, StringComparison.OrdinalIgnoreCase))
                        ReportError(it, ErrCat.Rubric, "Machine rubric file found but not referenced in <MachineRubric> element: {0}", fi.Name);
                }
            }

            // AssessmentType (PT or CAT)
            string assessmentType;
            {
                string meta = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PerformanceTaskComponentItem", sXmlNs);
                if (meta == null || string.Equals(meta, "N", StringComparison.Ordinal))assessmentType = "CAT";
                else if (string.Equals(meta, "Y", StringComparison.Ordinal)) assessmentType  = "PT";
                else
                {
                    assessmentType = "CAT";
                    ReportError(it, ErrCat.Metadata, "PerformanceTaskComponentItem should be 'Y' or 'N'. Found '{0}'.", meta);
                }
            }
            
            // Standard, Claim and Target
            string standard;
            string claim;
            string target;
            StandardFromMetadata(it, xmlMetadata, out standard, out claim, out target);
            if (string.IsNullOrEmpty(standard))
            {
                ReportError(it, ErrCat.Metadata, "No PrimaryStandard specified in metadata.");
            }

            // ASL
            string asl = string.Empty;
            {
                bool aslFound = CheckForAttachment(it, xml, "ASL", "MP4");
                if (aslFound) asl = "MP4";
                if (!aslFound) ReportUnexpectedFiles(it, "ASL video", "item_{0}_ASL*", it.ItemId);

                bool aslInMetadata = string.Equals(xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AccessibilityTagsASLLanguage", sXmlNs), "Y", StringComparison.OrdinalIgnoreCase);
                if (aslInMetadata && !aslFound) ReportError(it, ErrCat.Metadata, "Item metadata specifies ASL but no ASL in item.");
                if (!aslInMetadata && aslFound) ReportError(it, ErrCat.Metadata, "Item has ASL but not indicated in the metadata.");
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
                bool brfFound = CheckForAttachment(it, xml, "BRF", "BRF");
                if (brfFound) brailleFile = "BRF";
                if (!brfFound) ReportUnexpectedFiles(it, "Braille BRF", "item_{0}_*.brf", it.ItemId);

                bool prnFound = CheckForAttachment(it, xml, "PRN", "PRN");
                if (prnFound)
                {
                    if (brailleFile.Length > 0) brailleFile = string.Concat(brailleFile, " ", "PRN");
                    else brailleFile = "PRN";
                }
                if (!prnFound) ReportUnexpectedFiles(it, "Braille PRN", "item_{0}_*.prn", it.ItemId);
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
                    if (node == null) ReportError(it, ErrCat.Metadata, "Item content includes '{0}' language but metadata does not have a corresponding <Language> entry.", language);
                }

                // Now, search the metadata for translations and make sure all exist in the content
                foreach(XmlElement xmlEle in xmlMetadata.SelectNodes("metadata/sa:smarterAppMetadata/sa:Language", sXmlNs))
                {
                    string language = xmlEle.InnerText;
                    if (!languages.Contains(language))
                    {
                        ReportError(it, ErrCat.Metadata, "Item metadata indicates '{0}' language but item content does not include that language.", language);
                    }
                }
            }

            // Folder,it.ItemId,it.ItemType,Subject,Grade,Rubric,AsmtType,Standard,Claim,Target,ASL,BrailleEmbedded,BrailleFile,Translation
            mItemReport.WriteLine(string.Join(",", CsvEncode(it.Folder), CsvEncode(it.ItemId), CsvEncode(it.ItemType), CsvEncode(subject), CsvEncode(grade), CsvEncode(rubric), CsvEncode(assessmentType), CsvEncode(standard), CsvEncodeExcel(claim), CsvEncodeExcel(target), CsvEncode(asl), CsvEncode(brailleEmbedded), CsvEncode(brailleFile), CsvEncode(translation)));
        }

        bool CheckForAttachment(ItemContext it, XmlDocument xml, string attachType, string expectedExtension)
        {
            XmlElement xmlEle = xml.SelectSingleNode(string.Concat("itemrelease/item/content/attachmentlist/attachment[@type='", attachType, "']")) as XmlElement;
            if (xmlEle != null)
            {
                string filename = xmlEle.GetAttribute("file");
                if (string.IsNullOrEmpty(filename))
                {
                    ReportError(it, ErrCat.Item, "Attachment of type '{0}' missing file attribute.", attachType);
                    return false;
                }
                if (!File.Exists(Path.Combine(it.DiItem.FullName, filename)))
                {
                    ReportError(it, ErrCat.Item, "Dangling Reference: Item specifies '{0}' attachment '{1}' but file does not exist.", attachType, filename);
                    return false;
                }

                string extension = Path.GetExtension(filename);
                if (extension.Length > 0) extension = extension.Substring(1); // Strip leading "."
                if (!string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase))
                {
                    ReportError(it, ErrCat.Item, "Attachment of type '{0}' has extension '{1}', expected '{2}'. Filename='{3}'.", attachType, extension, expectedExtension, filename);
                }
                return true;
            }
            return false;
        }

        void ReportUnexpectedFiles(ItemContext it, string attachDescription, string pattern, params object[] args)
        {
            foreach (FileInfo file in it.DiItem.GetFiles(string.Format(pattern, args)))
            {
                ReportError(it, ErrCat.Item, "Item does not specify {0} but file '{1}' found.", attachDescription, file.Name);
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
            new StandardCoding("SBAC-MA-v6", 0, 3),
            new StandardCoding("SBAC-MA-v5", 0, 2),
            new StandardCoding("SBAC-MA-v4", 0, 2)
        };

        void StandardFromMetadata(ItemContext it, XmlDocument xmlMetadata, out string standard, out string claim, out string target)
        {
            // Try each coding
            foreach(StandardCoding coding in sStandardCodings)
            {
                string std = xmlMetadata.XpEval(string.Concat("metadata/sa:smarterAppMetadata/sa:StandardPublication[sa:Publication='", coding.Publication, "']/sa:PrimaryStandard"), sXmlNs);
                if (std != null)
                {
                    if (!std.StartsWith(string.Concat(coding.Publication, ":"), StringComparison.Ordinal))
                    {
                        ReportError(it, ErrCat.Metadata, "Standard alignment with publication '{0}' has invalid value '{1}.", coding.Publication, std);
                        continue;   // See if another coding works
                    }

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

        static readonly Regex sRxParseAudiofile = new Regex(@"Item_(\d+)_v(\d+)_(\w+)_(\d+)([a-zA-Z]+)_glossary_", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private void TabulateWordList(ItemContext it, XmlDocument xml)
        {
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
                    mTextGlossaryReport.WriteLine(string.Join(",", it.Folder, CsvEncode(it.ItemId), index.ToString(), CsvEncode(term), CsvEncode(language), htmlNode.InnerXml.Length.ToString()));
                }
            }

            // Tablulate m4a audio translations
            foreach (FileInfo fi in it.DiItem.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
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
                            ReportError(it, ErrCat.Item, "Audio file {0} has index {1} with no matching glossary term.", fi.Name, index);
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
                        mAudioGlossaryReport.WriteLine(String.Join(",", it.Folder, CsvEncode(it.ItemId), index.ToString(), CsvEncode(term), CsvEncode(language), CsvEncode(extension), fi.Length.ToString()));
                    }
                    else
                    {
                        ReportError(it, ErrCat.Unsupported, "Audio Glossary Filename in unrecognized format: {0}", fi.Name);
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

        // Error Categories
        enum ErrCat
        {
            Exception,
            Unsupported,
            Attribute,
            Rubric,
            Metadata,
            Item
        }

        void ReportError(ItemContext it, ErrCat category, string msg, params object[] args)
        {
            if (mErrorReport == null)
            {
                mErrorReport = new StreamWriter(Path.Combine(mRootPath, cErrorReportFn), false, sUtf8NoBomEncoding);
                mErrorReport.WriteLine("Folder,ItemId,ItemType,Category,ErrorMessage");
            }

            if (msg == null) msg = string.Empty;

            // "Folder,ItemId,ItemType,Category,ErrorMessage"
            mErrorReport.WriteLine(string.Join(",", CsvEncode(it.Folder), CsvEncode(it.ItemId), CsvEncode(it.ItemType), category.ToString(), CsvEncode(string.Format(msg, args))));

            ++mErrorCount;
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

        private class ItemContext
        {
            public ItemContext(Tabulator tabulator, DirectoryInfo diItem, string itemId, string itemType)
            {
                DiItem = diItem;
                ItemId = (itemId != null) ? itemId : string.Empty;
                ItemType = (itemType != null) ? itemType : string.Empty;
                if (diItem.FullName.StartsWith(tabulator.mRootPath))
                {
                    Folder = diItem.FullName.Substring(tabulator.mRootPath.Length);
                }
                else
                {
                    Folder = diItem.FullName;
                }
            }

            public DirectoryInfo DiItem { get; private set; }
            public string ItemId { get; private set; }
            public string ItemType { get; private set; }
            public string Folder { get; private set; }
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
            List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(dict);
            list.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b) { return b.Value - a.Value; });
            foreach (var pair in list)
            {
                writer.WriteLine("{0,6}: {1}", pair.Value, pair.Key);
            }
        }
    }
}
