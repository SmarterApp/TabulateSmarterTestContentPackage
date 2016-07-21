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
        const string cImsManifest = "imsmanifest.xml";
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

        const string cStimulusInteractionType = "Stimulus";
        const string cTutorialInteractionType = "TUT";

        static readonly HashSet<string> sValidWritingTypes = new HashSet<string>(
            new string[] {
                "Explanatory",
                "Opinion",
                "Informative",
                "Argumentative",
                "Narrative"
            });

        static readonly HashSet<string> sValidClaims = new HashSet<string>(
            new string[] {
                "1",
                "1-LT",
                "1-IT",
                "2",
                "2-W",
                "3",
                "3-L",
                "3-S",
                "4",
                "4-CR"
            });

        // Filenames
        const string cSummaryReportFn = "SummaryReport.txt";
        const string cItemReportFn = "ItemReport.csv";
        const string cStimulusReportFn = "StimulusReport.csv";
        const string cWordlistReportFn = "WordlistReport.csv";
        const string cGlossaryReportFn = "GlossaryReport.csv";
        const string cErrorReportFn = "ErrorReport.csv";

        int mErrorCount = 0;
        int mItemCount = 0;
        int mWordlistCount = 0;
        int mGlossaryTermCount = 0;
        int mGlossaryM4aCount = 0;
        int mGlossaryOggCount = 0;
        Dictionary<string, int> mTypeCounts = new Dictionary<string, int>();
        Dictionary<string, int> mTermCounts = new Dictionary<string, int>();
        Dictionary<string, int> mTranslationCounts = new Dictionary<string, int>();
        Dictionary<string, int> mM4aTranslationCounts = new Dictionary<string, int>();
        Dictionary<string, int> mOggTranslationCounts = new Dictionary<string, int>();
        Dictionary<string, int> mRubricCounts = new Dictionary<string, int>();

        // Per Package variables
        string mPackageName;
        FileFolder mPackageFolder;
        Dictionary<string, string> mFilenameToResourceId = new Dictionary<string, string>();
        HashSet<string> mResourceDependencies = new HashSet<string>();
        Dictionary<string, int> mWordlistRefCounts = new Dictionary<string, int>();   // Reference count for wordlist IDs
        Dictionary<string, ItemContext> mIdToItemContext = new Dictionary<string, ItemContext>();
        LinkedList<ItemContext> mStimContexts = new LinkedList<ItemContext>();

        // Per report variables
        TextWriter mItemReport;
        TextWriter mStimulusReport;
        TextWriter mWordlistReport;
        TextWriter mGlossaryReport;
        string mErrorReportPath;
        TextWriter mErrorReport;
        string mSummaryReportPath;

        // Tabulate a package in the specified directory
        public void TabulateOne(string path)
        {
            try
            {
                if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    FileInfo fi = new FileInfo(path);
                    Console.WriteLine("Tabulating " + fi.Name);
                    if (!fi.Exists) throw new FileNotFoundException(string.Format("Package '{0}' file not found!", path));

                    string filepath = fi.FullName;
                    Initialize(filepath.Substring(0, filepath.Length - 4));
                    using (ZipFileTree tree = new ZipFileTree(filepath))
                    {
                        TabulatePackage(string.Empty, tree);
                    }
                }
                else
                {
                    string folderpath = Path.GetFullPath(path);
                    Console.WriteLine("Tabulating " + Path.GetFileName(folderpath));
                    if (!Directory.Exists(folderpath)) throw new FileNotFoundException(string.Format("Package '{0}' directory not found!", folderpath));

                    Initialize(folderpath);
                    TabulatePackage(string.Empty, new FsFolder(folderpath));
                }
            }
            finally
            {
                Conclude();
            }
        }

        // Individually tabulate each package in subdirectories
        public void TabulateEach(string rootPath)
        {
            DirectoryInfo diRoot = new DirectoryInfo(rootPath);

            // Tablulate unpacked packages
            foreach (DirectoryInfo diPackageFolder in diRoot.GetDirectories())
            {
                if (File.Exists(Path.Combine(diPackageFolder.FullName, cImsManifest)))
                {
                    try
                    {
                        Console.WriteLine("Tabulating " + diPackageFolder.Name);
                        Initialize(diPackageFolder.FullName);
                        TabulatePackage(string.Empty, new FsFolder(diPackageFolder.FullName));
                    }
                    finally
                    {
                        Conclude();
                    }
                }
            }

            // Tabulate zipped packages
            foreach (FileInfo fiPackageFile in diRoot.GetFiles("*.zip"))
            {
                string filepath = fiPackageFile.FullName;
                Console.WriteLine("Opening " + fiPackageFile.Name);
                using (ZipFileTree tree = new ZipFileTree(filepath))
                {
                    if (tree.FileExists(cImsManifest))
                    {
                        try
                        {
                            Console.WriteLine("Tabulating " + fiPackageFile.Name);
                            Initialize(filepath.Substring(0, filepath.Length - 4));
                            TabulatePackage(string.Empty, tree);
                        }
                        finally
                        {
                            Conclude();
                        }
                    }
                }
            }
        }

        // Tabulate packages in subdirectories and aggregate the results
        public void TabulateAggregate(string rootPath)
        {
            DirectoryInfo diRoot = new DirectoryInfo(rootPath);
            try
            {
                Initialize(Path.Combine(rootPath, "Aggregate"));

                // Tabulate unpacked packages
                foreach (DirectoryInfo diPackageFolder in diRoot.GetDirectories())
                {
                    if (File.Exists(Path.Combine(diPackageFolder.FullName, cImsManifest)))
                    {
                        Console.WriteLine("Tabulating " + diPackageFolder.Name);
                        TabulatePackage(diPackageFolder.Name, new FsFolder(diPackageFolder.FullName));
                    }
                }

                // Tabulate packed packages
                foreach (FileInfo fiPackageFile in diRoot.GetFiles("*.zip"))
                {
                    string filepath = fiPackageFile.FullName;
                    Console.WriteLine("Opening " + fiPackageFile.Name);
                    using (ZipFileTree tree = new ZipFileTree(filepath))
                    {
                        if (tree.FileExists(cImsManifest))
                        {
                            Console.WriteLine("Tabulating " + fiPackageFile.Name);
                            string packageName = fiPackageFile.Name;
                            packageName = packageName.Substring(0, packageName.Length - 4) + "/";
                            TabulatePackage(packageName, tree);
                        }
                    }
                }

            }
            finally
            {
                Conclude();
            }
        }

        // Initialize all files and collections for a tabulation run
        private void Initialize(string reportPrefix)
        {
            reportPrefix = string.Concat(reportPrefix, "_");
            mErrorReportPath = string.Concat(reportPrefix, cErrorReportFn);
            if (File.Exists(mErrorReportPath)) File.Delete(mErrorReportPath);

            mItemReport = new StreamWriter(string.Concat(reportPrefix, cItemReportFn));
            mItemReport.WriteLine("Folder,ItemId,ItemType,Version,Subject,Grade,Rubric,AsmtType,Standard,Claim,Target,WordlistId,ASL,BrailleType,Translation,Media,Size");

            mStimulusReport = new StreamWriter(string.Concat(reportPrefix, cStimulusReportFn));
            mStimulusReport.WriteLine("Folder,StimulusId,Version,Subject,WordlistId,ASL,BrailleType,Translation,Media,Size,WordCount");

            mWordlistReport = new StreamWriter(string.Concat(reportPrefix, cWordlistReportFn));
            mWordlistReport.WriteLine("Folder,WIT_ID,RefCount,TermCount,MaxGloss,MinGloss,AvgGloss");

            mGlossaryReport = new StreamWriter(string.Concat(reportPrefix, cGlossaryReportFn), false, sUtf8NoBomEncoding);
            if (Program.gValidationOptions.IsEnabled("gtr"))
                mGlossaryReport.WriteLine("Folder,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize,Text");
            else
                mGlossaryReport.WriteLine("Folder,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize");

            mSummaryReportPath = string.Concat(reportPrefix, cSummaryReportFn);
            if (File.Exists(mSummaryReportPath)) File.Delete(mSummaryReportPath);

            mErrorCount = 0;
            mItemCount = 0;
            mWordlistCount = 0;
            mGlossaryTermCount = 0;
            mGlossaryM4aCount = 0;
            mGlossaryOggCount = 0;

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
                    Console.WriteLine("{0} Errors reported.", mErrorCount);
                    Console.WriteLine();
                }
            }
            finally
            {
                if (mStimulusReport != null)
                {
                    mStimulusReport.Dispose();
                    mStimulusReport = null;
                }
                if (mItemReport != null)
                {
                    mItemReport.Dispose();
                    mItemReport = null;
                }
                if (mGlossaryReport != null)
                {
                    mGlossaryReport.Dispose();
                    mGlossaryReport = null;
                }
                if (mErrorReport != null)
                {
                    mErrorReport.Dispose();
                    mErrorReport = null;
                }
            }
        }

        public void TabulatePackage(string packageName, FileFolder packageFolder)
        {
            mPackageName = packageName;

            if (!packageFolder.FileExists(cImsManifest)) throw new ArgumentException("Not a valid content package path. File imsmanifest.xml not found!");

            // Initialize package-specific collections
            mPackageFolder = packageFolder;
            mFilenameToResourceId.Clear();
            mResourceDependencies.Clear();
            mWordlistRefCounts.Clear();
            mIdToItemContext.Clear();
            mStimContexts.Clear();

            // Validate manifest
            try
            {
                ValidateManifest();
            }
            catch (Exception err)
            {
                ReportError(new ItemContext(this, packageFolder, null, null), ErrCat.Exception, ErrSeverity.Severe, err.ToString());
            }

            // First pass through items
            FileFolder ffItems;           
            if (packageFolder.TryGetFolder("Items", out ffItems))
            {
                foreach (FileFolder ffItem in ffItems.Folders)
                {
                    try
                    {
                        TabulateItem_Pass1(ffItem);
                    }
                    catch (Exception err)
                    {
                        ReportError(new ItemContext(this, ffItem, null, null), ErrCat.Exception, ErrSeverity.Severe, err.ToString());
                    }
                }
            }

            // First pass through stimuli
            if (packageFolder.TryGetFolder("Stimuli", out ffItems))
            {
                foreach (FileFolder ffItem in ffItems.Folders)
                {
                    try
                    {
                        TabulateStim_Pass1(ffItem);
                    }
                    catch (Exception err)
                    {
                        ReportError(new ItemContext(this, ffItem, null, null), ErrCat.Exception, ErrSeverity.Severe, err.ToString());
                    }
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

            // Second pass through stimuli
            foreach (ItemContext it in mStimContexts)
            {
                try
                {
                    TabulateItem_Pass2(it);
                }
                catch (Exception err)
                {
                    ReportError(it, ErrCat.Exception, ErrSeverity.Severe, err.ToString());
                }
            }
        }

        private void TabulateItem_Pass1(FileFolder ffItem)
        {
            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(ffItem, ffItem.Name + ".xml", xml))
            {
                ReportError(new ItemContext(this, ffItem, null, null), ErrCat.Item, ErrSeverity.Severe, "Item folder missing item file.");
                return;
            }

            // Get the details
            string itemType = xml.XpEval("itemrelease/item/@format");
            if (itemType == null) itemType = xml.XpEval("itemrelease/item/@type");
            if (itemType == null) throw new InvalidDataException("Item type not found");
            string itemId = xml.XpEval("itemrelease/item/@id");
            if (string.IsNullOrEmpty(itemId)) throw new InvalidDataException("Item id not found");
            string bankKey = xml.XpEvalE("itemrelease/item/@bankkey");

            // Add to the item count and the type count
            ++mItemCount;
            mTypeCounts.Increment(itemType);

            // Create and save the item context
            ItemContext it = new ItemContext(this, ffItem, itemId, itemType);
            if (mIdToItemContext.ContainsKey(itemId))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Multiple items with the same ID.");
            }
            else
            {
                mIdToItemContext.Add(itemId, it);
            }

            // Check for filename match
            if (!ffItem.Name.Equals(string.Format("item-{0}-{1}", bankKey, itemId), StringComparison.OrdinalIgnoreCase))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Item ID doesn't match file/folder name", "bankKey='{0}' itemId='{1}' foldername='{2}'", bankKey, itemId, ffItem);
            }

            // count wordlist reference
            CountWordlistReferences(it, xml);
        }

        private void TabulateStim_Pass1(FileFolder ffItem)
        {
            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(ffItem, ffItem.Name + ".xml", xml))
            {
                ReportError(new ItemContext(this, ffItem, null, null), ErrCat.Item, ErrSeverity.Severe, "Stimulus folder missing stimulus file.");
                return;
            }

            // See if passage
            XmlElement xmlPassage = xml.SelectSingleNode("itemrelease/passage") as XmlElement;
            if (xmlPassage == null) throw new InvalidDataException("Stimulus does not have passage xml.");

            string itemType = "pass";
            string itemId = xmlPassage.GetAttribute("id");
            if (string.IsNullOrEmpty(itemId)) throw new InvalidDataException("Item id not found");
            string bankKey = xmlPassage.GetAttribute("bankkey");
            if (bankKey == null) bankKey = string.Empty;

            // Add to the item count and the type count
            ++mItemCount;
            mTypeCounts.Increment(itemType);

            // Create and save the stimulus context
            ItemContext it = new ItemContext(this, ffItem, itemId, itemType);
            if (mIdToItemContext.ContainsKey(itemId))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Multiple items with the same ID.");
            }
            else
            {
                mStimContexts.AddLast(it);
            }

            // Check for filename match
            if (!ffItem.Name.Equals(string.Format("stim-{0}-{1}", bankKey, itemId), StringComparison.OrdinalIgnoreCase))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Stimulus ID doesn't match file/folder name", "bankKey='{0}' itemId='{1}' foldername='{2}'", bankKey, itemId, ffItem);
            }

            // count wordlist reference
            CountWordlistReferences(it, xml);
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
                case "sa":          // Short Answer
                case "ti":          // Table Interaction
                case "wer":         // Writing Extended Response
                    TabulateInteraction(it);
                    break;

                case "nl":          // Natural Language
                case "SIM":         // Simulation
                    ReportError(it, ErrCat.Unsupported, ErrSeverity.Severe, "Item type is not fully supported by the open source TDS.", "itemType='{0}'", it.ItemType);
                    TabulateInteraction(it);
                    break;

                case "wordList":    // Word List (Glossary)
                    TabulateWordList(it);
                    break;

                case "pass":        // Passage
                    TabulatePassage(it);
                    break;

                case "tut":         // Tutorial
                    TabulateTutorial(it);
                    break;

                default:
                    ReportError(it, ErrCat.Unsupported, ErrSeverity.Severe, "Unexpected item type: " + it.ItemType);
                    break;
            }
        }

        void TabulateInteraction(ItemContext it)
        {
            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Item folder missing item file.");
                return;
            }

            // Load metadata
            XmlDocument xmlMetadata = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, "metadata.xml", xmlMetadata))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Item metadata file not found.");
            }

            // Check interaction type
            string metaItemType = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:InteractionType", sXmlNs);
            if (!string.Equals(metaItemType, it.ItemType.ToUpperInvariant(), StringComparison.Ordinal))
                ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Incorrect metadata <InteractionType>.", "InteractionType='{0}' Expected='{1}'", metaItemType, it.ItemType.ToUpperInvariant());

            // Get the version
            string version = xml.XpEvalE("itemrelease/item/@version");

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
            string grade = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_att_Grade']/val").Trim();
            string metaGrade = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:IntendedGrade", sXmlNs);
            if (string.IsNullOrEmpty(grade))
            {
                ReportError(it, ErrCat.Attribute, ErrSeverity.Tolerable, "Missing grade in item attributes (itm_att_Grade).");
                grade = metaGrade;
                if (string.IsNullOrEmpty(grade))
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Missing <IntendedGrade> in item metadata.");
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
                    machineRubricType = Path.GetExtension(machineRubricFilename).ToLowerInvariant();
                    if (machineRubricType.Length > 0) machineRubricType = machineRubricType.Substring(1);
                    if (!it.FfItem.FileExists(machineRubricFilename))
                        ReportError(it, ErrCat.Rubric, ErrSeverity.Degraded, "Machine rubric not found.", "Filename='{0}'", machineRubricFilename);
                }

                string metadataScoringEngine = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:ScoringEngine", sXmlNs);

                // Count the rubric types
                mRubricCounts.Increment(string.Concat(it.ItemType, " '", xmlEle.XpEvalE("val"), "' ", machineRubricType));

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
                            foreach (string answer in parts)
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
                        if (!string.Equals(answerKeyValue, it.ItemType.ToUpperInvariant()))
                            ReportError(it, ErrCat.Rubric, ErrSeverity.Tolerable, "Unexpected answer key attribute.", "Value='{0}' Expected='{1}'", answerKeyValue, it.ItemType.ToUpperInvariant());
                        break;

                    case "er":          // Extended-Response
                    case "sa":          // Short Answer
                    case "wer":         // Writing Extended Response
                        metadataExpected = "HandScored";
                        if (!string.Equals(answerKeyValue, it.ItemType.ToUpperInvariant()))
                            ReportError(it, ErrCat.Rubric, ErrSeverity.Tolerable, "Unexpected answer key attribute.", "Value='{0}' Expected='{1}'", answerKeyValue, it.ItemType.ToUpperInvariant());
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
                    ReportError(it, ErrCat.Rubric, ErrSeverity.Benign, "Unexpected machine rubric found for HandScored item type.", "Filename='{0}'", machineRubricFilename);

                // Check for unreferenced machine rubrics
                foreach (FileFile fi in it.FfItem.Files)
                {
                    if (string.Equals(fi.Extension, ".qrx", StringComparison.OrdinalIgnoreCase)
                        && (machineRubricFilename == null || !string.Equals(fi.Name, machineRubricFilename, StringComparison.OrdinalIgnoreCase)))
                    {
                        ReportError(it, ErrCat.Rubric, ErrSeverity.Degraded, "Machine rubric file found but not referenced in <MachineRubric> element.", "Filename='{0}'", fi.Name);
                    }
                }
            }

            // AssessmentType (PT or CAT)
            string assessmentType;
            {
                string meta = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PerformanceTaskComponentItem", sXmlNs);
                if (meta == null || string.Equals(meta, "N", StringComparison.Ordinal)) assessmentType = "CAT";
                else if (string.Equals(meta, "Y", StringComparison.Ordinal)) assessmentType = "PT";
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

            // Validate claim
            if (!sValidClaims.Contains(claim))
                ReportError(it, ErrCat.Metadata, ErrSeverity.Degraded, "Unexpected claim value.", "Claim='{0}'", claim);

            // Validate target grade suffix (Generating lots of errors. Need to follow up.)
            {
                string[] parts = target.Split('-');
                if (parts.Length == 2 && !string.Equals(parts[1].Trim(), grade, StringComparison.OrdinalIgnoreCase))
                {
                    ReportError("tgs", it, ErrCat.Metadata, ErrSeverity.Tolerable, "Target suffix indicates a different grade from item attribute.", "ItemAttributeGrade='{0}' TargetSuffixGrade='{1}'", grade, parts[1]);
                }
            }

            // WordList ID
            string wordlistId = ValidateWordlistReferences(it, xml);

            // ASL
            string asl = string.Empty;
            {
                bool aslFound = CheckForAttachment(it, xml, "ASL", "MP4");
                if (aslFound) asl = "MP4";
                if (!aslFound) ReportUnexpectedFiles(it, "ASL video", "^item_{0}_ASL", it.ItemId);

                bool aslInMetadata = string.Equals(xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AccessibilityTagsASLLanguage", sXmlNs), "Y", StringComparison.OrdinalIgnoreCase);
                if (aslInMetadata && !aslFound) ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Item metadata specifies ASL but no ASL in item.");
                if (!aslInMetadata && aslFound) ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Item has ASL but not indicated in the metadata.");
            }

            // BrailleType
            string brailleType = GetBrailleType(it, xml, xmlMetadata);

            // Translation
            string translation = GetTranslation(it, xml, xmlMetadata);

            // Media
            string media = GetMedia(it, xml);

            // Size
            long size = GetItemSize(it);

            // Folder,ItemId,ItemType,Version,Subject,Grade,Rubric,AsmtType,Standard,Claim,Target,WordlistId,ASL,BrailleType,Translation,Media,Size
            mItemReport.WriteLine(string.Join(",", CsvEncode(it.Folder), CsvEncode(it.ItemId), CsvEncode(it.ItemType), CsvEncode(version), CsvEncode(subject), CsvEncode(grade), CsvEncode(rubric), CsvEncode(assessmentType), CsvEncode(standard), CsvEncodeExcel(claim), CsvEncodeExcel(target), CsvEncode(wordlistId), CsvEncode(asl), CsvEncode(brailleType), CsvEncode(translation), CsvEncode(media), size.ToString()));

            // === Tabulation is complete, check for other errors

            // Points
            {
                string answerKeyValue = string.Empty;
                string itemPoint = xml.XpEval("itemrelease/item/attriblist/attrib[@attid='itm_att_Item Point']/val");
                if (string.IsNullOrEmpty(itemPoint))
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Degraded, "Item Point attribute (item_att_Item Point) not found.");
                }
                else
                {
                    // Item Point attribute may have a suffix such as "pt", "pt.", " pt", " pts" and other variations.
                    // TODO: In seeking consistency, we may make this more picky in the future.
                    itemPoint = itemPoint.Trim();
                    if (!char.IsDigit(itemPoint[0]))
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Item Point attribute does not begin with an integer.", "itm_att_Item Point='{0}'", itemPoint);
                    }
                    else
                    {
                        int points = itemPoint.ParseLeadingInteger();

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
                            if (scorePoints[scorePoints.Length - 1] == '"')
                                scorePoints = scorePoints.Substring(0, scorePoints.Length - 1);
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
                                    ReportError(it, ErrCat.Metadata, ErrSeverity.Severe, "Metadata ScorePoints value is out of range.", "ScorePoints='{0}' value='{1}' min='0' max='{2}'", scorePoints, spoints, points);
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
                            string normalized = string.Concat(ptWritingType.Substring(0, 1).ToUpperInvariant(), ptWritingType.Substring(1).ToLowerInvariant());

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

                    // Get the bankKey
                    string bankKey = xml.XpEvalE("itemrelease/item/@bankkey");

                    // Look for the stimulus
                    string stimulusFilename = string.Format(@"Stimuli\stim-{1}-{0}\stim-{1}-{0}.xml", stimId, bankKey);
                    if (!mPackageFolder.FileExists(stimulusFilename))
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Severe, "PT item stimulus not found.", "StimulusId='{0}'", stimId);
                    }

                    // Make sure dependency is recorded in manifest
                    CheckDependencyInManifest(it, stimulusFilename, "Stimulus");
                }
            } // if Performance Task

            // Check for tutorial
            {
                string tutorialId = xml.XpEval("itemrelease/item/tutorial/@id");
                if (tutorialId == null)
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Degraded, "Tutorial id missing from item.");
                }
                else if (Program.gValidationOptions.IsEnabled("trd"))
                {
                    string bankKey = xml.XpEval("itemrelease/item/tutorial/@bankkey");

                    // Look for the tutorial
                    string tutorialFilename = string.Format(@"Items\item-{1}-{0}\item-{1}-{0}.xml", tutorialId, bankKey);
                    if (!mPackageFolder.FileExists(tutorialFilename))
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Tutorial not found.", "TutorialId='{0}'", tutorialId);
                    }

                    // Make sure dependency is recorded in manifest
                    CheckDependencyInManifest(it, tutorialFilename, "Tutorial");
                }
            }
        } // TablulateInteraction

        void TabulatePassage(ItemContext it)
        {
            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Item folder missing item file.");
                return;
            }

            // Load the metadata
            XmlDocument xmlMetadata = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, "metadata.xml", xmlMetadata))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Passage metadata file not found.");
            }

            // Check interaction type
            string metaItemType = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:InteractionType", sXmlNs);
            if (!string.Equals(metaItemType, cStimulusInteractionType, StringComparison.Ordinal))
                ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Incorrect metadata <InteractionType>.", "InteractionType='{0}' Expected='{1}'", metaItemType, cStimulusInteractionType);

            // Get the version
            string version = xml.XpEvalE("itemrelease/passage/@version");

            // Subject
            string subject = xml.XpEvalE("itemrelease/passage/attriblist/attrib[@attid='itm_item_subject']/val");
            string metaSubject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            if (string.IsNullOrEmpty(subject))
            {
                // For the present, we don't expect the subject in the item attributes on passages
                //ReportError(it, ErrCat.Attribute, ErrSeverity.Tolerable, "Missing subject in item attributes (itm_item_subject).");
                subject = metaSubject;
                if (string.IsNullOrEmpty(subject))
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Missing subject in item metadata.");
            }
            else
            {
                if (!string.Equals(subject, metaSubject, StringComparison.Ordinal))
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Subject mismatch between item and metadata.", "ItemSubject='{0}' MetadataSubject='{1}'", subject, metaSubject);
            }

            // Grade: Passages do not have a particular grade affiliation
            string grade = string.Empty;

            // Rubric
            string rubric = string.Empty; // Passages don't have rubrics

            // AssessmentType (PT or CAT)
            /*
            string assessmentType;
            {
                string meta = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PerformanceTaskComponentItem", sXmlNs);
                if (meta == null || string.Equals(meta, "N", StringComparison.Ordinal)) assessmentType = "CAT";
                else if (string.Equals(meta, "Y", StringComparison.Ordinal)) assessmentType = "PT";
                else
                {
                    assessmentType = "CAT";
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Degraded, "PerformanceTaskComponentItem metadata should be 'Y' or 'N'.", "Value='{0}'", meta);
                }
            }
            */ 

            // WordList ID
            string wordlistId = ValidateWordlistReferences(it, xml);

            // ASL
            string asl = string.Empty;
            {
                bool aslFound = CheckForAttachment(it, xml, "ASL", "MP4");
                if (aslFound) asl = "MP4";
                if (!aslFound) ReportUnexpectedFiles(it, "ASL video", "^item_{0}_ASL", it.ItemId);

                bool aslInMetadata = string.Equals(xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AccessibilityTagsASLLanguage", sXmlNs), "Y", StringComparison.OrdinalIgnoreCase);
                if (aslInMetadata && !aslFound) ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Item metadata specifies ASL but no ASL in item.");
                if (!aslInMetadata && aslFound) ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Item has ASL but not indicated in the metadata.");
            }

            // BrailleType
            string brailleType = GetBrailleType(it, xml, xmlMetadata);

            // Translation
            string translation = GetTranslation(it, xml, xmlMetadata);

            // Media
            string media = GetMedia(it, xml);

            // Size
            long size = GetItemSize(it);

            // WordCount
            long wordCount = GetWordCount(it, xml);

            // Folder,StimulusId,Version,Subject,WordlistId,ASL,BrailleType,Translation,Media,Size,WordCount
            mStimulusReport.WriteLine(string.Join(",", CsvEncode(it.Folder), CsvEncode(it.ItemId), CsvEncode(version), CsvEncode(subject), CsvEncode(wordlistId), CsvEncode(asl), CsvEncode(brailleType), CsvEncode(translation), CsvEncode(media), size.ToString(), wordCount.ToString()));

        } // TabulatePassage

        
        void TabulateTutorial(ItemContext it)
        {
            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Item folder missing item file.");
                return;
            }

            // Read the metadata
            XmlDocument xmlMetadata = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, "metadata.xml", xmlMetadata))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Tutorial metadata file not found.");
            }

            // Get the version
            string version = xml.XpEvalE("itemrelease/item/@version");

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
            string grade = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_att_Grade']/val"); // will return "NA" or empty
            
            // Rubric
            string rubric = string.Empty;   // Not applicable

            // AssessmentType (PT or CAT)
            string assessmentType = string.Empty; // Not applicable
            
            // Standard, Claim and Target (not applicable
            string standard = string.Empty;
            string claim = string.Empty;
            string target = string.Empty;

            // WordList ID
            string wordlistId = ValidateWordlistReferences(it, xml);

            // ASL
            string asl = string.Empty;
            {
                bool aslFound = CheckForAttachment(it, xml, "ASL", "MP4");
                if (aslFound) asl = "MP4";
                if (!aslFound) ReportUnexpectedFiles(it, "ASL video", "^item_{0}_ASL*", it.ItemId);

                bool aslInMetadata = string.Equals(xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AccessibilityTagsASLLanguage", sXmlNs), "Y", StringComparison.OrdinalIgnoreCase);
                if (aslInMetadata && !aslFound) ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Item metadata specifies ASL but no ASL in item.");
                if (!aslInMetadata && aslFound) ReportError(it, ErrCat.Metadata, ErrSeverity.Tolerable, "Item has ASL but not indicated in the metadata.");
            }

            // BrailleType
            string brailleType = GetBrailleType(it, xml, xmlMetadata);

            // Translation
            string translation = GetTranslation(it, xml, xmlMetadata);

            // Folder,ItemId,ItemType,Version,Subject,Grade,Rubric,AsmtType,Standard,Claim,Target,WordlistId,ASL,BrailleType,Translation
            mItemReport.WriteLine(string.Join(",", CsvEncode(it.Folder), CsvEncode(it.ItemId), CsvEncode(it.ItemType), CsvEncode(version), CsvEncode(subject), CsvEncode(grade), CsvEncode(rubric), CsvEncode(assessmentType), CsvEncode(standard), CsvEncodeExcel(claim), CsvEncodeExcel(target), CsvEncode(wordlistId), CsvEncode(asl), CsvEncode(brailleType), CsvEncode(translation)));

        } // TabulateTutorial

        bool TryLoadXml(FileFolder ff, string filename, XmlDocument xml)
        {
            FileFile ffXml;
            if (!ff.TryGetFile(filename, out ffXml))
            {
                return false;
            }
            else
            {
                using (Stream stream = ffXml.Open())
                {
                    xml.Load(stream);
                }
            }
            return true;
        }

        bool CheckForAttachment(ItemContext it, XmlDocument xml, string attachType, string expectedExtension)
        {
            string xp = (!it.IsPassage)
                ? string.Concat("itemrelease/item/content/attachmentlist/attachment[@type='", attachType, "']")
                : string.Concat("itemrelease/passage/content/attachmentlist/attachment[@type='", attachType, "']");

            XmlElement xmlEle = xml.SelectSingleNode(xp) as XmlElement;
            if (xmlEle != null)
            {
                string filename = xmlEle.GetAttribute("file");
                if (string.IsNullOrEmpty(filename))
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Attachment missing file attribute.", "attachType='{0}'", attachType);
                    return false;
                }
                if (!it.FfItem.FileExists(filename))
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

        void ReportUnexpectedFiles(ItemContext it, string fileType, string regexPattern, params object[] args)
        {
            Regex regex = new Regex(string.Format(regexPattern, args));
            foreach (FileFile file in it.FfItem.Files)
            {
                Match match = regex.Match(file.Name);
                if (match.Success)
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Benign, "Unreferenced file found.", "fileType='{0}', filename='{1}'", fileType, file.Name);
                }
            }
        }

        void CheckDependencyInManifest(ItemContext it, string dependencyFilename, string dependencyType)
        {
            // Look up item in manifest
            string itemResourceId = null;
            string itemFilename = string.Concat(it.FfItem.RootedName, "/", it.FfItem.Name, ".xml");
            if (!mFilenameToResourceId.TryGetValue(NormalizeFilenameInManifest(itemFilename), out itemResourceId))
            {
                ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, "Item not found in manifest.");
            }

            // Look up dependency in the manifest
            string dependencyResourceId = null;
            if (!mFilenameToResourceId.TryGetValue(NormalizeFilenameInManifest(dependencyFilename), out dependencyResourceId))
            {
                ReportError(it, ErrCat.Manifest, ErrSeverity.Tolerable, dependencyType + " not found in manifest.", "DependencyFilename='{0}'", dependencyFilename);
            }

            // Check for dependency in manifest
            if (!string.IsNullOrEmpty(itemResourceId) && !string.IsNullOrEmpty(dependencyResourceId))
            {
                if (!mResourceDependencies.Contains(ToDependsOnString(itemResourceId, dependencyResourceId)))
                    ReportError("pmd", it, ErrCat.Manifest, ErrSeverity.Benign, string.Format("Manifest does not record dependency between item and {0}.", dependencyType), "ItemResourceId='{0}' {1}ResourceId='{2}'", itemResourceId, dependencyType, dependencyResourceId);
            }
        }

        string GetBrailleType(ItemContext it, XmlDocument xml, XmlDocument xmlMetadata)
        {
            // First, check metadata
            string brailleTypeMeta = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:BrailleType", sXmlNs);

            SortedSet<string> brailleTypes = new SortedSet<string>(new CompareBrailleType());

            // Enumerate all of the braille attachments
            {
                string xp = (!it.IsPassage)
                    ? string.Concat("itemrelease/item/content/attachmentlist/attachment")
                    : string.Concat("itemrelease/passage/content/attachmentlist/attachment");

                foreach(XmlElement xmlEle in xml.SelectNodes(xp))
                {
                    // Get attachment type and check if braille
                    string attachType = xmlEle.GetAttribute("type");
                    if (string.IsNullOrEmpty(attachType))
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Attachment missing type attribute.");
                        continue;
                    }
                    if (!attachType.Equals("PRN") && !attachType.Equals("BRF"))
                    {
                        continue; // Not braille attachment
                    }

                    if (!attachType.Equals(brailleTypeMeta))
                    {
                        ReportError(it, ErrCat.Metadata, ErrSeverity.Severe, "Braille metadata does not match attachment type.", "metadata='{0}', fileType='{1}'", brailleTypeMeta, attachType);
                    }

                    // Check that the file exists
                    string filename = xmlEle.GetAttribute("file");
                    if (string.IsNullOrEmpty(filename))
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Attachment missing file attribute.", "attachType='{0}'", attachType);
                        continue;
                    }
                    if (!it.FfItem.FileExists(filename))
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Tolerable, "Dangling reference to attached file that does not exist.", "attachType='{0}' Filename='{1}'", attachType, filename);
                        continue;
                    }

                    // Check the extension
                    string extension = Path.GetExtension(filename);
                    if (extension.Length > 0) extension = extension.Substring(1); // Strip leading "."
                    if (!string.Equals(extension, attachType, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Benign, "Unexpected extension for attached file.", "extension='{0}' expected='{1}' filename='{2}'", extension, attachType, filename);
                    }

                    // Get the subtype (if any)
                    string subtype = xmlEle.GetAttribute("subtype");
                    string brailleFile = (string.IsNullOrEmpty(subtype)) ? attachType.ToUpperInvariant() : string.Concat(attachType.ToUpperInvariant(), "(", subtype.ToLowerInvariant(), ")");

                    // Report the result
                    if (!brailleTypes.Add(brailleFile))
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Tolerable, "Multiple attachments of same type and subtype.", "type='{0}'", brailleFile);
                    }
                }
            }

            // Enumerate all embedded braille.
            {
                bool emptyBrailleTextFound = false;
                foreach (XmlElement xmlBraille in xml.SelectNodes("//brailleText"))
                {
                    foreach (XmlElement node in xmlBraille.ChildNodes)
                    {
                        if (node.NodeType == XmlNodeType.Element &&
                            (string.Equals(node.Name, "brailleTextString", StringComparison.Ordinal) || string.Equals(node.Name, "brailleCode", StringComparison.Ordinal)))
                        {
                            if (node.InnerText.Length != 0)
                            {
                                string brailleEmbedded = string.Equals(node.Name, "brailleTextString", StringComparison.Ordinal) ? "Embed" : "EmbedCode";
                                string brailleType = node.GetAttribute("type");
                                if (!string.IsNullOrEmpty(brailleType)) brailleEmbedded = string.Concat(brailleEmbedded, "(", brailleType.ToLowerInvariant(), ")");
                                brailleTypes.Add(brailleEmbedded);
                            }
                            else
                            {
                                emptyBrailleTextFound = true;
                            }
                        }
                    }
                }

                if (emptyBrailleTextFound && Program.gValidationOptions.IsEnabled("ebt"))
                    ReportError(it, ErrCat.Item, ErrSeverity.Benign, "brailleTextString and/or brailleCode element is empty.");
            }

            // Check for match with metadata
            if (string.Equals(brailleTypeMeta, "Not Braillable", StringComparison.OrdinalIgnoreCase))
            {
                if (brailleTypes.Count != 0)
                {
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Benign, "Metadata indicates not braillable but braille content included.", "brailleTypes='{0}'", string.Join(";", brailleTypes));
                }
                brailleTypes.Clear();
                brailleTypes.Add("NotBraillable");
            }

            return string.Join(";", brailleTypes);
        }

        private class CompareBrailleType : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                // Make "PRN" sort between "BRF" and "Embed"
                if (x.StartsWith("PRN", StringComparison.Ordinal)) x = "C" + x.Substring(3);
                if (y.StartsWith("PRN", StringComparison.Ordinal)) y = "C" + y.Substring(3);
                return string.CompareOrdinal(x, y);
            }
        }

        // Returns the Wordlist ID
        string CountWordlistReferences(ItemContext it, XmlDocument xml)
        {
            string wordlistId = string.Empty;
            string xp = it.IsPassage
                ? "itemrelease/passage/resourceslist/resource[@type='wordList']"
                : "itemrelease/item/resourceslist/resource[@type='wordList']";

            foreach (XmlElement xmlRes in xml.SelectNodes(xp))
            {
                string witId = xmlRes.GetAttribute("id");
                if (string.IsNullOrEmpty(witId))
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Degraded, "Item references blank wordList id.");
                }
                else
                {
                    if (!string.IsNullOrEmpty(wordlistId))
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Tolerable, "Item references multiple wordlists.");
                    }
                    else
                    {
                        wordlistId = witId;
                    }

                    mWordlistRefCounts.Increment(witId);
                }
            }

            return wordlistId;
        }

        // Returns the Wordlist ID
        string ValidateWordlistReferences(ItemContext it, XmlDocument xml)
        {
            // Get the wordlist ID
            string xp = it.IsPassage
                ? "itemrelease/passage/resourceslist/resource[@type='wordList']/@id"
                : "itemrelease/item/resourceslist/resource[@type='wordList']/@id";
            string wordlistId = xml.XpEval(xp);

            // Compose lists of referenced term Indices and Names
            List<int> termIndices = new List<int>();
            List<string> terms = new List<string>();

            // Process all CDATA (embedded HTML) sections in the content
            ExtractWordlistRefsFromXmlSubtree(it, xml.SelectSingleNode(it.IsPassage ? "itemrelease/passage/content" : "itemrelease/item/content"),
                termIndices, terms);

            if (string.IsNullOrEmpty(wordlistId))
            {
                if (termIndices.Count > 0)
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Degraded, "Item has terms marked for glossary but does not reference a wordlist.");
                }
                return string.Empty;
            }

            ValidateWordlistVocabulary(wordlistId, it, termIndices, terms);
            
            return wordlistId;
        }

        // Recursively traverse an XML subtree, find any CDATA sections and pass the inner text (which is presumably HTML)
        // to ExtractWordlistRefsFromHtml.
        void ExtractWordlistRefsFromXmlSubtree(ItemContext it, XmlNode node, List<int> termIndices, List<string> terms)
        {
            if (node.NodeType == XmlNodeType.CDATA)
            {
                ExtractWordlistRefsFromHtml(it, node.InnerText, termIndices, terms);
            }
            else
            {
                foreach(XmlNode subnode in node.ChildNodes)
                {
                    ExtractWordlistRefsFromXmlSubtree(it, subnode, termIndices, terms);
                }
            }
        }

        /*
        This search is more picky than what is theoretically acceptable. In practice, however, we find that items match this pattern. Most
        likely because they are auto-generated.

        Areas of excessive pickiness:
            * Assumes no text between <span> and </span>
            * Assumes no HTML tags embedded within the wordlist term.
        */
        static readonly Regex sRxWordlistReference = new Regex(@"<span[^>]*data-word-index=[""']([^""']*)[""'][^>]*></span>([^<]*)<span[^>]*data-tag-boundary=[""']end[""'][^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        static readonly char[] s_WhiteAndPunct = { '\t', '\n', '\r', ' ', '!', '"', '#', '$', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/', ':', ';', '<', '=', '>', '?', '@', '[', '\\', ']', '^', '_', '`', '{', '|', '~' };

        void ExtractWordlistRefsFromHtml(ItemContext it, string html, List<int> termIndices, List<string> terms)
        {
            foreach (Match match in sRxWordlistReference.Matches(html))
            {
                int termIndex;
                if (!int.TryParse(match.Groups[1].Value, out termIndex))
                {
                    ReportError(it, ErrCat.Item, ErrSeverity.Severe, "WordList reference term index is not integer", "Ref='{0}'", match.Value);
                }
                termIndices.Add(termIndex);
                terms.Add(match.Groups[2].Value.Trim(s_WhiteAndPunct));
            }
        }

        string GetTranslation(ItemContext it, XmlDocument xml, XmlDocument xmlMetadata)
        {
            string translation = string.Empty;

            // Find non-english content and the language value
            HashSet<string> languages = new HashSet<string>();
            foreach (XmlElement xmlEle in xml.SelectNodes(it.IsPassage ? "itemrelease/passage/content" : "itemrelease/item/content"))
            {
                string language = xmlEle.GetAttribute("language").ToLowerInvariant();

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
                languages.Add(language.ToLowerInvariant());

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
            foreach (XmlElement xmlEle in xmlMetadata.SelectNodes("metadata/sa:smarterAppMetadata/sa:Language", sXmlNs))
            {
                string language = xmlEle.InnerText;
                if (!languages.Contains(language))
                {
                    ReportError(it, ErrCat.Metadata, ErrSeverity.Degraded, "Item metadata indicates language but item content does not include that language.", "Language='{0}'", language);
                }
            }

            return translation;
        }

        static readonly HashSet<string> sMediaFileTypes = new HashSet<string>(
            new string[] {"MP4", "MP3", "M4A", "OGG", "VTT", "M4V", "MPG", "MPEG"  });

        string GetMedia(ItemContext it, XmlDocument xml)
        {
            //if (it.ItemId.Equals("1117", StringComparison.Ordinal)) Debugger.Break();

            // First get the list of attachments so that they are not included in the media list
            HashSet<string> attachments = new HashSet<string>();
            foreach (XmlElement xmlEle in xml.SelectNodes(it.IsPassage ? "itemrelease/passage/content/attachmentlist/attachment" : "itemrelease/item/content/attachmentlist/attachment"))
            {
                string filename = xmlEle.GetAttribute("file").ToLowerInvariant();
                if (!string.IsNullOrEmpty(filename)) attachments.Add(filename);
            }

            // Get the content string so we can verify that media files are referenced.
            string content = string.Empty;
            foreach (XmlElement xmlEle in xml.SelectNodes(it.IsPassage ? "itemrelease/passage/content/stem" : "itemrelease/item/content/stem"))
            {
                content += xmlEle.InnerText;
            }

            // Enumerate all files and select the media
            System.Collections.Generic.SortedSet<string> mediaList = new SortedSet<string>();
            foreach(FileFile file in it.FfItem.Files)
            {
                string filename = file.Name;
                if (attachments.Contains(filename.ToLowerInvariant())) continue;

                string ext = Path.GetExtension(filename);
                if (ext.Length > 0) ext = ext.Substring(1).ToUpperInvariant(); // Drop the leading period
                if (sMediaFileTypes.Contains(ext))
                {
                    // Make sure media file is referenced
                    if (Program.gValidationOptions.IsEnabled("umf") && content.IndexOf(filename, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        ReportError(it, ErrCat.Item, ErrSeverity.Benign, "Media file not referenced in item.", "Filename='{0}'", filename);
                    }
                    else
                    {
                        mediaList.Add(ext);
                    }
                }
            }

            if (mediaList.Count == 0) return string.Empty;
            return string.Join(";", mediaList);
        }

        long GetItemSize(ItemContext it)
        {
            long size = 0;
            foreach (FileFile f in it.FfItem.Files)
            {
                size += f.Length;
            }
            return size;
        }

        long GetWordCount(ItemContext it, XmlDocument xml)
        {
            string content = string.Empty;
            int index = 0, wordCount = 0;
            foreach (XmlElement xmlEle in xml.SelectNodes(it.IsPassage ? "itemrelease/passage/content/stem" : "itemrelease/item/content/stem"))
            {
                content = xmlEle.InnerText;

                // strip HTML
                content = Regex.Replace(content, @"<[^>]+>|&nbsp;", "").Trim();
                // replace the non-breaking HTML character &#xA0; with a blank
                content = content.Replace("&#xA0;", "");
                
                // calculate word count
                while (index < content.Length)
                {
                    // check if current char is part of a word.  whitespace, hypen and slash are word terminators
                    while (index < content.Length && 
                           (Char.IsWhiteSpace(content[index]) == false &&
                           !content[index].Equals("-") &&
                           !content[index].Equals("/")))
                        index++;
                    
                    wordCount++;

                    // skip whitespace, hypen, slash and stand alone punctuation marks until next word
                    while (index < content.Length && 
                           (Char.IsWhiteSpace(content[index]) == true ||
                           content[index].Equals("-") ||
                           content[index].Equals("/") ||
                           Regex.IsMatch(content[index].ToString(), @"[\p{P}]")))
                        index++;
                }
            }
            return wordCount;
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

        private void TabulateWordList(ItemContext it)
        {
            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Item folder missing item file.");
                return;
            }

            // Count this wordlist
            ++mWordlistCount;

            // See if the wordlist has been referenced
            int refCount = mWordlistRefCounts.Count(it.ItemId);
            if (refCount == 0)
            {
                ReportError(it, ErrCat.Wordlist, ErrSeverity.Benign, "Wordlist is not referenced by any item.");
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
            mWordlistReport.WriteLine(string.Join(",", it.Folder, CsvEncode(it.ItemId), refCount.ToString(), termcount.ToString(), maxgloss.ToString(), mingloss.ToString(), (((double)totalgloss)/((double)termcount)).ToString("f2")));
        }

        static readonly Regex sRxAudioAttachment = new Regex(@"<a[^>]*href=""([^""]*)""[^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        static readonly Regex sRxImageAttachment = new Regex(@"<img[^>]*src=""([^""]*)""[^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // Attachments don't have to follow the naming convention but they usually do. When they match then we compare values.
        // Sample: item_116605_v1_116605_01btagalog_glossary_ogg_m4a.m4a
        static readonly Regex sRxAttachmentNamingConvention = new Regex(@"^item_(\d+)_v\d+_(\d+)_(\d+)([a-zA-Z]+)_glossary(?:_ogg)?(?:_m4a)?(?:_ogg)?\.(?:ogg|m4a)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private void ValidateWordlistVocabulary(string wordlistId, ItemContext itemIt, List<int> termIndices, List<string> terms)
        {
            // Read the wordlist XML
            ItemContext it;
            if (!mIdToItemContext.TryGetValue(wordlistId, out it))
            {
                ReportError(itemIt, ErrCat.Item, ErrSeverity.Degraded, "Item references non-existent wordlist (WIT)", "wordlistId='{0}'", wordlistId);
                return;
            }
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportError(it, ErrCat.Item, ErrSeverity.Severe, "Item folder missing item file.");
                return;
            }

            // Sanity check
            if (!string.Equals(xml.XpEval("itemrelease/item/@id"), it.ItemId)) throw new InvalidDataException("Item id mismatch on pass 2");

            // Prefix itemId for passages
            string itemId = itemIt.ItemType.Equals("pass", StringComparison.OrdinalIgnoreCase) ? string.Concat("pass-", itemIt.ItemId) : itemIt.ItemId;

            // Create a dictionary of attachment files
            Dictionary<string, long> attachmentFiles = new Dictionary<string, long>();
            foreach (FileFile fi in it.FfItem.Files)
            {
                // If Audio or image file
                string extension = fi.Extension.ToLowerInvariant();
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
                    ReportError(it, ErrCat.Wordlist, ErrSeverity.Severe, "Wordlist has multiple terms with the same index.", "index='{0}'", index);
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
                    ReportError(it, ErrCat.Wordlist, ErrSeverity.Benign, "Wordlist term is not referenced by item.", "itemId='{0}' term='{1}' termIndex='{2}'", itemId, term, index);
                }

                // Find the attachment references
                foreach (XmlNode htmlNode in kwNode.SelectNodes("html"))
                {
                    string listType = htmlNode.XpEval("@listType");
                    mTranslationCounts.Increment(listType);

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
                        ProcessGlossaryAttachment(filename, it, index, listType, termReferenced, wordlistTerms, attachmentFiles, attachmentToReference, ref audioType, ref audioSize);

                        // Check for dual types
                        if (string.Equals(Path.GetExtension(filename), ".ogg", StringComparison.OrdinalIgnoreCase))
                        {
                            filename = Path.GetFileNameWithoutExtension(filename) + ".m4a";
                            ProcessGlossaryAttachment(filename, it, index, listType, termReferenced, wordlistTerms, attachmentFiles, attachmentToReference, ref audioType, ref audioSize);
                        }
                        else if (string.Equals(Path.GetExtension(filename), ".m4a", StringComparison.OrdinalIgnoreCase))
                        {
                            filename = Path.GetFileNameWithoutExtension(filename) + ".ogg";
                            ProcessGlossaryAttachment(filename, it, index, listType, termReferenced, wordlistTerms, attachmentFiles, attachmentToReference, ref audioType, ref audioSize);
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
                                ReportError(it, ErrCat.Wordlist, ErrSeverity.Degraded, "Wordlist attachment filename indicates item ID mismatch.", "filename='{0}' filenameItemId='{1}' expectedItemId='{2}'", filename, match2.Groups[1].Value, wordlistId);
                            }

                            // Check that the wordlist term index matches
                            /* While most filename indices match. It's quite common for them not to match and still be the correct audio
                               Disabling this check because it's mostly false alarms.

                            int filenameIndex;
                            if (!int.TryParse(match2.Groups[3].Value, out filenameIndex)) filenameIndex = -1;
                            if (filenameIndex != index && filenameIndex != ordinal
                                && (filenameIndex >= wordlistTerms.Count || !string.Equals(wordlistTerms[filenameIndex], term, StringComparison.OrdinalIgnoreCase)))
                            {
                                ReportError(it, ErrCat.Wordlist, ErrSeverity.Degraded, "Wordlist attachment filename indicates term index mismatch.", "filename='{0}' filenameIndex='{1}' expectedIndex='{2}'", filename, filenameIndex, index);
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
                                    filenameListType = "tagalGlossary";
                                    break;

                                case "apunjabi":
                                case "bpunjabi":
                                case "punjabiwest":
                                    filenameListType = "punjabiGlossary";
                                    break;

                                // Conventional case
                                default:
                                    filenameListType = string.Concat(filenameListType.ToLower(), "Glossary");
                                    break;
                            }
                            if (!filenameListType.Equals(listType))
                            {
                                ReportError(it, ErrCat.Wordlist, ErrSeverity.Degraded, "Wordlist attachment filename indicates attachment type mismatch.", "filename='{0}' filenameListType='{1}' expectedListType='{2}'", filename, filenameListType, listType);
                            }
                        }

                    }

                    // Look for an image glossary entry
                    match = sRxImageAttachment.Match(html);
                    if (match.Success)
                    {
                        // Use RegEx to find the audio glossary entry in the contents.
                        string filename = match.Groups[1].Value;
                        ProcessGlossaryAttachment(filename, it, index, listType, termReferenced, wordlistTerms, attachmentFiles, attachmentToReference, ref audioType, ref audioSize);
                    }

                    // Folder,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize
                    if (Program.gValidationOptions.IsEnabled("gtr"))
                        mGlossaryReport.WriteLine(string.Join(",", it.Folder, CsvEncode(it.ItemId), itemIt.ItemId.ToString(), index.ToString(), CsvEncodeExcel(term), CsvEncode(listType), html.Length.ToString(), audioType, audioSize.ToString(), imageType, imageSize.ToString(), CsvEncode(html)));
                    else
                        mGlossaryReport.WriteLine(string.Join(",", it.Folder, CsvEncode(it.ItemId), itemIt.ItemId.ToString(), index.ToString(), CsvEncodeExcel(term), CsvEncode(listType), html.Length.ToString(), audioType, audioSize.ToString(), imageType, imageSize.ToString()));
                }
            }

            Porter.Stemmer stemmer = new Porter.Stemmer();

            // Make sure terms match references
            for (int i=0; i<termIndices.Count; ++i)
            {
                int index = termIndices[i];
                if (index >= wordlistTerms.Count || string.IsNullOrEmpty(wordlistTerms[index]))
                {
                    ReportError(itemIt, ErrCat.Item, ErrSeverity.Degraded, "Item references non-existent wordlist term.", "text='{0}' wordlistId='{1}' termIndex='{2}'", terms[i], wordlistId, index);
                }
                else
                {
                    string stemmedText = stemmer.StemWord(terms[i].ToLowerInvariant());
                    string stemmedTerm = stemmer.StemWord(wordlistTerms[index].ToLowerInvariant());
                    if (!stemmedText.Equals(stemmedTerm, StringComparison.Ordinal))
                    {
                        ReportError(itemIt, ErrCat.Item, ErrSeverity.Degraded, "Item text does not match wordlist term.", "text='{0}' term='{1}' wordlistId='{2}' termIndex='{3}'", terms[i], wordlistTerms[index], wordlistId, index);
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
                        ReportError(it, ErrCat.Wordlist, ErrSeverity.Benign, "Unreferenced wordlist attachment file.", "filename='{0}'", pair.Key);
                    }
                }
            }
        }


        // This is kind of ugly with so many parameters but it's the cleanest way to handle this task that's repeated multiple times
        void ProcessGlossaryAttachment(string filename,
            ItemContext it, int termIndex, string listType, bool termReferenced,
            List<string> wordlistTerms, Dictionary<string, long> attachmentFiles, Dictionary<string, TermAttachmentReference> attachmentToTerm,
            ref string type, ref long size)
        {
            long fileSize = 0;
            if (!attachmentFiles.TryGetValue(filename, out fileSize))
            {
                if (termReferenced)
                {
                    ReportError(it, ErrCat.Wordlist, ErrSeverity.Severe, "Wordlist attachment not found.",
                        "filename='{0}' term='{1} termIndex='{2}", filename, wordlistTerms[termIndex], termIndex);
                }
                else if (Program.gValidationOptions.IsEnabled("mwa"))
                {
                    ReportError(it, ErrCat.Wordlist, ErrSeverity.Benign, "Wordlist attachment not found. Benign because corresponding term is not referenced.",
                        "filename='{0}' term='{1} termIndex='{2}", filename, wordlistTerms[termIndex], termIndex);
                }
            }

            // See if this attachment has previously been referenced
            TermAttachmentReference previousTerm = null;
            if (attachmentToTerm.TryGetValue(filename, out previousTerm))
            {
                // Error if different terms (case insensitive)
                if (!string.Equals(wordlistTerms[termIndex], wordlistTerms[previousTerm.TermIndex], StringComparison.InvariantCultureIgnoreCase))
                {
                    ReportError(it, ErrCat.Wordlist, ErrSeverity.Severe, "Two different wordlist terms reference the same attachment.",
                        "filename='{0}' termA='{1}' termB='{2}' termIndexA='{3}' termIndexB='{4}",
                        filename, wordlistTerms[previousTerm.TermIndex], wordlistTerms[termIndex], previousTerm.TermIndex, termIndex);
                }

                // Error if different listTypes (language or image)
                if (!string.Equals(listType, previousTerm.ListType, StringComparison.Ordinal))
                {
                    ReportError(it, ErrCat.Wordlist, ErrSeverity.Severe, "Same wordlist attachment used for different languages or types.",
                        "filename='{0}' term='{1}' typeA='{2}' typeB='{3}' termIndexA='{4}' termIndexB='{5}",
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

        void ValidateManifest()
        {
            // Prep an itemcontext for reporting errors
            ItemContext it = new ItemContext(this, mPackageFolder, null, null);

            // Load the manifest
            XmlDocument xmlManifest = new XmlDocument(sXmlNt);
            if (!TryLoadXml(mPackageFolder, cImsManifest, xmlManifest))
            {
                ReportError(it, ErrCat.Manifest, ErrSeverity.Severe, "Manifest not found.");
                return;
            }

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
                else if (!mPackageFolder.FileExists(filename))
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
                filename = NormalizeFilenameInManifest(filename);
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
                foreach (FileFolder ff in mPackageFolder.Folders)
                {
                    ValidateDirectoryInManifest(it, ff);
                }
            }
        }

        // Recursively check that files exist in the manifest
        void ValidateDirectoryInManifest(ItemContext it, FileFolder ff)
        {
            // See if this is an item or stimulus directory
            string itemFileName = null;
            string itemId = null;
            if (ff.Name.StartsWith("item-", StringComparison.OrdinalIgnoreCase) || ff.Name.StartsWith("stim-", StringComparison.OrdinalIgnoreCase))
            {
                FileFile fi;
                if (ff.TryGetFile(string.Concat(ff.Name, ".xml"), out fi))
            {
                    itemFileName = NormalizeFilenameInManifest(fi.RootedName);

                if (!mFilenameToResourceId.TryGetValue(itemFileName, out itemId))
                {
                        ReportError(it, ErrCat.Manifest, ErrSeverity.Degraded, "Item does not appear in the manifest.", "ItemFilename='{0}'", itemFileName);
                    itemFileName = null;
                    itemId = null;
                }
            }
            }

            foreach (FileFile fi in ff.Files)
            {
                string filename = NormalizeFilenameInManifest(fi.RootedName);

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
            foreach(FileFolder ffSub in ff.Folders)
            {
                ValidateDirectoryInManifest(it, ffSub);
            }
        }

        string NormalizeFilenameInManifest(string filename)
        {
            filename = filename.ToLowerInvariant().Replace('\\', '/');
            return (filename[0] == '/') ? filename.Substring(1) : filename;
        }

        static string ToDependsOnString(string itemId, string dependsOnId)
        {
            return string.Concat(itemId, "~", dependsOnId);
        }

        void SummaryReport(TextWriter writer)
        {
            writer.WriteLine("Errors: {0}", mErrorCount);
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
            writer.WriteLine("Glossary Terms Used in Wordlists:");
            mTermCounts.Dump(writer);
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
                mErrorReport = new StreamWriter(mErrorReportPath, false, sUtf8NoBomEncoding);
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

        void ReportError(string validationOption, ItemContext it, ErrCat category, ErrSeverity severity, string msg)
        {
            if (Program.gValidationOptions.IsEnabled(validationOption))
                ReportError(it, category, severity, msg, null);
        }

        void ReportError(string validationOption, ItemContext it, ErrCat category, ErrSeverity severity, string msg, string detail, params object[] args)
        {
            if (Program.gValidationOptions.IsEnabled(validationOption))
                ReportError(it, category, severity, msg, detail, args);
        }

        private static readonly char[] cCsvEscapeChars = {',', '"', '\'', '\r', '\n'};

        static string CsvEncode(string text)
        {
            if (text.IndexOfAny(cCsvEscapeChars) < 0) return text;
            return string.Concat("\"", text.Replace("\"", "\"\""), "\"");
        }

        static string CsvEncodeExcel(string text)
        {
            return string.Concat("\"", text.Replace("\"", "\"\""), "\t\"");
        }

        private class ItemContext
        {
            public ItemContext(Tabulator tabulator, FileFolder ffItem, string itemId, string itemType)
            {
                FfItem = ffItem;
                ItemId = (itemId != null) ? itemId : string.Empty;
                ItemType = (itemType != null) ? itemType : string.Empty;
                Folder = tabulator.mPackageName + ffItem.RootedName;
            }

            public FileFolder FfItem { get; private set; }
            public string ItemId { get; private set; }
            public string ItemType { get; private set; }
            public string Folder { get; private set; }

            /*
            public string ItemFilename
            {
                get
                {
                    return Path.Combine(DiItem.FullName, DiItem.Name + ".xml");
                }
            }
            */ 

            public bool IsPassage
            {
                get
                {
                    return string.Equals(ItemType, "pass", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        class WordlistRef
        {
            public WordlistRef(ItemContext it, string witId, int[] termIndices, string[] terms)
            {
                It = it;
                WitId = witId;
                TermIndices = termIndices;
                Terms = terms;
            }

            public ItemContext It { get; private set; }
            public string WitId { get; private set; }
            public int[] TermIndices { get; private set; }
            public string[] Terms { get; private set; }
        }

        static void AddWordListRef(Dictionary <string, LinkedList<WordlistRef> > dict, WordlistRef value)
        {
            LinkedList<WordlistRef> list;
            if (!dict.TryGetValue(value.WitId, out list))
            {
                list = new LinkedList<WordlistRef>();
                dict.Add(value.WitId, list);
            }
            list.AddLast(value);
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

        public static int Count(this Dictionary<string, int> dict, string key)
        {
            int count;
            if (!dict.TryGetValue(key, out count)) count = 0;
            return count;
        }

        public static void Dump(this Dictionary<string, int> dict, TextWriter writer)
        {
            List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(dict);
            list.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
            {
                int diff = b.Value - a.Value;
                return (diff != 0) ? diff : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
            });
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

        public static int ParseLeadingInteger(this string str)
        {
            str = str.Trim();
            int i = 0;
            foreach (char c in str)
            {
                if (!char.IsDigit(c)) return i;
                i = (i * 10) + (c - '0');
            }
            return i;
        }
    }
}
