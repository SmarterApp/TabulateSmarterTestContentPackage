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
            sXmlNs.AddNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instance");
            sXmlNs.AddNamespace("ims", "http://www.imsglobal.org/xsd/apip/apipv1p0/imscp_v1p1");
        }

        static readonly HashSet<string> sValidWritingTypes = new HashSet<string>(
            new string[] {
                "Explanatory",
                "Opinion",
                "Informative",
                "Argumentative",
                "Narrative"
            });

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

        // Per Package variables
        string mPackagePath;
        Dictionary<string, string> mFilenameToResourceId = new Dictionary<string, string>();
        HashSet<string> mResourceDependencies = new HashSet<string>();
        Dictionary<string, string> mWitIdToItemId = new Dictionary<string, string>();   // Wordlist ID to Item Id
        Dictionary<string, ItemContext> mIdToItemContext = new Dictionary<string, ItemContext>();

        // Per report variables
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

            mTextGlossaryReport = new StreamWriter(Path.Combine(reportFolderPath, cTextGlossaryReportFn), false, sUtf8NoBomEncoding);
            mTextGlossaryReport.WriteLine("Folder,WIT_ID,ItemId,Index,Term,Language,Length");

            mAudioGlossaryReport = new StreamWriter(Path.Combine(reportFolderPath, cAudioGlossaryReportFn));
            mAudioGlossaryReport.WriteLine("Folder,WIT_ID,ItemId,Index,Term,Language,Encoding,Size");

            mItemReport = new StreamWriter(Path.Combine(reportFolderPath, cItemReportFn));
            mItemReport.WriteLine("Folder,ItemId,ItemType,Subject,Grade,Rubric,AsmtType,Standard,Claim,Target,ASL,BrailleEmbedded,BrailleFile,Translation");

            mSummaryReportPath = Path.Combine(reportFolderPath, cSummaryReportFn);
            if (File.Exists(mSummaryReportPath)) File.Delete(mSummaryReportPath);

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
                if (mSummaryReportPath != null)
                {
                    using (StreamWriter summaryReport = new StreamWriter(mSummaryReportPath, false, sUtf8NoBomEncoding))
                    {
                        SummaryReport(summaryReport);
                    }

                    // Report aggregate results to the console
                    SummaryReport(Console.Out);
                    Console.WriteLine();
                }
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

            // Initialize package-specific collections
            mPackagePath = null;
            mFilenameToResourceId.Clear();
            mResourceDependencies.Clear();
            mWitIdToItemId.Clear();
            mIdToItemContext.Clear();

            mPackagePath = packageFolderPath;

            // Validate manifest
            try
            {
                ValidateManifest(packageFolderPath);
            }
            catch (Exception err)
            {
                ReportError(new ItemContext(this, new DirectoryInfo(packageFolderPath), null, null), ErrCat.Exception, ErrSeverity.Severe, err.ToString());
            }

            // First pass through items
            DirectoryInfo diItems = new DirectoryInfo(Path.Combine(packageFolderPath, "Items"));
            foreach (DirectoryInfo diItem in diItems.EnumerateDirectories())
            {
                try
                {
                    TabulateItem_Pass1(diItem);
                }
                catch (Exception err)
                {
                    ReportError(new ItemContext(this, diItem, null, null), ErrCat.Exception, ErrSeverity.Severe, err.ToString());
                }
            }

            // Second pass through items
            foreach (var entry in mIdToItemContext)
            {
                try
                {
                    TabulateItem_Pass2(entry.Value);
                }
                catch (Exception err)
                {
                    ReportError(entry.Value, ErrCat.Exception, ErrSeverity.Severe, err.ToString());
                }
            }

            // Verify Wordlist References
            VerifyWordlistReferences();
        }

        private void TabulateItem_Pass1(DirectoryInfo diItem)
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

            // Create and save the item context
            ItemContext it = new ItemContext(this, diItem, itemId, itemType);
            mIdToItemContext.Add(itemId, it);

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
                    // Defer wordlists to pass 2
                    break;

                case "pass":        // Passage
                case "tut":         // Tutorial
                    break;  // Ignore for the moment

                default:
                    ReportError(it, ErrCat.Unsupported, ErrSeverity.Benign, "Unexpected item type: " + itemType);
                    break;
            }
        }

        private void TabulateItem_Pass2(ItemContext it)
        {
            switch (it.ItemType)
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
                    // Do nothing on these item types in pass 2
                    break;

                case "wordList":    // Word List (Glossary)
                    TabulateWordList(it);
                    break;

                case "pass":        // Passage
                case "tut":         // Tutorial
                    break;  // Ignore for the moment

                default:
                    ReportError(it, ErrCat.Unsupported, ErrSeverity.Benign, "Unexpected item type: " + it.ItemType);
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
                ReportError(it, ErrCat.Attribute, ErrSeverity.Tolerable, "Missing subject in item attributes (itm_item_subject).");
                subject = metaSubject;
                if (string.IsNullOrEmpty(subject))
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Missing subject in item metadata.");
            }
            else
            {
                if (!string.Equals(subject, metaSubject, StringComparison.Ordinal))
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Subject mismatch between item and metadata.", "ItemSubject='{0}' MetadataSubject='{1}'", subject, metaSubject);
            }

            // Grade
            string grade = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_att_Grade']/val");
            string metaGrade = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:IntendedGrade", sXmlNs);
            if (string.IsNullOrEmpty(grade))
            {
                ReportError(it, ErrCat.Attribute, ErrSeverity.Tolerable, "Missing grade in item attributes (itm_att_Grade).");
                grade = metaGrade;
                if (string.IsNullOrEmpty(grade))
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Missing grade in item metadata.");
            }
            else
            {
                if (!string.Equals(grade, metaGrade, StringComparison.Ordinal))
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Grade mismatch between item and metadata.", "ItemGrade='{0}', MetadataGrade='{1}'", grade, metaGrade);
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
                        ReportError(it, ErrCat.Rubric, ErrSeverity.Degraded, "Machine rubric not found.", "Filename='{0}'", machineRubricFilename);
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
                            ReportError(it, ErrCat.Rubric, ErrSeverity.Severe, "Unexpected MC answer key attribute.", "itm_att_Answer Key='{0}'", answerKeyValue);
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
                            if (!validAnswer) ReportError(it, ErrCat.Rubric, ErrSeverity.Severe, "Unexpected MS answer attribute: '{0}'", answerKeyValue);
                        }
                        break;

                    case "EBSR":    // Evidence-based selected response
                        rubric = "Embedded";
                        usesMachineRubric = true;
                        metadataExpected = "Automatic with Key(s)";
                        if (answerKeyValue.Length != 1 || answerKeyValue[0] < 'A' || answerKeyValue[0] > 'Z')
                            ReportError(it, ErrCat.Rubric, ErrSeverity.Severe, "Unexpected EBSR answer key attribute.", "itm_att_Answer Key='{0}'", answerKeyValue);
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
                            ReportError(it, ErrCat.Rubric, ErrSeverity.Tolerable, "Unexpected answer key attribute.", "Value='{0}' Expected='{1}'", answerKeyValue, it.ItemType.ToUpper());
                        break;

                    case "er":          // Extended-Response
                    case "sa":          // Short Answer
                    case "wer":         // Writing Extended Response
                        metadataExpected = "HandScored";
                        if (!string.Equals(answerKeyValue, it.ItemType.ToUpper()))
                            ReportError(it, ErrCat.Rubric, ErrSeverity.Tolerable, "Unexpected answer key attribute.", "Value='{0}' Expected='{1}'", answerKeyValue, it.ItemType.ToUpper());
                        break;

                    default:
                        ReportError(it, ErrCat.Unsupported, ErrSeverity.Benign, "Validation of rubrics of this type are not supported.");
                        break;
                }

                // Check Scoring Engine metadata
                if (metadataExpected != null && !string.Equals(metadataScoringEngine, metadataExpected, StringComparison.Ordinal))
                {
                    if (string.Equals(metadataScoringEngine, metadataExpected, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportError(it, ErrCat.Metadata, ErrSeverity.Benign, "Capitalization error in ScoringEngine metadata.", "Found='{0}' Expected='{1}'", metadataScoringEngine, metadataExpected);
                    }
                    else
                    {
                        // If first word of rubric metadata is the same (e.g. both are "Automatic" or both are "HandScored") then error is benign, otherwise error is tolerable
                        if (string.Equals(metadataScoringEngine.FirstWord(), metadataExpected.FirstWord(), StringComparison.OrdinalIgnoreCase))
                        {
                            ReportError(it, ErrCat.Metadata, ErrSeverity.Benign, "Incorrect ScoringEngine metadata.", "Found='{0}' Expected='{1}'", metadataScoringEngine, metadataExpected);
                        }
                        else
                        {
                            ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Automatic/HandScored scoring metadata error.", "Found='{0}' Expected='{1}'", metadataScoringEngine, metadataExpected);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(machineRubricFilename) && !usesMachineRubric)
                    ReportError(it, ErrCat.Rubric, ErrSeverity.Benign, "Unexpected machine rubric found for HandScored item type.", "Filename='{1}'", machineRubricFilename);

                // Check for unreferenced machine rubrics
                foreach(FileInfo fi in it.DiItem.EnumerateFiles("*.qrx"))
                {
                    if (machineRubricFilename == null || !string.Equals(fi.Name, machineRubricFilename, StringComparison.OrdinalIgnoreCase))
                        ReportError(it, ErrCat.Rubric, ErrSeverity.Degraded, "Machine rubric file found but not referenced in <MachineRubric> element.", "Filename='{0}'", fi.Name);
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
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Degraded, "PerformanceTaskComponentItem metadata should be 'Y' or 'N'.", "Value='{0}'", meta);
                }
            }
            
            // Standard, Claim and Target
            string standard;
            string claim;
            string target;
            StandardFromMetadata(it, xmlMetadata, out standard, out claim, out target);
            if (string.IsNullOrEmpty(standard))
            {
                ReportError(it, ErrCat.Metadata, ErrSeverity.Degraded, "No PrimaryStandard specified in metadata.");
            }

            // ASL
            string asl = string.Empty;
            {
                bool aslFound = CheckForAttachment(it, xml, "ASL", "MP4");
                if (aslFound) asl = "MP4";
                if (!aslFound) ReportUnexpectedFiles(it, "ASL video", "item_{0}_ASL*", it.ItemId);

                bool aslInMetadata = string.Equals(xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AccessibilityTagsASLLanguage", sXmlNs), "Y", StringComparison.OrdinalIgnoreCase);
                if (aslInMetadata && !aslFound) ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Item metadata specifies ASL but no ASL in item.");
                if (!aslInMetadata && aslFound) ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Item has ASL but not indicated in the metadata.");
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
                    if (node == null) ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Item content includes language but metadata does not have a corresponding <Language> entry.", "Language='{0}'", language);
                }

                // Now, search the metadata for translations and make sure all exist in the content
                foreach(XmlElement xmlEle in xmlMetadata.SelectNodes("metadata/sa:smarterAppMetadata/sa:Language", sXmlNs))
                {
                    string language = xmlEle.InnerText;
                    if (!languages.Contains(language))
                    {
                        ReportError(it, ErrCat.Metadata, ErrSeverity.Degraded, "Item metadata indicates language but item content does not include that language.", "Language='{0}'", language);
                    }
                }
            }

            // Folder,it.ItemId,it.ItemType,Subject,Grade,Rubric,AsmtType,Standard,Claim,Target,ASL,BrailleEmbedded,BrailleFile,Translation
            mItemReport.WriteLine(string.Join(",", CsvEncode(it.Folder), CsvEncode(it.ItemId), CsvEncode(it.ItemType), CsvEncode(subject), CsvEncode(grade), CsvEncode(rubric), CsvEncode(assessmentType), CsvEncode(standard), CsvEncodeExcel(claim), CsvEncodeExcel(target), CsvEncode(asl), CsvEncode(brailleEmbedded), CsvEncode(brailleFile), CsvEncode(translation)));

            // === Tabulation is complete, check for other errors

            // Points
            {
                string answerKeyValue = string.Empty;
                string itemPoint = xml.XpEval("itemrelease/item/attriblist/attrib[@attid='itm_att_Item Point']/val");
                if (itemPoint == null) ReportError(it, ErrCat.Item, ErrSeverity.Degraded, "Item Point attribute not found.");
                int points;
                if (!int.TryParse(itemPoint.FirstWord(), out points))
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Item Point attribute is not integer.", "itm_att_Item Point='{0}'", itemPoint);
                }
                else
                {
                    // See if matches MaximumNumberOfPoints (defined as optional in metadata)
                    string metaPoint = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:MaximumNumberOfPoints", sXmlNs);
                    if (metaPoint == null)
                    {
                        ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "MaximumNumberOfPoints not found in metadata.");
                    }
                    else
                    {
                        int mpoints;
                        if (!int.TryParse(metaPoint, out mpoints))
                        {
                            ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Metadata MaximumNumberOfPoints value is not integer.", "MaximumNumberOfPoints='{0}'", metaPoint);
                        }
                        else if (mpoints != points)
                        {
                            ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Metadata MaximumNumberOfPoints does not match item point attribute.", "MaximumNumberOfPoints='{0}' itm_att_Item Point='{0}'", mpoints, points);
                        }
                    }

                    // See if matches ScorePoints (defined as optional in metadata)
                    string scorePoints = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:ScorePoints", sXmlNs);
                    if (scorePoints == null)
                    {
                        ReportError(it, ErrCat.Metadata, ErrSeverity.Benign, "ScorePoints not found in metadata.");
                    }
                    else
                    {
                        scorePoints = scorePoints.Trim();
                        if (scorePoints[0] == '"')
                            scorePoints = scorePoints.Substring(1);
                        else
                            ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "ScorePoints value missing leading quote.");
                        if (scorePoints[scorePoints.Length-1] == '"')
                            scorePoints = scorePoints.Substring(0, scorePoints.Length-1);
                        else
                            ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "ScorePoints value missing trailing quote.");

                        int maxspoints = -1;
                        int minspoints = 100000;
                        foreach (string sp in scorePoints.Split(','))
                        {
                            int spoints;
                            if (!int.TryParse(sp.Trim(), out spoints))
                            {
                                ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Metadata ScorePoints value is not integer.", "ScorePoints='{0}' value='{1}'", scorePoints, sp);
                            }
                            else if (spoints < 0 || spoints > points)
                            {
                                ReportError(it, ErrCat.Metadata, ErrSeverity.Severe, "Metadata ScorePoints value is out of range (0 - {1}).", "ScorePoints='{0}' value='{1}' min='0' max='{2}'", scorePoints, spoints, points);
                            }
                            else
                            {
                                if (maxspoints < spoints) 
                                {
                                    maxspoints = spoints;
                                }
                                else
                                {
                                    ReportError(it, ErrCat.Metadata, ErrSeverity.Benign, "Metadata ScorePoints are not in ascending order.", "ScorePoints='{0}'", scorePoints);
                                }
                                if (minspoints > spoints) minspoints = spoints;
                            }
                        }
                        if (minspoints > 0) ReportError(it, ErrCat.Metadata, ErrSeverity.Benign, "Metadata ScorePoints doesn't include a zero score", "ScorePoints='{0}'", scorePoints);
                        if (maxspoints < points) ReportError(it, ErrCat.Metadata, ErrSeverity.Degraded, "Metadata ScorePoints doesn't include a maximum score.", "ScorePoints='{0}' max='{1}'", scorePoints, points);
                    }
                }
            }

            // Performance Task Details
            if (string.Equals(assessmentType, "PT", StringComparison.OrdinalIgnoreCase))
            {
                // PtSequence
                int seq;
                string ptSequence = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PtSequence", sXmlNs);
                if (ptSequence == null)
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Degraded, "Metadata for PT item is missing <PtSequence> element.");
                else if (!int.TryParse(ptSequence.Trim(), out seq))
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Degraded, "Metadata <PtSequence> is not an integer.", "PtSequence='{0}'", ptSequence);

                // PtWritingType Metadata (defined as optional in metadata but we'll still report a benign error if it's not on PT WER items)
                if (string.Equals(it.ItemType, "wer", StringComparison.OrdinalIgnoreCase))
                {
                    string ptWritingType = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PtWritingType", sXmlNs);
                    if (ptWritingType == null)
                    {
                        ReportError(it, ErrCat.Metadata, ErrSeverity.Benign, "Metadata for PT item is missing <PtWritingType> element.");
                    }
                    else
                    {
                        ptWritingType = ptWritingType.Trim();
                        if (!sValidWritingTypes.Contains(ptWritingType))
                        {
                            // Fix capitalization
                            string normalized = string.Concat(ptWritingType.Substring(0, 1).ToUpper(), ptWritingType.Substring(1).ToLower());

                            // Report according to type of error
                            if (!sValidWritingTypes.Contains(normalized))
                                ReportError(it, ErrCat.Metadata, ErrSeverity.Benign, "PtWritingType metadata has invalid value.", "PtWritingType='{0}'", ptWritingType);
                            else
                                ReportError(it, ErrCat.Metadata, ErrSeverity.Benign, "Capitalization error in PtWritingType metadata.", "PtWritingType='{0}' expected='{1}'", ptWritingType, normalized);
                        }
                    }
                }

                // Stimulus (Passage) ID
                string stimId = xml.XpEval("itemrelease/item/attriblist/attrib[@attid='stm_pass_id']/val");
                if (stimId == null)
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Severe, "PT Item missing associated passage ID (stm_pass_id).");
                }
                else
                {
                    string metaStimId = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:AssociatedStimulus", sXmlNs);
                    if (metaStimId == null)
                    {
                        ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "PT Item metatadata missing AssociatedStimulus.");
                    }
                    else if (!string.Equals(stimId, metaStimId, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "PT Item passage ID doesn't match metadata AssociatedStimulus.", "Item stm_pass_id='{0}' Metadata AssociatedStimulus='{1}'", stimId, metaStimId);
                    }

                    // Stimulus dependencies are not recorded in the manifest therefore this code isn't useful
                    // Retaining because the ResourceId lookup code may be useful for other things in the future.
                    // For example, manifest DOES record dependencies between items and tutorials. This can be
                    // validated in the future.
                    /*

                    // Look for the stimulus
                    string stimulusFilename = Path.Combine(mPackagePath, string.Format("Stimuli\\stim-200-{0}\\stim-200-{0}.xml", stimId));
                    if (!File.Exists(stimulusFilename))
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Severe, "PT item stimulus not found.", "StimulusId='{0}'", stimId);
                    }

                    // Look up item in manifest
                    string itemResourceId = null;
                    if (!mFilenameToResourceId.TryGetValue(NormalizeFilenameInManifest(it.ItemFilename), out itemResourceId))
                    {
                        ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, "Item not found in manifest.");
                    }

                    // Look up stimulus in manifest
                    string stimulusResourceId = null;
                    if (!mFilenameToResourceId.TryGetValue(NormalizeFilenameInManifest(stimulusFilename), out stimulusResourceId))
                    {
                        ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, "Stimulus not found in manifest.", "StimulusId='{0}'", stimId);
                    }

                    // Check for dependency on stimulus in manifest
                    if (!string.IsNullOrEmpty(itemResourceId) && !string.IsNullOrEmpty(stimulusResourceId))
                    {
                        if (!mResourceDependencies.Contains(ToDependsOnString(itemResourceId, itemResourceId)))
                            ReportError(it, ErrCat.Manifest, ErrSeverity.Benign, "Manifest does not record dependency between item and stimulus.", "ItemResourceId='{0}' StimulusResourceId='{1}'", itemResourceId, stimulusResourceId);
                        else
                            ReportError(it, ErrCat.Manifest, ErrSeverity.Benign, "Manifest records dependency between item and stimulus.", "ItemResourceId='{0}' StimulusResourceId='{1}'", itemResourceId, stimulusResourceId);
                    }
                    */
                }
            } // if Performance Task

            // WordList Reference
            foreach (XmlElement xmlRes in xml.SelectNodes("itemrelease/item/resourceslist/resource[@type='wordList']"))
            {
                string witId = xmlRes.GetAttribute("id");
                if (string.IsNullOrEmpty(witId))
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Degraded, "Item has blank wordList id.");
                }
                else
                {
                    string otherItemId;
                    if (mWitIdToItemId.TryGetValue(witId, out otherItemId))
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Benign, "Multiple items reference the same wordlist.", "OtherItemId='{0}'", otherItemId);
                    }
                    else
                    {
                        mWitIdToItemId.Add(witId, it.ItemId);
                    }
                }
            }

        } // TablulateInteraction

        bool CheckForAttachment(ItemContext it, XmlDocument xml, string attachType, string expectedExtension)
        {
            XmlElement xmlEle = xml.SelectSingleNode(string.Concat("itemrelease/item/content/attachmentlist/attachment[@type='", attachType, "']")) as XmlElement;
            if (xmlEle != null)
            {
                string filename = xmlEle.GetAttribute("file");
                if (string.IsNullOrEmpty(filename))
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Attachment missing file attribute.", "attachType='{0}'", attachType);
                    return false;
                }
                if (!File.Exists(Path.Combine(it.DiItem.FullName, filename)))
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Tolerable, "Dangling reference to attached file that does not exist.", "attachType='{0}' Filename='{1}'", attachType, filename);
                    return false;
                }

                string extension = Path.GetExtension(filename);
                if (extension.Length > 0) extension = extension.Substring(1); // Strip leading "."
                if (!string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase))
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Benign, "Unexpected extension for attached file.", "attachType='{0}' extension='{1}' expected='{2}' filename='{3}'", attachType, extension, expectedExtension, filename);
                }
                return true;
            }
            return false;
        }

        void ReportUnexpectedFiles(ItemContext it, string fileType, string pattern, params object[] args)
        {
            foreach (FileInfo file in it.DiItem.GetFiles(string.Format(pattern, args)))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Benign, "Unreferenced file found.", "fileType='{0}', filename='{1}'", fileType, file.Name);
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
                        ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Standard reference has invalid value.", "Publication='{0}' StandardId='{1}", coding.Publication, std);
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

        private void TabulateWordList(ItemContext it)
        {
            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            xml.Load(Path.Combine(it.DiItem.FullName, it.DiItem.Name + ".xml"));

            // Sanity check
            if (!string.Equals(xml.XpEval("itemrelease/item/@id"), it.ItemId)) throw new InvalidDataException("Item id mismatch on pass 2");

            // Find the referring item ID
            string referringId;
            if (!mWitIdToItemId.TryGetValue(it.ItemId, out referringId))
            {
                ReportError(it, ErrCat.Wordlist, ErrSeverity.Benign, "Wordlist is not referenced by any item.");
                referringId = string.Empty;
            }

            // Enumerate up the terms in the wordlist
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

                    // Folder,WIT_ID,ItemId,Index,Term,Language,Length
                    mTextGlossaryReport.WriteLine(string.Join(",", it.Folder, CsvEncode(it.ItemId), CsvEncode(referringId), index.ToString(), CsvEncodeExcel(term), CsvEncode(language), htmlNode.InnerXml.Length.ToString()));
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
                            ReportError(it, ErrCat.Wordlist, ErrSeverity.Benign, "Audio file with no matching glossary term.", "filename='{0}' index='{1}'", fi.Name, index);
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

                        // Folder,WIT_ID,ItemId,Index,Term,Language,Encoding,Size
                        mAudioGlossaryReport.WriteLine(String.Join(",", it.Folder, CsvEncode(it.ItemId), CsvEncode(referringId), index.ToString(), CsvEncodeExcel(term), CsvEncode(language), CsvEncode(extension), fi.Length.ToString()));
                    }
                    else
                    {
                        ReportError(it, ErrCat.Unsupported, ErrSeverity.Degraded, "Audio Glossary Filename in unrecognized format: {0}", fi.Name);
                    }
                }
            }
        }

        void VerifyWordlistReferences()
        {
            foreach (var entry in mWitIdToItemId)
            {
                ItemContext it;
                if (!mIdToItemContext.TryGetValue(entry.Key, out it) || !string.Equals(it.ItemType, "wordList", StringComparison.Ordinal))
                {
                    if (mIdToItemContext.TryGetValue(entry.Value, out it))
                        ReportError(it, ErrCat.Item, ErrSeverity.Degraded, "Item references nonexistent wordlist.", "WIT_ID='{0}'", entry.Key);
                    else
                        throw new ApplicationException("Item id not in index!");
                }
            }
        }

        void ValidateManifest(string packageFolderPath)
        {
            // Prep an itemcontext for reporting errors
            ItemContext it = new ItemContext(this, new DirectoryInfo(packageFolderPath), null, null);

            // Load the manifest
            XmlDocument xmlManifest = new XmlDocument(sXmlNt);
            xmlManifest.Load(Path.Combine(packageFolderPath, "imsmanifest.xml"));

            // Keep track of every resource id mentioned in the manifest
            HashSet<string> ids = new HashSet<string>();

            // Enumerate all resources in the manifest
            foreach (XmlElement xmlRes in xmlManifest.SelectNodes("ims:manifest/ims:resources/ims:resource", sXmlNs))
            {
                string id = xmlRes.GetAttribute("identifier");
                if (string.IsNullOrEmpty(id))
                    ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, "Resource in manifest is missing id.", "Filename='{0}'", xmlRes.XpEvalE("ims:file/@href", sXmlNs));
                string filename = xmlRes.XpEval("ims:file/@href", sXmlNs);
                if (string.IsNullOrEmpty(filename))
                {
                    ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, "Resource specified in manifest has no filename.", "ResourceId='{0}'", id);
                }
                else if (!File.Exists(Path.Combine(packageFolderPath, filename)))
                {
                    ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, "Resource specified in manifest does not exist.", "ResourceId='{0}' Filename='{1}'", id, filename);
                }

                if (ids.Contains(id))
                {
                    ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, "Resource listed multiple times in manifest.", "ResourceId='{0}'", id);
                }
                else
                {
                    ids.Add(id);
                }

                // Normalize the filename
                filename = filename.ToLower().Replace('\\', '/');
                if (mFilenameToResourceId.ContainsKey(filename))
                {
                    ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, "File listed multiple times in manifest.", "ResourceId='{0}' Filename='{1}'", id, filename);
                }
                else
                {
                    mFilenameToResourceId.Add(filename, id);
                }

                // Index any dependencies
                foreach (XmlElement xmlDep in xmlRes.SelectNodes("ims:dependency", sXmlNs))
                {
                    string dependsOnId = xmlDep.GetAttribute("identifierref");
                    if (string.IsNullOrEmpty(dependsOnId))
                    {
                        ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, "Dependency in manifest is missing identifierref attribute.", "ResourceId='{0}'", id);
                    }
                    else
                    {
                        string dependency = ToDependsOnString(id, dependsOnId);
                        if (mResourceDependencies.Contains(dependency))
                        {
                            ReportError(it, ErrCat.Manifest, ErrSeverity.Benign, "Dependency in manifest repeated multiple times.", "ResourceId='{0}' DependsOnId='{1}'", id, dependsOnId);
                        }
                        else
                        {
                            mResourceDependencies.Add(dependency);
                         }
                    }

                }
            }

            // Enumerate all files and check for them in the manifest
            {
                DirectoryInfo diPackage = new DirectoryInfo(packageFolderPath);
                foreach (DirectoryInfo di in diPackage.GetDirectories())
                {
                    ValidateDirectoryInManifest(it, di);
                }
            }
        }

        // Recursively check that files exist in the manifest
        void ValidateDirectoryInManifest(ItemContext it, DirectoryInfo di)
        {
            // See if this is an item or stimulus directory
            string itemFileName = null;
            string itemId = null;
            if (
                (di.Name.StartsWith("item-", StringComparison.OrdinalIgnoreCase) || di.Name.StartsWith("stim-", StringComparison.OrdinalIgnoreCase))
                && (File.Exists(Path.Combine(di.FullName, string.Concat(di.Name, ".xml")))))
            {
                itemFileName = NormalizeFilenameInManifest(Path.Combine(di.FullName, string.Concat(di.Name, ".xml")));

                if (!mFilenameToResourceId.TryGetValue(itemFileName, out itemId))
                {
                    ReportError(it, ErrCat.Manifest, ErrSeverity.Degraded, "Item does not appear in the manifest.", "ItemId='{0}'", di.Name);
                    itemFileName = null;
                    itemId = null;
                }
            }

            foreach (FileInfo fi in di.EnumerateFiles())
            {
                string filename = NormalizeFilenameInManifest(fi.FullName);

                string resourceId;
                if (!mFilenameToResourceId.TryGetValue(filename, out resourceId))
                {
                    ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, "Resource does not appear in the manifest.", "Filename='{0}'", filename);
                }

                // If in an item, see if dependency is expressed
                else if (itemId != null && !string.Equals(itemId, resourceId, StringComparison.Ordinal))
                {
                    // Check for dependency
                    if (!mResourceDependencies.Contains(ToDependsOnString(itemId, resourceId)))
                        ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, "Manifest does not express resource dependency.", "ResourceId='{0}' DependesOnId='{1}'", itemId, resourceId);
                }
            }

            // Recurse
            foreach(DirectoryInfo diSub in di.GetDirectories())
            {
                ValidateDirectoryInManifest(it, diSub);
            }
        }

        string NormalizeFilenameInManifest(string filename)
        {
            int ppLen = mPackagePath.Length;
            if (filename.Length > ppLen && filename.StartsWith(mPackagePath, StringComparison.OrdinalIgnoreCase) && filename[ppLen] == '\\')
                filename = filename.Substring(ppLen+1);
            filename = filename.ToLower().Replace('\\', '/');
            return filename;
        }

        static string ToDependsOnString(string itemId, string dependsOnId)
        {
            return string.Concat(itemId, "~", dependsOnId);
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
            Item,
            Wordlist,
            Manifest
        }

        // Error Severity
        enum ErrSeverity
        {
            Severe = 4,
            Degraded = 3,
            Tolerable = 2,
            Benign = 1
            // NotAnError would be zero if it were included
        }


        void ReportError(ItemContext it, ErrCat category, ErrSeverity severity, string msg, string detail, params object[] args)
        {
            if (mErrorReport == null)
            {
                mErrorReport = new StreamWriter(Path.Combine(mRootPath, cErrorReportFn), false, sUtf8NoBomEncoding);
                mErrorReport.WriteLine("Folder,ItemId,ItemType,Category,Severity,ErrorMessage,Detail");
            }

            if (string.IsNullOrEmpty(msg))
                msg = string.Empty;
            else
                msg = CsvEncode(msg);

            if (string.IsNullOrEmpty(detail))
                detail = string.Empty;
            else
                detail = CsvEncode(string.Format(detail, args));

            // "Folder,ItemId,ItemType,Category,ErrorMessage"
            mErrorReport.WriteLine(string.Join(",", CsvEncode(it.Folder), CsvEncode(it.ItemId), CsvEncode(it.ItemType), category.ToString(), severity.ToString(), msg, detail));

            ++mErrorCount;
        }

        void ReportError(ItemContext it, ErrCat category, ErrSeverity severity, string msg)
        {
            ReportError(it, category, severity, msg, null);
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
            public string ItemFilename
            {
                get
                {
                    return Path.Combine(DiItem.FullName, DiItem.Name + ".xml");
                }
            }
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

        static readonly char[] cWhitespace = new char[] { ' ', '\t', '\r', '\n' };
        public static string FirstWord(this string str)
        {
            str = str.Trim();
            int space = str.IndexOfAny(cWhitespace);
            return (space > 0) ? str.Substring(0, space) : str;
        }
    }
}
