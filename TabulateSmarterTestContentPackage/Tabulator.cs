using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text.RegularExpressions;
using TabulateSmarterTestContentPackage.Extractors;
using TabulateSmarterTestContentPackage.Mappers;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
using TabulateSmarterTestContentPackage.Validators;

namespace TabulateSmarterTestContentPackage
{
    
    // Using partial class so that we can distribute the functions among source code files
    // while retaining the context of the tabulator class.
    partial class Tabulator : IDisposable
    {
        const string cImsManifest = "imsmanifest.xml";
        const string cItemTypeStim = "stim";
        const string cItemTypeWordlist = "wordList";
        const string cItemTypeTutorial = "tut";
        const string cScoreMetaHand = "HandScored";
        const string cScoreMetaMachine = "Automatic with Machine Rubric";
        const string cSelectedResponseAnswerKey = "SR";
        const string cSubjectTutorial = "Student Help";

        static NameTable sXmlNt;
        static XmlNamespaceManager sXmlNs;

        const string cStimulusInteractionType = "Stimulus";

        static readonly HashSet<string> sValidWritingTypes = new HashSet<string>(
            new[] {
                "Explanatory",
                "Opinion",
                "Informative",
                "Argumentative",
                "Narrative"
            });

        // Filenames
        const string cSummaryReportFn = "SummaryReport.txt";
        const string cItemReportFn = "ItemReport.csv";
        const string cStimulusReportFn = "StimulusReport.csv";
        const string cWordlistReportFn = "WordlistReport.csv";
        const string cGlossaryReportFn = "GlossaryReport.csv";
        const string cErrorReportFn = "ErrorReport.csv";
        const string cIdReportFn = "IdReport.csv";
        const string cRubricExportFn = "Rubrics";

        const int c_maxWerPoints = 10;

        static ItemIdentifier cBlankItemId = new ItemIdentifier(string.Empty, 0, 0);

        // Per Package variables
        TestPackage mPackage;
        Dictionary<string, string> mFilenameToResourceId = new Dictionary<string, string>();
        HashSet<string> mResourceDependencies = new HashSet<string>();
        DistinctList<ItemIdentifier> mItemQueue = new DistinctList<ItemIdentifier>();
        DistinctList<ItemIdentifier> mStimQueue = new DistinctList<ItemIdentifier>();
        DistinctList<ItemIdentifier> mWordlistQueue = new DistinctList<ItemIdentifier>();
        DistinctList<ItemIdentifier> mTutorialQueue = new DistinctList<ItemIdentifier>();
        Dictionary<string, int> mWordlistRefCounts = new Dictionary<string, int>();   // Reference count for wordlist IDs
        int mProgressCount = 0;
        int mTransferCount = 0;

        // Per report variables
        bool mInitialized;
        int mStartTicks;
        int mItemCount = 0;
        int mWordlistCount = 0;
        int mStimCount = 0;
        int mGlossaryTermCount = 0;
        Dictionary<string, int> mTypeCounts = new Dictionary<string, int>();
        Dictionary<string, int> mTermCounts = new Dictionary<string, int>();
        Dictionary<string, int> mTranslationCounts = new Dictionary<string, int>();
        Dictionary<string, int> mAnswerKeyCounts = new Dictionary<string, int>();

        string mReportPathPrefix;
        TextWriter mItemReport;
        TextWriter mStimulusReport;
        TextWriter mWordlistReport;
        TextWriter mGlossaryReport;
        TextWriter mSummaryReport;
        string mRubricPathPrefix;



        static Tabulator()
        {
            sXmlNt = new NameTable();
            sXmlNs = new XmlNamespaceManager(sXmlNt);
            sXmlNs.AddNamespace("sa", "http://www.smarterapp.org/ns/1/assessment_item_metadata");
            sXmlNs.AddNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instance");
            sXmlNs.AddNamespace("ims", "http://www.imsglobal.org/xsd/apip/apipv1p0/imscp_v1p1");

            StaticInitWordlist();
        }

        public static XmlNameTable XmlNt => sXmlNt;
        public static XmlNamespaceManager XmlNsMgr => sXmlNs;

        public Tabulator(string reportPathPrefix)
        {
            mReportPathPrefix = string.Concat(reportPathPrefix, "_");
        }

        public void Dispose()
        {
            Conclude();
        }

        public bool ReportIds { get; set; }
        public bool ExitAfterSelect { get; set; }
        public bool ExportRubrics { get; set; }
        public bool DeDuplicate { get; set; }

        public void SelectItems(IEnumerable<ItemIdentifier> itemIds)
        {
            foreach(var ii in itemIds)
            {
                if (ii.IsStimulus)
                {
                    mStimQueue.Add(ii);
                }
                else
                {
                    mItemQueue.Add(ii);
                }
                int count = mItemQueue.Count + mStimQueue.Count;
                if (count % 100 == 0)
                {
                    Console.Error.Write($"   Selected {count}\r");
                }
            }
        }

        void ReportSelectedItems()
        {
            // File gets deleted at init so we can do append for each set of items.
            using (var report = new StreamWriter(string.Concat(mReportPathPrefix, cIdReportFn), true, Encoding.UTF8))
            {
                if (report.BaseStream.Length == 0)
                {
                    report.WriteLine("BankKey,ItemId,ItemType");
                }
                foreach (ItemIdentifier ii in mItemQueue)
                {
                    report.WriteLine(string.Join(",", ii.BankKey, ii.ItemId, ii.ItemType));
                }

                foreach (ItemIdentifier ii in mStimQueue)
                {
                    report.WriteLine(string.Join(",", ii.BankKey, ii.ItemId, ii.ItemType));
                }
            }
        }

        // Initialize all files and collections for a tabulation run
        private void Initialize()
        {
            mStartTicks = Environment.TickCount;

            // Prep the error report
            ReportingUtility.ErrorReportPath = string.Concat(mReportPathPrefix, cErrorReportFn);
            File.Delete(ReportingUtility.ErrorReportPath); // Delete does not throw exception if file does not exist.
            ReportingUtility.DeDuplicate = DeDuplicate;

            // Delete any existing reports
            File.Delete(mReportPathPrefix + cIdReportFn);
            File.Delete(mReportPathPrefix + cItemReportFn);
            File.Delete(mReportPathPrefix + cStimulusReportFn);
            File.Delete(mReportPathPrefix + cWordlistReportFn);
            File.Delete(mReportPathPrefix + cGlossaryReportFn);

            // Prep the summary report
            mSummaryReport = new StreamWriter(string.Concat(mReportPathPrefix, cSummaryReportFn), false, Encoding.UTF8);

            // Write the header
            {
                var application = System.Reflection.Assembly.GetExecutingAssembly();
                mSummaryReport.WriteLine($"TabulateSmarterTestContentPackage v{application.GetName().Version}");
                mSummaryReport.WriteLine();
            }

            // Report options
            mSummaryReport.WriteLine($"Validation Options: {Program.Options}");

            // If tabulation not being suppressed, open the other reports
            if (!ExitAfterSelect)
            {
                mItemReport = new StreamWriter(string.Concat(mReportPathPrefix, cItemReportFn), false, Encoding.UTF8);
                // DOK is "Depth of Knowledge"
                // In the case of multiple standards/claims/targets, these headers will not be sufficient
                // TODO: Add CsvHelper library to allow expandable headers
                mItemReport.WriteLine("Folder,BankKey,ItemId,ItemType,Version,Subject,Grade,Status,AnswerKey,AnswerOptions,AsmtType,WordlistId,StimId,TutorialId,ASL," +
                                      "BrailleType,Translation,Glossary,Media,Size,DOK,AllowCalculator,MathematicalPractice,MaxPoints," +
                                      "Claim,Target,CCSS,ClaimContentTarget,SecondaryCCSS,SecondaryClaimContentTarget,PtWritingType," +
                                      "CAT_MeasurementModel,CAT_ScorePoints,CAT_Dimension,CAT_Weight,CAT_Parameters,PP_MeasurementModel," +
                                      "PP_ScorePoints,PP_Dimension,PP_Weight,PP_Parameters");

                mStimulusReport = new StreamWriter(string.Concat(mReportPathPrefix, cStimulusReportFn), false, Encoding.UTF8);
                mStimulusReport.WriteLine("Folder,BankKey,StimulusId,Version,Subject,Status,WordlistId,ASL,BrailleType,Translation,Glossary,Media,Size,WordCount,StimulusLiteraryKnowledgeDemands,StimulusLiteraryNonFiction,StimulusReadabilityFK,StimulusReadabilityLexile");

                mWordlistReport = new StreamWriter(string.Concat(mReportPathPrefix, cWordlistReportFn), false, Encoding.UTF8);
                mWordlistReport.WriteLine("Folder,BankKey,WIT_ID,RefCount,TermCount,MaxGloss,MinGloss,AvgGloss");

                mGlossaryReport = new StreamWriter(string.Concat(mReportPathPrefix, cGlossaryReportFn), false, Encoding.UTF8);
                mGlossaryReport.WriteLine(Program.gValidationOptions.IsEnabled("gtr")
                    ? "Folder,BankKey,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize,Text"
                    : "Folder,BankKey,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize");

                // If rubrics being exported, ensure the directory exists
                if (ExportRubrics)
                {
                    mRubricPathPrefix = string.Concat(mReportPathPrefix, cRubricExportFn);
                    Directory.CreateDirectory(mRubricPathPrefix);
                }
            }

            ReportingUtility.ErrorCount = 0;
            mItemCount = 0;
            mWordlistCount = 0;
            mStimCount = 0;
            mGlossaryTermCount = 0;

            mTypeCounts.Clear();
            mTermCounts.Clear();
            mTranslationCounts.Clear();
            mAnswerKeyCounts.Clear();

            mInitialized = true;
        }

        private void Conclude()
        {
            try
            {
                if (mSummaryReport != null)
                {
                    SummaryReport(mSummaryReport);

                    // Report aggregate results to the console
                    Console.WriteLine("{0} Errors reported.", ReportingUtility.ErrorCount);
                    Console.WriteLine();
                }
            }
            finally
            {
                if (mSummaryReport != null)
                {
                    mSummaryReport.Dispose();
                    mSummaryReport = null;
                }
                if (mWordlistReport != null)
                {
                    mWordlistReport.Dispose();
                    mWordlistReport = null;
                }
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
                ReportingUtility.CloseReport();
            }
        }

        public void Tabulate(TestPackage package)
        {
            if (!mInitialized)
            {
                Initialize();
            }
            
            Console.WriteLine("Tabulating " + package.Name);
            try
            {
                var startTicks = Environment.TickCount;
                ReportingUtility.CurrentPackageName = package.Name;

                // Initialize package-specific collections
                mPackage = package;
                mFilenameToResourceId.Clear();
                mResourceDependencies.Clear();
                mWordlistRefCounts.Clear();

                // Validate the manifest (if it exists)
                {
                    FileFolder rootFolder;
                    if (package.TryGetItem(null, out rootFolder))   // Retrieves the root folder if the package has one
                    {
                        // Validate manifest
                        if (!ValidateManifest(rootFolder))
                        {
                            return; // Not a valid package
                        }
                    }
                }

                // Select the items to be tablulated (if a specific set was not already given)
                string label;
                if (mItemQueue.Count == 0 && mStimQueue.Count == 0)
                {
                    SelectItems(package.ItemsAndStimuli);
                    label = "Selected";
                }
                else
                {
                    label = "Preselected";
                }

                // Sort the queues before reporting IDs
                mItemQueue.Sort();
                mStimQueue.Sort();

                // Report
                if (ReportIds)
                {
                    ReportSelectedItems();
                }
                Console.WriteLine($"   {label} {mItemQueue.Count} items and {mStimQueue.Count} stimuli.");

                // Tabulate the selected items
                if (!ExitAfterSelect)
                {
                    // Process Items
                    foreach (var ii in mItemQueue)
                    {
                        ++mProgressCount;
                        try
                        {
                            TabulateItem(ii);
                            ReportProgress();
                        }
                        catch (Exception err)
                        {
                            ReportingUtility.ReportError(ii, err);
                        }
                    }

                    // Process stimuli
                    mStimQueue.Sort(); // Sort again - stimuli may have been added while processing items
                    foreach (var ii in mStimQueue)
                    {
                        ++mProgressCount;
                        try
                        {
                            TabulateStimulus(ii);
                            ReportProgress();
                        }
                        catch (Exception err)
                        {
                            ReportingUtility.ReportError(ii, err);
                        }
                    }

                    // Process WordLists
                    mWordlistQueue.Sort();
                    foreach (var ii in mWordlistQueue)
                    {
                        ++mProgressCount;
                        try
                        {
                            TabulateWordList(ii);
                            ReportProgress();
                        }
                        catch (Exception err)
                        {
                            ReportingUtility.ReportError(ii, err);
                        }
                    }

                    // Process Tutorials (we handle these separately because they are dependent on items)
                    mTutorialQueue.Sort();
                    foreach (var ii in mTutorialQueue)
                    {
                        ++mProgressCount;
                        try
                        {
                            TabulateTutorial(ii);
                            ReportProgress();
                        }
                        catch (Exception err)
                        {
                            ReportingUtility.ReportError(ii, err);
                        }
                    }
                }

                ReportingUtility.CurrentPackageName = null;

                var elapsedTicks = unchecked((uint)Environment.TickCount - (uint)startTicks);
                Console.WriteLine("   Package time: {0}.{1:d3} seconds", elapsedTicks / 1000, elapsedTicks % 1000);
                mSummaryReport.WriteLine("{0}: {1}.{2:d3} seconds", package.Name, elapsedTicks / 1000, elapsedTicks % 1000);

                // Clear the queues
                mItemQueue.Clear();
                mStimQueue.Clear();
                mWordlistQueue.Clear();
                mTutorialQueue.Clear();
                mProgressCount = 0;
                mTransferCount = 0;
            }
            catch (Exception err)
            {
                Console.WriteLine("   Exception: " + err.Message);
                ReportingUtility.ReportError(null, err);
            }
        }

        static readonly char[] c_progressChars = new char[] { '|', '/', '-', '\\' };
        static int s_progressSpin;
        static int s_lastProgressTicks;
        private void ReportProgress()
        {
            uint elapsedTicks = unchecked((uint)Environment.TickCount - (uint)s_lastProgressTicks);
            if (elapsedTicks > 150) // milliseconds
            {
                ++s_progressSpin;
                s_lastProgressTicks = Environment.TickCount;
            }
            int total = mItemQueue.Count + mStimQueue.Count + mTutorialQueue.Count + mWordlistQueue.Count - mTransferCount;
            Console.Error.Write($"   {mProgressCount} of {total} {c_progressChars[s_progressSpin & 3]}\r");
        }

        private void TabulateItem(ItemIdentifier ii)
        {
            /* Handling wordList items.
             * 
             * To begin with, we don't know if an item is an actual interaction item so wordList
             * items will come into this function. However, wordLists need to be processed after
             * items so they have their own queue. WordLists get into that queue in one two ways.
             * 
             * 1) When an item is processed that references a wordList then the ID of that wordlist
             * is added into the wordList queue.
             * 
             * 2) In this function, when the item is read and it's type is wordList then it will be
             * added to the queue.
             * 
             * We avoid reading and parsing the wordList XML unnecessarily by checkign whether the
             * item ID is in the wordlist queue before ever reading it in. That will happen if the
             * referencing item comes before the wordlist item in the queue.
             */

            // Skip the item if it's already in the wordlist queue.
            if (mWordlistQueue.Contains(ii))
            {
                --mProgressCount;   // Didn't actually process anything
                return;
            }

            // Skip the item it it's already in the tutorial queue.
            if (mTutorialQueue.Contains(ii))
            {
                --mProgressCount;   // Didn't actually process anything
                return;
            }

            // Get the item folder
            FileFolder ffItem;
            if (!mPackage.TryGetItem(ii, out ffItem))
            {
                ReportingUtility.ReportError(ii, ErrorId.T0110);
                return;
            }

            // Read the item XML
            var xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(ffItem, ffItem.Name + ".xml", xml))
            {
                ReportingUtility.ReportError(ii, ErrorId.T0025, LoadXmlErrorDetail);
                return;
            }

            // Get the actual item type
            string itemType = null;
            var xmlItem = xml.SelectSingleNode("itemrelease/item") as XmlElement;
            if (xmlItem != null)
            {
                itemType = xmlItem.XpEval("@format") ?? xmlItem.XpEval("@type");
            }
            if (string.IsNullOrEmpty(itemType))
            {
                ReportingUtility.ReportError(ii, ErrorId.T0044, LoadXmlErrorDetail);
                return;
            }
            ii.ItemType = itemType;

            // If wordlist, transfer to the wordList queue
            if (ii.ItemType.Equals(cItemTypeWordlist, StringComparison.Ordinal))
            {
                if (mWordlistQueue.Add(ii))
                {
                    ++mTransferCount;
                }
                --mProgressCount; // Have not yet processed this item
                return;
            }

            // If tutorial, transfer to the tutorial queue
            if (ii.ItemType.Equals(cItemTypeTutorial))
            {
                if (mTutorialQueue.Add(ii))
                {
                    ++mTransferCount;
                }
                --mProgressCount; // Have not yet processed this item
                return;
            }

            // Count the item
            ++mItemCount;
            mTypeCounts.Increment(itemType);

            // Build the item context
            var it = new ItemContext(mPackage, ffItem, ii);

            // Handle the item according to type
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
                    ReportingUtility.ReportError(it, ErrorId.T0111, "itemType='{0}'", it.ItemType);
                    TabulateInteraction(it);
                    break;

                default:
                    ReportingUtility.ReportError(it, ErrorId.T0080, "itemType='{0}'", it.ItemType);
                    break;
            }
        }

        private void TabulateInteraction(ItemContext it)
        {
            // Read the item XML
            var xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportingUtility.ReportError(it, ErrorId.T0025, LoadXmlErrorDetail);
                return;
            }

            // Load metadata
            var xmlMetadata = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, "metadata.xml", xmlMetadata))
            {
                ReportingUtility.ReportError(it, ErrorId.T0112, LoadXmlErrorDetail);
            }

            VersionValidator.Validate(it, xml, xmlMetadata); 

            IList< ItemScoring > scoringInformation = new List<ItemScoring>();
            if (xmlMetadata.HasChildNodes)
            {
                scoringInformation = IrtExtractor.RetrieveIrtInformation(xmlMetadata.MapToXDocument()).ToList();
            }
            if (!scoringInformation.Any())
            {
                scoringInformation.Add(new ItemScoring());
            }

            // Check interaction type
            var metaItemType = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:InteractionType", sXmlNs);
            if (!string.Equals(metaItemType, it.ItemType.ToUpperInvariant(), StringComparison.Ordinal))
                ReportingUtility.ReportError(it, ErrorId.T0113, "InteractionType='{0}' Expected='{1}'", metaItemType, it.ItemType.ToUpperInvariant());

            // Subject
            var subject = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_item_subject']/val");
            var metaSubject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            if (string.IsNullOrEmpty(subject))
            {
                ReportingUtility.ReportError(it, ErrorId.T0060);
                subject = metaSubject;
                if (string.IsNullOrEmpty(subject))
                    ReportingUtility.ReportError(it, ErrorId.T0061);
            }
            else
            {
                if (!string.Equals(subject, metaSubject, StringComparison.Ordinal))
                    ReportingUtility.ReportError(it, ErrorId.T0115, "ItemSubject='{0}' MetadataSubject='{1}'", subject, metaSubject);
            }

            // AllowCalculator
            var allowCalculator = AllowCalculatorFromMetadata(xmlMetadata, sXmlNs);
            if (string.IsNullOrEmpty(allowCalculator) && 
                (string.Equals(metaSubject, "MATH", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(subject, "MATH", StringComparison.OrdinalIgnoreCase)))
            {
                ReportingUtility.ReportError(it, ErrorId.T0001);
            }

            // MaximumNumberOfPoints
            var maximumNumberOfPoints = MaximumNumberOfPointsFromMetadata(xmlMetadata, sXmlNs);
            {
                int maxPts = 0;
                if (string.IsNullOrEmpty(maximumNumberOfPoints) || !int.TryParse(maximumNumberOfPoints, out maxPts))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0049);
                }

                else if (it.ItemType.Equals("wer", StringComparison.OrdinalIgnoreCase) && maxPts > c_maxWerPoints)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0050, $"maxPoints='{maxPts}' subject='{subject}'");
                }
            }

            // Grade
            var grade = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_att_Grade']/val").Trim();
            var metaGrade = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:IntendedGrade", sXmlNs);
            if (string.IsNullOrEmpty(grade))
            {
                ReportingUtility.ReportError(it, ErrorId.T0059);
                grade = metaGrade;
                if (string.IsNullOrEmpty(grade))
                    ReportingUtility.ReportError(it, ErrorId.T0058);
            }
            else
            {
                if (!string.Equals(grade, metaGrade, StringComparison.Ordinal))
                    ReportingUtility.ReportError(it, ErrorId.T0116, "ItemGrade='{0}', MetadataGrade='{1}'", grade, metaGrade);
            }

            // Answer Key
            var answerKey = string.Empty;
            ScoringType scoringType;
            {

                var answerKeyValue = string.Empty;
                var xmlEle = xml.SelectSingleNode("itemrelease/item/attriblist/attrib[@attid='itm_att_Answer Key']") as XmlElement;
                if (xmlEle != null)
                {
                    answerKeyValue = xmlEle.XpEvalE("val");
                }

                // The XML element is "MachineRubric" but it should really be called MachineScoring or AnswerKey
                var machineScoringType = string.Empty;
                var machineScoringFilename = xml.XpEval("itemrelease/item/MachineRubric/@filename");
                if (machineScoringFilename != null)
                {
                    machineScoringType = Path.GetExtension(machineScoringFilename).ToLowerInvariant();
                    if (machineScoringType.Length > 0) machineScoringType = machineScoringType.Substring(1);
                    if (!it.FfItem.FileExists(machineScoringFilename))
                        ReportingUtility.ReportError(it, ErrorId.T0046, "filename='{0}'", machineScoringFilename);
                    if (!machineScoringType.Equals("qrx", StringComparison.Ordinal))
                        ReportingUtility.ReportError(it, ErrorId.T0117, $"type='{machineScoringType}' filename='{machineScoringFilename}'");
                }

                var metadataScoringEngine = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:ScoringEngine", sXmlNs);

                // Answer key type is dictated by item type
                scoringType = ScoringType.Basic;
                string metadataExpected = null;
                switch (it.ItemType)
                {
                    case "mc":      // Multiple Choice
                        metadataExpected = "Automatic with Key";
                        if (answerKeyValue.Length != 1 || answerKeyValue[0] < 'A' || answerKeyValue[0] > 'Z')
                            ReportingUtility.ReportError(it, ErrorId.T0081, "itm_att_Answer Key='{0}'", answerKeyValue);
                        answerKey = answerKeyValue;
                        scoringType = ScoringType.Basic;
                        break;

                    case "ms":      // Multi-select
                        metadataExpected = "Automatic with Key(s)";
                        {
                            var parts = answerKeyValue.Split(',');
                            var validAnswer = parts.Length > 0;
                            foreach (string answer in parts)
                            {
                                if (answer.Length != 1 || answer[0] < 'A' || answer[0] > 'Z') validAnswer = false;
                            }
                            if (!validAnswer) ReportingUtility.ReportError(it, ErrorId.T0082, "itm_att_Answer Key='{0}'", answerKeyValue);
                            answerKey = answerKeyValue;
                            scoringType = ScoringType.Basic;
                        }
                        break;

                    case "EBSR":    // Evidence-based selected response
                        {
                            metadataExpected = "Automatic with Key(s)";
                            if (answerKeyValue.Length != 1 || answerKeyValue[0] < 'A' || answerKeyValue[0] > 'Z')
                                ReportingUtility.ReportError(it, ErrorId.T0078, "itm_att_Answer Key='{0}'", answerKeyValue);

                            // Retrieve the answer key for the second part of the EBSR
                            xmlEle = xml.SelectSingleNode("itemrelease/item/attriblist/attrib[@attid='itm_att_Answer Key (Part II)']") as XmlElement;
                            string answerKeyPart2 = null;
                            if (xmlEle != null)
                            {
                                answerKeyPart2 = xmlEle.XpEvalE("val");
                            }

                            if (answerKeyPart2 == null)
                            {
                                // Severity is benign because the current system uses the qrx file for scoring and doesn't
                                // depend on this attribute. However, we may depend on it in the future in which case
                                // the error would become severe.
                                ReportingUtility.ReportError(it, ErrorId.T0118);
                            }
                            else
                            {
                                var parts = answerKeyPart2.Split(',');
                                var validAnswer = parts.Length > 0;
                                foreach (var answer in parts)
                                {
                                    if (answer.Length != 1 || answer[0] < 'A' || answer[0] > 'Z') validAnswer = false;
                                }
                                if (validAnswer)
                                {
                                    answerKeyValue = string.Concat(answerKeyValue, ";", answerKeyPart2);
                                }
                                else
                                {
                                    ReportingUtility.ReportError(it, ErrorId.T0079, "itm_att_Answer Key (Part II)='{0}'", answerKeyPart2);
                                }
                            }
                            answerKey = answerKeyValue;
                            scoringType = ScoringType.Qrx;  // Basic scoring could be achieved but the current implementation uses Qrx
                        }
                        break;

                    case "eq":          // Equation
                    case "gi":          // Grid Item (graphic)
                    case "htq":         // Hot Text (in wrapped-QTI format)
                    case "mi":          // Match Interaction
                    case "ti":          // Table Interaction
                        {
                            bool handScored = metadataScoringEngine.StartsWith(cScoreMetaHand, StringComparison.OrdinalIgnoreCase);
                            bool hasMachineKey = !string.IsNullOrEmpty(machineScoringFilename);
                            if (!hasMachineKey && !handScored)
                                ReportingUtility.ReportError(it, ErrorId.T0035);
                            // Other conflicts between key presence and metadata settings are reported later
                            metadataExpected = hasMachineKey ? cScoreMetaMachine : cScoreMetaHand;
                            if (!string.Equals(answerKeyValue, it.ItemType.ToUpperInvariant()))
                                ReportingUtility.ReportError(it, ErrorId.T0119, "Value='{0}' Expected='{1}'", answerKeyValue, it.ItemType.ToUpperInvariant());
                            answerKey = hasMachineKey ? machineScoringType : (handScored ? ScoringType.Hand.ToString() : string.Empty);
                            scoringType = ScoringType.Qrx;
                        }
                        break;

                    case "er":          // Extended-Response
                    case "sa":          // Short Answer
                    case "wer":         // Writing Extended Response
                        metadataExpected = cScoreMetaHand;
                        if (!string.Equals(answerKeyValue, it.ItemType.ToUpperInvariant()))
                            ReportingUtility.ReportError(it, ErrorId.T0119, "Value='{0}' Expected='{1}'", answerKeyValue, it.ItemType.ToUpperInvariant());
                        answerKey = ScoringType.Hand.ToString();
                        scoringType = ScoringType.Hand;
                        break;

                    default:
                        ReportingUtility.ReportError(it, ErrorId.T0120);
                        answerKey = string.Empty;
                        scoringType = ScoringType.Basic;    // We don't really know.
                        break;
                }

                // Count the answer key types
                mAnswerKeyCounts.Increment(string.Concat(it.ItemType, " '", answerKey, "'"));
                if (!Program.gValidationOptions.IsEnabled("akv")
                    && (scoringType == ScoringType.Basic || it.ItemType.Equals("EBSR", StringComparison.Ordinal)))
                {
                    answerKey = cSelectedResponseAnswerKey;
                }

                // Check Scoring Engine metadata
                if (metadataExpected != null && !string.Equals(metadataScoringEngine, metadataExpected, StringComparison.Ordinal))
                {
                    if (string.Equals(metadataScoringEngine, metadataExpected, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0121, "Found='{0}' Expected='{1}'", metadataScoringEngine, metadataExpected);
                    }
                    else
                    {
                        // If first word of scoring engine metadata is the same (e.g. both are "Automatic" or both are "HandScored") then error is benign, otherwise error is tolerable
                        if (string.Equals(metadataScoringEngine.FirstWord(), metadataExpected.FirstWord(), StringComparison.OrdinalIgnoreCase))
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0023, "Found='{0}' Expected='{1}'", metadataScoringEngine, metadataExpected);
                        }
                        else
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0006, "Found='{0}' Expected='{1}'", metadataScoringEngine, metadataExpected);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(machineScoringFilename) && scoringType != ScoringType.Qrx)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0122, $"Filename='{machineScoringFilename}' ItemType='{it.ItemType}'");
                }

                // Check for unreferenced machine scoring files
                foreach (var fi in it.FfItem.Files)
                {
                    if (string.Equals(fi.Extension, ".qrx", StringComparison.OrdinalIgnoreCase)
                        && (machineScoringFilename == null || !string.Equals(fi.Name, machineScoringFilename, StringComparison.OrdinalIgnoreCase)))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0045, "Filename='{0}'", fi.Name);
                    }
                }

            } // Answer key

            // See how many answer options there are
            var answerOptions = string.Empty;
            if (scoringType == ScoringType.Basic)
            {
                var options = xml.SelectNodes("itemrelease/item/content[@language='ENU']/optionlist/option");
                if (options.Count == 0)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0031, $"itemtype='{it.ItemType}'");
                }
                answerOptions = options.Count.ToString();
            }

            // Rubric
            // If non-embedded answer key (either hand-scored or QRX scoring but not EBSR type check for a rubric (human scoring guidance)
            // We only care about english rubrics (at least for the present)
            if (scoringType != ScoringType.Basic && !it.ItemType.Equals("EBSR", StringComparison.OrdinalIgnoreCase))
            {
                RubricValidator.Validate(it, xml, ExportRubrics, mRubricPathPrefix);
            }

            // AssessmentType (PT or CAT)
            string assessmentType;
            {
                var meta = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PerformanceTaskComponentItem", sXmlNs);
                if (meta == null || string.Equals(meta, "N", StringComparison.Ordinal)) assessmentType = "CAT";
                else if (string.Equals(meta, "Y", StringComparison.Ordinal)) assessmentType = "PT";
                else
                {
                    assessmentType = "CAT";
                    ReportingUtility.ReportError(it, ErrorId.T0063, "Value='{0}'", meta);
                }
            }

            // Standards Alignment
            var standards = ItemStandardExtractor.Extract(it, grade, xmlMetadata);
            var reportingStandard = ItemStandardExtractor.Summarize(it, standards, subject, grade);
            bool hasPrimaryStandard = ItemStandardExtractor.HasPrimaryStandard;

            // MathematicalPractice
            var mathematicalPractice = MathematicalPracticeFromMetadata(xmlMetadata, sXmlNs);
            if (string.IsNullOrEmpty(mathematicalPractice)
                && standards[0].Subject == SmarterApp.ContentSpecSubject.Math
                && (standards[0].Claim >= SmarterApp.ContentSpecClaim.C2 && standards[0].Claim <= SmarterApp.ContentSpecClaim.C4))
            {
                ReportingUtility.ReportError(it, ErrorId.T0048, $"claim='{standards[0].Claim}'");
            }

            // Performance Task Writing Type
            var ptWritingType = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:PtWritingType", sXmlNs).Trim();

            // BrailleType (need this before validating content)
            string brailleType = BrailleValidator.Validate(it, xml, xmlMetadata);

            // Validate content segments
            string wordlistId;
            int englishCharacterCount;
            GlossaryTypes aggregateGlossaryTypes;
            ValidateContentAndWordlist(it, xml, !string.IsNullOrEmpty(brailleType), standards[0], out wordlistId, out englishCharacterCount, out aggregateGlossaryTypes);

            // Stimulus ID
            // Retrieve the Stimulus ID from the <associatedpassage> element
            var stimId = xml.XpEvalE("itemrelease/item/associatedpassage").Trim();            

            // Tutorial ID
            var tutorialId = xml.XpEvalE("itemrelease/item/tutorial/@id").Trim();
            if (string.IsNullOrEmpty(tutorialId))
            {
                ReportingUtility.ReportError(it, ErrorId.T0076);
            }
            else
            {
                TutorialIds.ValidateTutorialId(it, subject, tutorialId);
            }

            // ASL
            // When checking for ASL, the 'file' attribute value in the <attachment> element of the <attachmentlist> in the item xml is evaluated. This is only 1/2 the story.
            // An additional check for the .webm (or .mp4, depending on which file is referenced in the 'file' attribute) needs to be checked. Both file types types
            // are referenced in the <source> elements within the <attachment> element
            // This initial check shoud only return the ASL type from the 'file' attribute without actually checking if the file(s) exist
            var asl = GetAslType(it, xml, xmlMetadata);

            // Translation
            var translation = GetTranslation(it, xml, xmlMetadata);

            // Media
            var media = GetMedia(it, xml);

            // Size
            var size = GetItemSize(it);

            // DepthOfKnowledge (for WER must be 4 or higher)
            var depthOfKnowledge = DepthOfKnowledgeFromMetadata(xmlMetadata, sXmlNs);
            if (it.ItemType.Equals("wer", StringComparison.OrdinalIgnoreCase))
            {
                int idok;
                if (!int.TryParse(depthOfKnowledge, out idok) || idok < 4)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0223, $"depthOfKnowledge='{depthOfKnowledge}' for WER expected to be 4 or higher.");
                }
            }

            if (!string.IsNullOrEmpty(asl))
            {
                AslVideoValidator.Validate(it, xml);
            }

            var scoringSeparation = scoringInformation.GroupBy(
                x => !string.IsNullOrEmpty(x.Domain) && x.Domain.Equals("paper", StringComparison.OrdinalIgnoreCase)).ToList();

            //"Folder,BankKey,ItemId,ItemType,Version,Subject,Grade,Status,AnswerKey,AnswerOptions,AsmtType,WordlistId,StimId,TutorialId,ASL," +
            //"BrailleType,Translation,Media,Size,DOK,AllowCalculator,MathematicalPractice,MaxPoints," +
            //"Claim,Target,PrimaryCommonCore,PrimaryClaimContentTarget,SecondaryCommonCore,SecondaryClaimContentTarget,PtWritingType," +
            //"CAT_MeasurementModel,CAT_ScorePoints,CAT_Dimension,CAT_Weight,CAT_Parameters,PP_MeasurementModel," +
            //"PP_ScorePoints,PP_Dimension,PP_Weight,PP_Parameters"
            mItemReport.WriteLine(string.Join(",", CsvEncode(it.FolderDescription), it.BankKey.ToString(), it.ItemId.ToString(), CsvEncode(it.ItemType), CsvEncode(it.Version), CsvEncode(subject), 
                CsvEncode(grade), CsvEncode(GetStatus(it, xmlMetadata)), CsvEncode(answerKey), CsvEncode(answerOptions), CsvEncode(assessmentType), CsvEncode(wordlistId), CsvEncode(stimId), CsvEncode(tutorialId),
                CsvEncode(asl), CsvEncode(brailleType), CsvEncode(translation), GlossStringFlags(aggregateGlossaryTypes), CsvEncode(media), size.ToString(), CsvEncode(depthOfKnowledge), CsvEncode(allowCalculator), 
                CsvEncode(mathematicalPractice), CsvEncode(maximumNumberOfPoints),
                hasPrimaryStandard ? CsvEncode(standards[0].Claim.ToString()) : "", 
                hasPrimaryStandard ? CsvEncodeExcel(standards[0].Target) : "",
                hasPrimaryStandard ? CsvEncode(reportingStandard.PrimaryCCSS) : "",
                hasPrimaryStandard ? CsvEncode(reportingStandard.PrimaryClaimContentTarget) : "",
                CsvEncode(reportingStandard.SecondaryCCSS),
                CsvEncode(reportingStandard.SecondaryClaimsContentTargets), 
                CsvEncode(ptWritingType),
                CsvEncode(scoringSeparation.FirstOrDefault(x => !x.Key)?.Select(x => x.MeasurementModel).Aggregate((x,y) => $"{x};{y}") ?? string.Empty), 
                CsvEncode(scoringSeparation.FirstOrDefault(x => !x.Key)?.Select(x => x.ScorePoints).Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                CsvEncode(scoringSeparation.FirstOrDefault(x => !x.Key)?.Select(x => x.Dimension).Aggregate((x, y) => $"{x};{y}") ?? string.Empty), 
                CsvEncode(scoringSeparation.FirstOrDefault(x => !x.Key)?.Select(x => x.Weight).Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                CsvEncode(scoringSeparation.FirstOrDefault(x => !x.Key)?.Select(x => x.GetParameters()).Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                CsvEncode(scoringSeparation.FirstOrDefault(x => x.Key)?.Select(x => x.MeasurementModel).Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                CsvEncode(scoringSeparation.FirstOrDefault(x => x.Key)?.Select(x => x.ScorePoints).Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                CsvEncode(scoringSeparation.FirstOrDefault(x => x.Key)?.Select(x => x.Dimension).Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                CsvEncode(scoringSeparation.FirstOrDefault(x => x.Key)?.Select(x => x.Weight).Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                CsvEncode(scoringSeparation.FirstOrDefault(x => x.Key)?.Select(x => x.GetParameters()).Aggregate((x, y) => $"{x};{y}") ?? string.Empty)));

            // === Tabulation is complete, check for other errors

            FileValidator.Validate(it, xml);

            // Points
            {
                var itemPoint = xml.XpEval("itemrelease/item/attriblist/attrib[@attid='itm_att_Item Point']/val");
                if (string.IsNullOrEmpty(itemPoint))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0038);
                }
                else
                {
                    // Item Point attribute may have a suffix such as "pt", "pt.", " pt", " pts" and other variations.
                    // TODO: In seeking consistency, we may make this more picky in the future.
                    itemPoint = itemPoint.Trim();
                    if (!char.IsDigit(itemPoint[0]))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0039, "itm_att_Item Point='{0}'", itemPoint);
                    }
                    else
                    {
                        var points = itemPoint.ParseLeadingInteger();

                        // See if matches MaximumNumberOfPoints (defined as optional in metadata)
                        var metaPoint = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:MaximumNumberOfPoints", sXmlNs);
                        if (metaPoint == null)
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0123);
                        }
                        else
                        {
                            int mpoints;
                            if (!int.TryParse(metaPoint, out mpoints))
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0124, "MaximumNumberOfPoints='{0}'", metaPoint);
                            }
                            else if (mpoints != points)
                            {
                                ReportingUtility.ReportError(it, ErrorId.T0125, "MaximumNumberOfPoints='{0}' itm_att_Item Point='{0}'", mpoints, points);
                            }
                        }

                        // See if matches ScorePoints (defined as optional in metadata)
                        var scorePoints = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:ScorePoints", sXmlNs);
                        if (scorePoints == null)
                        {
                            ReportingUtility.ReportError(it, ErrorId.T0126);
                        }
                        else
                        {
                            scorePoints = scorePoints.Trim();
                            if (scorePoints[0] == '"')
                                scorePoints = scorePoints.Substring(1);
                            else
                                ReportingUtility.ReportError(it, ErrorId.T0069);
                            if (scorePoints[scorePoints.Length - 1] == '"')
                                scorePoints = scorePoints.Substring(0, scorePoints.Length - 1);
                            else
                                ReportingUtility.ReportError(it, ErrorId.T0070);

                            var maxspoints = -1;
                            var minspoints = 100000;
                            foreach (string sp in scorePoints.Split(','))
                            {
                                int spoints;
                                if (!int.TryParse(sp.Trim(), out spoints))
                                {
                                    ReportingUtility.ReportError(it, ErrorId.T0056, "ScorePoints='{0}' value='{1}'", scorePoints, sp);
                                }
                                else if (spoints < 0 || spoints > points)
                                {
                                    ReportingUtility.ReportError(it, ErrorId.T0057, "ScorePoints='{0}' value='{1}' min='0' max='{2}'", scorePoints, spoints, points);
                                }
                                else
                                {
                                    if (maxspoints < spoints)
                                    {
                                        maxspoints = spoints;
                                    }
                                    else
                                    {
                                        ReportingUtility.ReportError(it, ErrorId.T0127, "ScorePoints='{0}'", scorePoints);
                                    }
                                    if (minspoints > spoints) minspoints = spoints;
                                }
                            }
                            if (minspoints > 0) ReportingUtility.ReportError(it, ErrorId.T0055, "ScorePoints='{0}'", scorePoints);
                            if (maxspoints < points) ReportingUtility.ReportError(it, ErrorId.T0054, "ScorePoints='{0}' max='{1}'", scorePoints, points);
                        }
                    }
                }

                // Check editor type for Short Answer (SA) item format.
                // (Note: Item format and item type are usually the same but not always!)
                var itemFormat = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_att_Item Format']/val");
                if (string.Equals(itemFormat, "SA", StringComparison.OrdinalIgnoreCase))
                {
                    var responseType = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_att_Response Type']/val");
                    string expectedResponseType;
                    switch (subject)
                    {
                        case "MATH":
                            expectedResponseType = "HTMLEditor";
                            break;
                        case "ELA":
                            expectedResponseType = "PlainText";
                            break;
                        default:
                            expectedResponseType = string.Empty;
                            break;
                    }
                    if (!string.Equals(responseType, expectedResponseType, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0209, $"subject='{subject}' responseType='{responseType}' expected='{expectedResponseType}'");
                    }
                }
            }

            // Valdate stimulus reference
            if (!string.IsNullOrEmpty(stimId))
            {
                var metaStimId = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AssociatedStimulus", sXmlNs);
                if (!string.Equals(stimId, metaStimId, StringComparison.OrdinalIgnoreCase))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0042, "Item assocatedpassage='{0}' Metadata AssociatedStimulus='{1}'", stimId, metaStimId);
                }

                int nStimId;
                if (!int.TryParse(stimId, out nStimId))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0128, $"Item associatedpassage='{stimId}'");
                }
                else
                {
                    // Look for the stimulus
                    var iiStimulus = new ItemIdentifier(cItemTypeStim, it.BankKey, nStimId);
                    if (!mPackage.ItemExists(iiStimulus))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0102, "StimulusId='{0}'", stimId);
                    }
                    else
                    {
                        // Queue up the stimulus (if it's not already there)
                        mStimQueue.Add(iiStimulus);

                        // Make sure dependency is recorded in manifest
                        var stimulusFilename = string.Format(@"Stimuli\stim-{0}-{1}\stim-{0}-{1}.xml", it.BankKey, stimId);
                        CheckDependencyInManifest(it, stimulusFilename, "Stimulus");
                    }
                }
            }

            // Performance Task Details
            if (string.Equals(assessmentType, "PT", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(stimId))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0129);
                }

                // PtSequence
                int seq;
                var ptSequence = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PtSequence", sXmlNs);
                if (ptSequence == null)
                    ReportingUtility.ReportError(it, ErrorId.T0130);
                else if (!int.TryParse(ptSequence.Trim(), out seq))
                    ReportingUtility.ReportError(it, ErrorId.T0094, "PtSequence='{0}'", ptSequence);

                // PtWritingType Metadata (defined as optional in metadata but we'll still report a benign error if it's not on PT WER items)
                if (string.Equals(it.ItemType, "wer", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(ptWritingType))
                    {
                        ReportingUtility.ReportError(it, ErrorId.T0051);
                    }
                    else
                    {
                        if (!sValidWritingTypes.Contains(ptWritingType))
                        {
                            // Fix capitalization
                            var normalized = string.Concat(ptWritingType.Substring(0, 1).ToUpperInvariant(), ptWritingType.Substring(1).ToLowerInvariant());

                            // Report according to type of error
                            if (!sValidWritingTypes.Contains(normalized))
                                ReportingUtility.ReportError(it, ErrorId.T0131, "PtWritingType='{0}'", ptWritingType);
                            else
                                ReportingUtility.ReportError(it, ErrorId.T0132, "PtWritingType='{0}' expected='{1}'", ptWritingType, normalized);
                        }
                    }
                }
            } // if Performance Task

            // Check for tutorial details
            if (Program.gValidationOptions.IsEnabled("trd")
                && !string.IsNullOrEmpty(tutorialId)    // Missing tutorialId is reported earlier
                && !(mPackage is SingleItemPackage))
            {
                var bankKey = xml.XpEval("itemrelease/item/tutorial/@bankkey");
                if (string.IsNullOrEmpty(bankKey))
                {
                    bankKey = it.BankKey.ToString();
                }

                // Look for the tutorial
                var iiTutorial = new ItemIdentifier(cItemTypeTutorial, bankKey, tutorialId);
                if (!mPackage.ItemExists(iiTutorial))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0099, "TutorialId='{0}'", tutorialId);
                }
                else
                {
                    // Queue this up (if it isn't already) and manage progress counts
                    if (mTutorialQueue.Add(iiTutorial))
                    {
                        if (mItemQueue.Contains(iiTutorial)) ++mTransferCount;
                    }
                }

                // Make sure dependency is recorded in manifest
                var tutorialFilename = string.Format(@"Items\item-{1}-{0}\item-{1}-{0}.xml", tutorialId, bankKey);
                CheckDependencyInManifest(it, tutorialFilename, "Tutorial");
            }

            // If GI (Grid Item) Check for RendererSpec - GAX
            if (string.Equals(it.ItemType, "GI", StringComparison.OrdinalIgnoreCase))
            {
                // Attempt to retrieve the RendererSpec
                var rendererSpec = xml.XpEval("itemrelease/item/RendererSpec/@filename");
                if (string.IsNullOrEmpty(rendererSpec))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0211, "RendererSpec = ''");
                }
                else if (!it.FfItem.FileExists(rendererSpec))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0211, $"RendererSpec='{rendererSpec}' file not found.");
                }
                else if (!string.Equals(Path.GetExtension(rendererSpec), ".gax", StringComparison.OrdinalIgnoreCase))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0211, $"RendererSpec='{rendererSpec}' expected 'gax' extension.");
                }
            }

            // Check case of the folder name
            if (!it.FfItem.Name.StartsWith("Item-", StringComparison.Ordinal))
            {
                ReportingUtility.ReportError(it, ErrorId.T0212, $"folderName='{it.FfItem.Name}' expected='Item-###-####'");
            }
        } // TabulateInteraction

        void TabulateStimulus(ItemIdentifier ii)
        {
            // Get the item context
            ItemContext it;
            if (!ItemContext.TryCreate(mPackage, ii, out it))
            {
                ReportingUtility.ReportError(ii, ErrorId.T0133);
                return;
            }

            // Count the stimulus
            ++mStimCount;

            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportingUtility.ReportError(it, ErrorId.T0025, LoadXmlErrorDetail);
                return;
            }

            // Load the metadata
            XmlDocument xmlMetadata = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, "metadata.xml", xmlMetadata))
            {
                ReportingUtility.ReportError(it, ErrorId.T0112, LoadXmlErrorDetail);
            }

            VersionValidator.Validate(it, xml, xmlMetadata);
            ii.Version = it.Version; // Make available to error reporting

            // Check interaction type
            string metaItemType = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:InteractionType", sXmlNs);
            if (!string.Equals(metaItemType, cStimulusInteractionType, StringComparison.Ordinal))
                ReportingUtility.ReportError(it, ErrorId.T0113, "InteractionType='{0}' Expected='{1}'", metaItemType, cStimulusInteractionType);

            // Subject
            string subject = xml.XpEvalE("itemrelease/passage/attriblist/attrib[@attid='itm_item_subject']/val");
            string metaSubject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            if (string.IsNullOrEmpty(subject))
            {
                // For the present, we don't expect the subject in the item attributes on passages
                //ReportingUtility.ReportError(it, ErrorId.T0060);
                subject = metaSubject;
                if (string.IsNullOrEmpty(subject))
                    ReportingUtility.ReportError(it, ErrorId.T0061);
            }
            else
            {
                if (!string.Equals(subject, metaSubject, StringComparison.Ordinal))
                    ReportingUtility.ReportError(it, ErrorId.T0115, "ItemSubject='{0}' MetadataSubject='{1}'", subject, metaSubject);
            }

            // Grade: Passages do not have a particular grade affiliation
            string grade = string.Empty;

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
                    ReportingUtility.ReportError(it, ErrorId.T0063, "Value='{0}'", meta);
                }
            }
            */

            // BrailleType
            string brailleType = BrailleValidator.Validate(it, xml, xmlMetadata);

            // Validate content segments
            string wordlistId;
            int englishCharacterCount;
            GlossaryTypes aggregateGlossaryTypes;
            ValidateContentAndWordlist(it, xml, !string.IsNullOrEmpty(brailleType), null, out wordlistId, out englishCharacterCount, out aggregateGlossaryTypes);

            // ASL
            string asl = GetAslType(it, xml, xmlMetadata);
            if (!string.IsNullOrEmpty(asl))
            {
                AslVideoValidator.Validate(it, xml);
            }

            // Translation
            string translation = GetTranslation(it, xml, xmlMetadata);

            // Media
            string media = GetMedia(it, xml);

            // Size
            long size = GetItemSize(it);

            // WordCount
            long wordCount = GetWordCount(it, xml);

            // StimulusLiteraryKnowledgeDemands
            string literaryKnowledgeDemands = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:StimulusLiteraryKnowledgeDemands", sXmlNs);

            // StimulusLiteraryNonFiction
            string literaryNonFiction = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:StimulusLiteraryNonFiction", sXmlNs);

            // StimulusReadabilityFK
            string readabilityFk = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:StimulusReadabilityFK", sXmlNs);

            // StimulusReadabilityLexile
            string readabilityLexile = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:StimulusReadabilityLexile", sXmlNs);

            // Folder,BankKey,StimulusId,Version,Subject,Status,WordlistId,ASL,BrailleType,Translation,Media,Size,WordCount
            mStimulusReport.WriteLine(string.Join(",", CsvEncode(it.FolderDescription), it.BankKey.ToString(), it.ItemId.ToString(),
                CsvEncode(it.Version ), CsvEncode(subject), CsvEncode(GetStatus(it, xmlMetadata)), CsvEncode(wordlistId), CsvEncode(asl), CsvEncode(brailleType),
                CsvEncode(translation), GlossStringFlags(aggregateGlossaryTypes), CsvEncode(media), size.ToString(), wordCount.ToString(), CsvEncode(literaryKnowledgeDemands), 
                CsvEncode(literaryNonFiction), CsvEncode(readabilityFk), CsvEncode(readabilityLexile)));

            // Tabulation is complete, check for other errors.

            FileValidator.Validate(it, xml);

            // Check case of the folder name
            if (!it.FfItem.Name.StartsWith("stim-", StringComparison.Ordinal))
            {
                ReportingUtility.ReportError(it, ErrorId.T0212, $"folderName='{it.FfItem.Name}' expected='stim-###-####'");
            }

        } // TabulateStimulus


        void TabulateTutorial(ItemIdentifier ii)
        {
            // Get the item context
            ItemContext it;
            if (!ItemContext.TryCreate(mPackage, ii, out it))
            {
                ReportingUtility.ReportError(ii, ErrorId.T0134);
                return;
            }

            // Count the item
            ++mItemCount;
            mTypeCounts.Increment(cItemTypeTutorial);

            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportingUtility.ReportError(it, ErrorId.T0025, LoadXmlErrorDetail);
                return;
            }

            // Read the metadata
            XmlDocument xmlMetadata = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, "metadata.xml", xmlMetadata))
            {
                ReportingUtility.ReportError(it, ErrorId.T0112, LoadXmlErrorDetail);
            }

            // Validate the version
            VersionValidator.Validate(it, xml, xmlMetadata);
            ii.Version = it.Version; // Make available to error reporting

            // Subject
            string subject = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_item_subject']/val");
            string metaSubject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            if (string.IsNullOrEmpty(subject))
            {
                subject = cSubjectTutorial;
            }
            if (string.IsNullOrEmpty(metaSubject))
            {
                metaSubject = cSubjectTutorial;
            }

            // Grade
            var grade = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_att_Grade']/val"); // will return "NA" or empty
            
            // Answer Key
            var answerKey = string.Empty;   // Not applicable

            // AssessmentType (PT or CAT)
            var assessmentType = string.Empty; // Not applicable
            
            // Standard, Claim and Target (not applicable
            var standard = string.Empty;
            var claim = string.Empty;
            var target = string.Empty;

            // BrailleType
            string brailleType = BrailleValidator.Validate(it, xml, xmlMetadata);

            // Validate content segments
            string wordlistId;
            int englishCharacterCount;
            GlossaryTypes aggregateGlossaryTypes;
            ValidateContentAndWordlist(it, xml, !string.IsNullOrEmpty(brailleType), null, out wordlistId, out englishCharacterCount, out aggregateGlossaryTypes);

            // ASL
            var asl = GetAslType(it, xml, xmlMetadata);

            // Translation
            var translation = GetTranslation(it, xml, xmlMetadata);

            //"Folder,BankKey,ItemId,ItemType,Version,Subject,Grade,Status,AnswerKey,AnswerOptions,AsmtType,WordlistId,StimId,TutorialId,ASL," +
            //"BrailleType,Translation,Glossary,Media,Size,DOK,AllowCalculator,MathematicalPractice,MaxPoints," +
            //"Claim,Target,PrimaryCommonCore,PrimaryClaimContentTarget,SecondaryCommonCore,SecondaryClaimContentTarget,PtWritingType," +
            //"CAT_MeasurementModel,CAT_ScorePoints,CAT_Dimension,CAT_Weight,CAT_Parameters,PP_MeasurementModel," +
            //"PP_ScorePoints,PP_Dimension,PP_Weight,PP_Parameters"
            mItemReport.WriteLine(string.Join(",", CsvEncode(it.FolderDescription), it.BankKey.ToString(), it.ItemId.ToString(), CsvEncode(it.ItemType), CsvEncode(it.Version),
                CsvEncode(subject), CsvEncode(grade), CsvEncode(GetStatus(it, xmlMetadata)), CsvEncode(answerKey), string.Empty, CsvEncode(assessmentType), CsvEncode(wordlistId),
                string.Empty, string.Empty, CsvEncode(asl), CsvEncode(brailleType), CsvEncode(translation), GlossStringFlags(aggregateGlossaryTypes),
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));

            // Tabulation is complete, check for other errors.

            FileValidator.Validate(it, xml);

            // Check case of the folder name
            if (!it.FfItem.Name.StartsWith("Item-", StringComparison.Ordinal))
            {
                ReportingUtility.ReportError(it, ErrorId.T0212, $"folderName='{it.FfItem.Name}' expected='Item-###-####'");
            }

        } // TabulateTutorial

        string LoadXmlErrorDetail { get; set; }

        private bool TryLoadXml(FileFolder ff, string filename, XmlDocument xml)
        {
            FileFile ffXml;
            if (!ff.TryGetFile(filename, out ffXml))
            {
                LoadXmlErrorDetail = $"filename='{Path.GetFileName(filename)}' detail='File not found'";
                return false;
            }
            else
            {
                using (StreamReader reader = new StreamReader(ffXml.Open(), Encoding.UTF8, true, 1024, false))
                {
                    try
                    {
                        xml.Load(reader);
                    }
                    catch (Exception err)
                    {
                        LoadXmlErrorDetail = $"filename='{Path.GetFileName(filename)}' detail='{err.Message}'";
                        return false;
                    }
                }
            }
            return true;
        }

        static bool CheckForAttachment(ItemContext it, XmlDocument xml, string attachType, string expectedExtension)
        {
            var fileName = FileUtility.GetAttachmentFilename(it, xml, attachType);
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }
            if (!it.FfItem.FileExists(fileName))
            {
                ReportingUtility.ReportError(it, ErrorId.T0014, "attachType='{0}' Filename='{1}'", attachType, fileName);
                return false;
            }

            var extension = Path.GetExtension(fileName);
            if (extension.Length > 0) extension = extension.Substring(1); // Strip leading "."
            if (!string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                ReportingUtility.ReportError(it, ErrorId.T0135, "attachType='{0}' extension='{1}' expected='{2}' filename='{3}'", attachType, extension, expectedExtension, fileName);
            }
            return true;
        }

        static void ReportUnexpectedFiles(ItemContext it, string fileType, string regexPattern, params object[] args)
        {
            var regex = new Regex(string.Format(regexPattern, args));
            foreach (FileFile file in it.FfItem.Files)
            {
                Match match = regex.Match(file.Name);
                if (match.Success)
                {
                    ReportingUtility.ReportError(it, ErrorId.T0083, "fileType='{0}', filename='{1}'", fileType, file.Name);
                }
            }
        }

        private void CheckDependencyInManifest(ItemContext it, string dependencyFilename, string dependencyType)
        {
            // Suppress manifest checks if the manifest is empty
            if (mFilenameToResourceId.Count == 0) return;

            // Look up item in manifest
            string itemResourceId = null;
            string itemFilename = string.Concat(it.FfItem.RootedName, "/", it.FfItem.Name, ".xml");
            if (!mFilenameToResourceId.TryGetValue(NormalizeFilenameInManifest(itemFilename), out itemResourceId))
            {
                ReportingUtility.ReportError(it, ErrorId.T0100);
            }

            // Look up dependency in the manifest
            string dependencyResourceId = null;
            if (!mFilenameToResourceId.TryGetValue(NormalizeFilenameInManifest(dependencyFilename), out dependencyResourceId))
            {
                ReportingUtility.ReportError(it, ErrorId.T0136, $"type='{dependencyType}' DependencyFilename='{dependencyFilename}'");
            }

            // Check for dependency in manifest
            if (Program.gValidationOptions.IsEnabled("pmd")
                && !string.IsNullOrEmpty(itemResourceId) && !string.IsNullOrEmpty(dependencyResourceId)
                && !mResourceDependencies.Contains(ToDependsOnString(itemResourceId, dependencyResourceId)))
            {
                ReportingUtility.ReportError(it, ErrorId.T0137, $"ItemResourceId='{itemResourceId}' {dependencyType}ResourceId='{dependencyResourceId}'");
            }
        }

        private string GetAslType(ItemContext it, XmlDocument xml, XmlDocument xmlMetadata)
        {
            //var aslFound = CheckForAttachment(it, xml, "ASL", "MP4");
            // this is the file name from the 'file' attribute of the <attachment> element. 
            // set the aslFound variable based on the filename value being there
            var fileName = FileUtility.GetAttachmentFilename(it, xml, "ASL");
            var aslFound = fileName == "" ? false : true;

            if (!aslFound)
            {
                ReportUnexpectedFiles(it, "ASL video", "^item_{0}_ASL", it.ItemId);
            }

            var aslInMetadata = string.Equals(xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AccessibilityTagsASLLanguage", sXmlNs), "Y", StringComparison.OrdinalIgnoreCase);
            if (aslInMetadata && !aslFound) ReportingUtility.ReportError(it, ErrorId.T0037);
            if (!aslInMetadata && aslFound) ReportingUtility.ReportError(it, ErrorId.T0032);

            return (aslFound && aslInMetadata) ? "MP4" : string.Empty;
        }

        private string GetStatus(ItemContext it, XmlDocument xmlMetadata)
        {
            return xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Status", sXmlNs);
        }

        string GetTranslation(ItemContext it, XmlDocument xml, XmlDocument xmlMetadata)
        {
            // Find non-english content and the language value
            HashSet<string> languages = new HashSet<string>();
            foreach (XmlElement xmlEle in xml.SelectNodes(it.IsStimulus ? "itemrelease/passage/content" : "itemrelease/item/content"))
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

                // See if metadata agrees
                XmlNode node = xmlMetadata.SelectSingleNode(string.Concat("metadata/sa:smarterAppMetadata/sa:Language[. = '", language, "']"), sXmlNs);
                if (node == null) ReportingUtility.ReportError(it, ErrorId.T0030, "Language='{0}'", language);
            }

            string translation = string.Empty;

            // Now, search the metadata for translations and make sure all exist in the content
            foreach (XmlElement xmlEle in xmlMetadata.SelectNodes("metadata/sa:smarterAppMetadata/sa:Language", sXmlNs))
            {
                string language = xmlEle.InnerText;
                if (!languages.Contains(language))
                {
                    ReportingUtility.ReportError(it, ErrorId.T0036, "Language='{0}'", language);
                }

                // If not english, add to result
                if (!string.Equals(language, "eng", StringComparison.Ordinal))
                {
                    translation = (translation.Length > 0) ? string.Concat(translation, " ", language) : language;
                }
            }

            return translation;
        }

        static readonly HashSet<string> sMediaFileTypes = new HashSet<string>(
            new[] {"MP4", "MP3", "M4A", "OGG", "VTT", "M4V", "MPG", "MPEG"  });

        string GetMedia(ItemContext it, XmlDocument xml)
        {
            //if (it.ItemId.Equals("1117", StringComparison.Ordinal)) Debugger.Break();

            // First get the list of attachments so that they are not included in the media list
            HashSet<string> attachments = new HashSet<string>();
            foreach (XmlElement xmlEle in xml.SelectNodes(it.IsStimulus ? "itemrelease/passage/content/attachmentlist/attachment" : "itemrelease/item/content/attachmentlist/attachment"))
            {
                string filename = xmlEle.GetAttribute("file").ToLowerInvariant();
                if (!string.IsNullOrEmpty(filename)) attachments.Add(filename);
            }

            // Get the content string so we can verify that media files are referenced.
            string content = string.Empty;
            foreach (XmlElement xmlEle in xml.SelectNodes(it.IsStimulus ? "itemrelease/passage/content/stem" : "itemrelease/item/content/stem"))
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
                        ReportingUtility.ReportError(it, ErrorId.T0156, "Filename='{0}'", filename);
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
            foreach (
                XmlElement xmlEle in
                xml.SelectNodes(it.IsStimulus ? "itemrelease/passage/content/stem" : "itemrelease/item/content/stem"))
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
                           (char.IsWhiteSpace(content[index]) == false &&
                            !content[index].Equals("-") &&
                            !content[index].Equals("/")))
                        index++;

                    wordCount++;

                    // skip whitespace, hypen, slash and stand alone punctuation marks until next word
                    while (index < content.Length &&
                           (char.IsWhiteSpace(content[index]) ||
                            content[index].Equals("-") ||
                            content[index].Equals("/") ||
                            Regex.IsMatch(content[index].ToString(), @"[\p{P}]")))
                        index++;
                }
            }
            return wordCount;
        }

        private static string DepthOfKnowledgeFromMetadata(XmlNode xmlMetadata, XmlNamespaceManager xmlNamespaceManager)
        {
            return xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:DepthOfKnowledge", xmlNamespaceManager);
        }

        private static string MathematicalPracticeFromMetadata(XmlNode xmlMetadata, XmlNamespaceManager xmlNamespaceManager)
        {
           return xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:MathematicalPractice", xmlNamespaceManager);
        }

        private static string AllowCalculatorFromMetadata(XmlNode xmlMetadata, XmlNamespaceManager xmlNamespaceManager)
        {
            return xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AllowCalculator", xmlNamespaceManager);
        }

        private static string MaximumNumberOfPointsFromMetadata(XmlNode xmlMetadata,
            XmlNamespaceManager xmlNamespaceManager)
        {
            return xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:MaximumNumberOfPoints", xmlNamespaceManager);
        }

        bool ValidateManifest(FileFolder rootFolder)
        {
            try
            {
                if (!rootFolder.FileExists(cImsManifest))
                {
                    Console.WriteLine($"   Not a content package; '{cImsManifest}' must exist in root.");
                    ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0157);
                    return false;
                }

                // Optionally suppress manifest validation
                if (!Program.gValidationOptions.IsEnabled("mst"))
                {
                    return true;
                }

                // Load the manifest
                XmlDocument xmlManifest = new XmlDocument(sXmlNt);
                if (!TryLoadXml(rootFolder, cImsManifest, xmlManifest))
                {
                    ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0158, LoadXmlErrorDetail);
                    return true;
                }

                // Keep track of every resource id mentioned in the manifest
                HashSet<string> ids = new HashSet<string>();

                // Enumerate all resources in the manifest
                foreach (XmlElement xmlRes in xmlManifest.SelectNodes("ims:manifest/ims:resources/ims:resource", sXmlNs))
                {
                    string id = xmlRes.GetAttribute("identifier");
                    if (string.IsNullOrEmpty(id))
                        ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0159, $"Filename='{xmlRes.XpEvalE("ims: file / @href", sXmlNs)}'");
                    string filename = xmlRes.XpEval("ims:file/@href", sXmlNs);
                    if (string.IsNullOrEmpty(filename))
                    {
                        ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0160, $"ResourceId='{id}'");
                    }
                    else if (!rootFolder.FileExists(filename))
                    {
                        ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0098, $"ResourceId='{id}' Filename='{filename}'");
                    }

                    if (ids.Contains(id))
                    {
                        ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0095, $"ResourceId='{id}'");
                    }
                    else
                    {
                        ids.Add(id);
                    }

                    // Normalize the filename
                    filename = NormalizeFilenameInManifest(filename);
                    if (mFilenameToResourceId.ContainsKey(filename))
                    {
                        ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0109, $"ResourceId='{id}' Filename='{filename}'");
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
                            ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0161, $"ResourceId='{id}'");
                        }
                        else
                        {
                            string dependency = ToDependsOnString(id, dependsOnId);
                            if (mResourceDependencies.Contains(dependency))
                            {
                                ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0108, $"ResourceId='{id}' DependsOnId='{dependsOnId}'");
                            }
                            else
                            {
                                mResourceDependencies.Add(dependency);
                            }
                        }

                    }
                }

                if (mFilenameToResourceId.Count == 0)
                {
                    // Suppress error if the manifest is just one empty element
                    if (xmlManifest.ChildNodes.Count == 1 && xmlManifest.FirstChild.ChildNodes.Count == 0 && xmlManifest.FirstChild.Attributes.Count == 0)
                    {
                        return true; // Deliberately empty manifest
                    }
                    ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0162);
                    return true;
                }

                // Enumerate all files and check for them in the manifest
                {
                    foreach (FileFolder ff in rootFolder.Folders)
                    {
                        ValidateDirectoryInManifest(ff);
                    }
                }
            }
            catch (Exception err)
            {
                ReportingUtility.ReportError(Errors.ManifestItemId, err);
            }

            return true;
        }

        // Recursively check that files exist in the manifest
        private void ValidateDirectoryInManifest(FileFolder ff)
        {
            // See if this is an item or stimulus directory
            string itemId = null;
            if (ff.Name.StartsWith("item-", StringComparison.OrdinalIgnoreCase) || ff.Name.StartsWith("stim-", StringComparison.OrdinalIgnoreCase))
            {
                FileFile fi;
                if (ff.TryGetFile(string.Concat(ff.Name, ".xml"), out fi))
                {
                    var itemFileName = NormalizeFilenameInManifest(fi.RootedName);

                    if (!mFilenameToResourceId.TryGetValue(itemFileName, out itemId))
                    {
                        ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0103, $"ItemFilename='{itemFileName}'");
                        itemFileName = null;
                        itemId = null;
                    }
                }
            }

            foreach (var fi in ff.Files)
            {
                var filename = NormalizeFilenameInManifest(fi.RootedName);

                string resourceId;
                if (!mFilenameToResourceId.TryGetValue(filename, out resourceId))
                {
                    ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0104, $"Filename='{filename}'");
                }

                // If in an item, see if dependency is expressed
                else if (itemId != null && !string.Equals(itemId, resourceId, StringComparison.Ordinal))
                {
                    // Check for dependency
                    if (!mResourceDependencies.Contains(ToDependsOnString(itemId, resourceId)))
                        ReportingUtility.ReportError(Errors.ManifestItemId, ErrorId.T0163, $"ResourceId='{itemId}' DependesOnId='{resourceId}'");
                }
            }

            // Recurse
            foreach (var ffSub in ff.Folders)
            {
                ValidateDirectoryInManifest(ffSub);
            }
        }

        private static string NormalizeFilenameInManifest(string filename)
        {
            filename = filename.ToLowerInvariant().Replace('\\', '/');
            return (filename[0] == '/') ? filename.Substring(1) : filename;
        }

        private static string ToDependsOnString(string itemId, string dependsOnId)
        {
            return string.Concat(itemId, "~", dependsOnId);
        }

        private void SummaryReport(TextWriter writer)
        {
            uint elapsed = unchecked((uint)Environment.TickCount - (uint)mStartTicks);
            writer.WriteLine();
            writer.WriteLine("Elapsed: {0}.{1:d3}", elapsed / 1000, elapsed % 1000);

            if (ExitAfterSelect)
            {
                writer.WriteLine("Items: {0}", mItemQueue.Count);
                writer.WriteLine("Stimuli: {0}", mStimQueue.Count);
            }
            else
            {
                writer.WriteLine("Errors: {0}", ReportingUtility.ErrorCount);
                writer.WriteLine("Items: {0}", mItemCount);
                writer.WriteLine("Stimuli: {0}", mStimCount);
                writer.WriteLine("Word Lists: {0}", mWordlistCount);
                writer.WriteLine("Glossary Terms: {0}", mGlossaryTermCount);
                writer.WriteLine("Distinct Glossary Terms: {0}", mTermCounts.Count);
                writer.WriteLine();

                writer.WriteLine("Item Type Counts:");
                mTypeCounts.Dump(writer);
                writer.WriteLine();
                writer.WriteLine("Translation Counts:");
                mTranslationCounts.Dump(writer);
                writer.WriteLine();
                writer.WriteLine("Answer Key Counts:");
                mAnswerKeyCounts.Dump(writer);
                writer.WriteLine();
                writer.WriteLine("Glossary Terms Used in Wordlists:");
                mTermCounts.Dump(writer);
            }
            writer.WriteLine();
        }

        private static readonly char[] cCsvEscapeChars = {',', '"', '\'', '\r', '\n'};

        public static string CsvEncode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            if (text.IndexOfAny(cCsvEscapeChars) < 0) return text;
            return string.Concat("\"", text.Replace("\"", "\"\""), "\"");
        }

        public static string CsvEncodeExcel(string text)
        {
            return string.Concat("\"", text.Replace("\"", "\"\""), "\t\"");
        }

        public static string CsvEncode(params object[] args)
        {
            var b = new StringBuilder();
            bool first = true;
            foreach(var arg in args)
            {
                if (!first) b.Append(',');
                else first = false;

                if (arg != null) b.Append(CsvEncode(arg.ToString()));
            }
            return b.ToString();
        }
    }

    internal static class TabulatorHelp
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

        public static XmlNode NextNode(this XmlNode node, XmlNode withinSubtree = null)
        {
            if (node == null) throw new NullReferenceException("Null passed to NextNode.");

            // Try first child
            var next = node.FirstChild;
            if (next != null) return next;

            // Try next sibling
            next = node.NextSibling;
            if (next != null) return next;

            // Find nearest parent that has a sibling
            next = node;
            for (;;)
            {
                next = next.ParentNode;
                if (next == null) return null;

                // Apply subtree limit
                if (withinSubtree != null && Object.ReferenceEquals(withinSubtree, next))
                {
                    return null;
                }

                // Found?
                if (next.NextSibling != null) return next.NextSibling;
            }
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
            var space = str.IndexOfAny(cWhitespace);
            return (space > 0) ? str.Substring(0, space) : str;
        }

        public static int ParseLeadingInteger(this string str)
        {
            str = str.Trim();
            var i = 0;
            foreach (char c in str)
            {
                if (!char.IsDigit(c)) return i;
                i = (i * 10) + (c - '0');
            }
            return i;
        }
    }

    internal class XmlSubtreeEnumerable : IEnumerable<XmlNode>
    {
        XmlNode m_root;

        public XmlSubtreeEnumerable(XmlNode root)
        {
            m_root = root;
        }

        public IEnumerator<XmlNode> GetEnumerator()
        {
            return new XmlSubtreeEnumerator(m_root);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new XmlSubtreeEnumerator(m_root);
        }
    }

    internal class XmlSubtreeEnumerator : IEnumerator<XmlNode>
    {
        XmlNode m_root;
        XmlNode m_current;
        bool m_atEnd;

        public XmlSubtreeEnumerator(XmlNode root)
        {
            m_root = root;
            Reset();
        }

        public void Reset()
        {
            m_current = null;
            m_atEnd = false;
        }
        public XmlNode Current
        {
            get
            {
                if (m_current == null) throw new InvalidOperationException("");
                return m_current;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }

        public bool MoveNext()
        {
            if (m_atEnd) return false;
            if (m_current == null)
            {
                m_current = m_root.FirstChild;
                if (m_current == null)
                {
                    m_atEnd = true;
                }
            }
            else
            {
                XmlNode next = m_current.FirstChild;
                if (next == null)
                {
                    next = m_current.NextSibling;
                }
                if (next == null)
                {
                    next = m_current;
                    for (;;)
                    {
                        next = next.ParentNode;
                        if (Object.ReferenceEquals(m_root, next))
                        {
                            next = null;
                            m_atEnd = true;
                            break;
                        }
                        if (next.NextSibling != null)
                        {
                            next = next.NextSibling;
                            break;
                        }
                    }
                }
                m_current = next;
            }
            return m_current != null;
        }

        public void Dispose()
        {
            // Nothing to dispose.
        }

    }
}
