using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NLog;
using TabulateSmarterTestContentPackage.Extensions;
using TabulateSmarterTestContentPackage.Extractors;
using TabulateSmarterTestContentPackage.Mappers;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
using TabulateSmarterTestContentPackage.Validators;

namespace TabulateSmarterTestContentPackage
{
    public class Tabulator : IDisposable
    {

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        const string cImsManifest = "imsmanifest.xml";
        const string cItemTypeStim = "stim";
        const string cItemTypeWordlist = "wordList";
        const string cItemTypeTutorial = "tut";

        static NameTable sXmlNt;
        static XmlNamespaceManager sXmlNs;
        static Dictionary<string, int> sExpectedTranslationsIndex;

        static string[] sExpectedTranslations = {
            "arabicGlossary",
            "cantoneseGlossary",
            "esnGlossary",
            "koreanGlossary",
            "mandarinGlossary",
            "punjabiGlossary",
            "russianGlossary",
            "tagalGlossary",
            "ukrainianGlossary",
            "vietnameseGlossary"
        };
        static int sExpectedTranslationsBitflags;

        const string cStimulusInteractionType = "Stimulus";

        static readonly HashSet<string> sValidWritingTypes = new HashSet<string>(
            new[] {
                "Explanatory",
                "Opinion",
                "Informative",
                "Argumentative",
                "Narrative"
            });

        static readonly HashSet<string> sValidClaims = new HashSet<string>(
            new[] {
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
        const string cIdReportFn = "IdReport.csv";

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
        Dictionary<ShaHash, ItemIdentifier> mRubrics = new Dictionary<ShaHash, ItemIdentifier>();
        string mReportPathPrefix;
        TextWriter mItemReport;
        TextWriter mStimulusReport;
        TextWriter mWordlistReport;
        TextWriter mGlossaryReport;
        TextWriter mSummaryReport;
        TextWriter mIdReport;

        static Tabulator()
        {
            sXmlNt = new NameTable();
            sXmlNs = new XmlNamespaceManager(sXmlNt);
            sXmlNs.AddNamespace("sa", "http://www.smarterapp.org/ns/1/assessment_item_metadata");
            sXmlNs.AddNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instance");
            sXmlNs.AddNamespace("ims", "http://www.imsglobal.org/xsd/apip/apipv1p0/imscp_v1p1");

            sExpectedTranslationsIndex = new Dictionary<string, int>(sExpectedTranslations.Length);
            sExpectedTranslationsBitflags = 0;
            for (var i = 0; i < sExpectedTranslations.Length; ++i)
            {
                sExpectedTranslationsIndex.Add(sExpectedTranslations[i], i);
                sExpectedTranslationsBitflags |= (1 << i);
            }
        }

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
            if (mIdReport == null)
            {
                mIdReport = new StreamWriter(string.Concat(mReportPathPrefix, cIdReportFn), false, Encoding.UTF8);
                mIdReport.WriteLine("BankKey,ItemId,ItemType");
            }
            foreach (ItemIdentifier ii in mItemQueue)
            {
                mIdReport.WriteLine(string.Join(",", ii.BankKey, ii.ItemId, ii.ItemType));
            }

            foreach (ItemIdentifier ii in mStimQueue)
            {
                mIdReport.WriteLine(string.Join(",", ii.BankKey, ii.ItemId, ii.ItemType));
            }
        }

        // Initialize all files and collections for a tabulation run
        private void Initialize()
        {
            mStartTicks = Environment.TickCount;

            // Prep the error report
            ReportingUtility.ErrorReportPath = string.Concat(mReportPathPrefix, cErrorReportFn);
            File.Delete(ReportingUtility.ErrorReportPath); // Delete does not throw exception if file does not exist.

            // Delete any existing reports
            File.Delete(mReportPathPrefix + cIdReportFn);
            File.Delete(mReportPathPrefix + cItemReportFn);
            File.Delete(mReportPathPrefix + cStimulusReportFn);
            File.Delete(mReportPathPrefix + cWordlistReportFn);
            File.Delete(mReportPathPrefix + cGlossaryReportFn);

            // Prep the summary report
            mSummaryReport = new StreamWriter(string.Concat(mReportPathPrefix, cSummaryReportFn), false, Encoding.UTF8);

            // If tabulation not being suppressed, open the other reports
            if (!ExitAfterSelect)
            {
                mItemReport = new StreamWriter(string.Concat(mReportPathPrefix, cItemReportFn), false, Encoding.UTF8);
                // DOK is "Depth of Knowledge"
                // In the case of multiple standards/claims/targets, these headers will not be sufficient
                // TODO: Add CsvHelper library to allow expandable headers
                mItemReport.WriteLine("Folder,BankKey,ItemId,ItemType,Version,Subject,Grade,AnswerKey,AsmtType,WordlistId,ASL," +
                                      "BrailleType,Translation,Media,Size,DOK,AllowCalculator,MathematicalPractice,MaxPoints," +
                                      "CommonCore,ClaimContentTarget,SecondaryCommonCore,SecondaryClaimContentTarget, CAT_MeasurementModel," +
                                      "CAT_ScorePoints,CAT_Dimension,CAT_Weight,CAT_Parameters, PP_MeasurementModel," +
                                      "PP_ScorePoints,PP_Dimension,PP_Weight,PP_Parameters");

                mStimulusReport = new StreamWriter(string.Concat(mReportPathPrefix, cStimulusReportFn), false, Encoding.UTF8);
                mStimulusReport.WriteLine("Folder,BankKey,StimulusId,Version,Subject,WordlistId,ASL,BrailleType,Translation,Media,Size,WordCount");

                mWordlistReport = new StreamWriter(string.Concat(mReportPathPrefix, cWordlistReportFn), false, Encoding.UTF8);
                mWordlistReport.WriteLine("Folder,BankKey,WIT_ID,RefCount,TermCount,MaxGloss,MinGloss,AvgGloss");

                mGlossaryReport = new StreamWriter(string.Concat(mReportPathPrefix, cGlossaryReportFn), false, Encoding.UTF8);
                mGlossaryReport.WriteLine(Program.gValidationOptions.IsEnabled("gtr")
                    ? "Folder,BankKey,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize,Text"
                    : "Folder,BankKey,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize");
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
                if (mIdReport != null)
                {
                    mIdReport.Dispose();
                    mIdReport = null;
                }
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
                    mItemQueue.Sort();
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
                            ReportingUtility.ReportError(ii, ErrorCategory.Exception, ErrorSeverity.Severe, err.GetType().Name, err.ToString());
                        }
                    }

                    // Process stimuli
                    mStimQueue.Sort();
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
                            ReportingUtility.ReportError(ii, ErrorCategory.Exception, ErrorSeverity.Severe, err.GetType().Name, err.ToString());
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
                            ReportingUtility.ReportError(ii, ErrorCategory.Exception, ErrorSeverity.Severe, err.GetType().Name, err.ToString());
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
                            ReportingUtility.ReportError(ii, ErrorCategory.Exception, ErrorSeverity.Severe, err.GetType().Name, err.ToString());
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
                ReportingUtility.ReportError(string.Empty, ErrorCategory.Exception, ErrorSeverity.Severe, err.GetType().Name, err.ToString());
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
            Console.Error.Write($"   {mProgressCount - mTransferCount} of {mItemQueue.Count + mStimQueue.Count + mTutorialQueue.Count + mWordlistQueue.Count - mTransferCount} {c_progressChars[s_progressSpin & 3]}\r");
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
                return;
            }

            // Get the item folder
            FileFolder ffItem;
            if (!mPackage.TryGetItem(ii, out ffItem))
            {
                ReportingUtility.ReportError(ii, ErrorCategory.Item, ErrorSeverity.Severe, "Item not found in package.");
                return;
            }

            // Read the item XML
            var xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(ffItem, ffItem.Name + ".xml", xml))
            {
                ReportingUtility.ReportError(ffItem, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid item file.", LoadXmlErrorDetail);
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
                ReportingUtility.ReportError(ffItem, ErrorCategory.Item, ErrorSeverity.Severe, "Item type not specified.", LoadXmlErrorDetail);
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
                return;
            }

            // If tutorial, transfer to the tutorial queue
            if (ii.ItemType.Equals(cItemTypeTutorial))
            {
                if (mTutorialQueue.Add(ii))
                {
                    ++mTransferCount;
                }
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
                    ReportingUtility.ReportError(it, ErrorCategory.Unsupported, ErrorSeverity.Severe, "Item type is not fully supported by the open source TDS.", "itemType='{0}'", it.ItemType);
                    TabulateInteraction(it);
                    break;

                default:
                    ReportingUtility.ReportError(it, ErrorCategory.Unsupported, ErrorSeverity.Severe, "Unexpected item type.", "itemType='{0}'", it.ItemType);
                    break;
            }
        }

        private void TabulateInteraction(ItemContext it)
        {
            // Read the item XML
            var xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid item file.", LoadXmlErrorDetail);
                return;
            }

            IList<ItemScoring> scoringInformation = new List<ItemScoring>();
            // Load metadata
            var xmlMetadata = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, "metadata.xml", xmlMetadata))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid metadata.xml.",
                    LoadXmlErrorDetail);
            }
            else
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
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Incorrect metadata <InteractionType>.", "InteractionType='{0}' Expected='{1}'", metaItemType, it.ItemType.ToUpperInvariant());

            // Get the version
            var version = xml.XpEvalE("itemrelease/item/@version");

            // Subject
            var subject = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_item_subject']/val");
            var metaSubject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            if (string.IsNullOrEmpty(subject))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Attribute, ErrorSeverity.Tolerable, "Missing subject in item attributes (itm_item_subject).");
                subject = metaSubject;
                if (string.IsNullOrEmpty(subject))
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Missing subject in item metadata.");
            }
            else
            {
                if (!string.Equals(subject, metaSubject, StringComparison.Ordinal))
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Subject mismatch between item and metadata.", "ItemSubject='{0}' MetadataSubject='{1}'", subject, metaSubject);
            }

            // AllowCalculator
            var allowCalculator = AllowCalculatorFromMetadata(xmlMetadata, sXmlNs);
            if (string.IsNullOrEmpty(allowCalculator) && 
                (string.Equals(metaSubject, "MATH", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(subject, "MATH", StringComparison.OrdinalIgnoreCase)))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded, "Allow Calculator field not present for MATH subject item");
            }

            // MathematicalPractice
            var mathematicalPractice = MathematicalPracticeFromMetadata(xmlMetadata, sXmlNs);
            if (string.IsNullOrEmpty(mathematicalPractice) &&
                (string.Equals(metaSubject, "MATH", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(subject, "MATH", StringComparison.OrdinalIgnoreCase)))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded, "Mathematical Practice field not present for MATH subject item");
            }

            // MaximumNumberOfPoints
            int testInt;
            var maximumNumberOfPoints = MaximumNumberOfPointsFromMetadata(xmlMetadata, sXmlNs);
            if (string.IsNullOrEmpty(maximumNumberOfPoints) || !int.TryParse(maximumNumberOfPoints, out testInt))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded, "MaximumNumberOfPoints field not present in metadata");
            }

            // Grade
            var grade = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_att_Grade']/val").Trim();
            var metaGrade = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:IntendedGrade", sXmlNs);
            if (string.IsNullOrEmpty(grade))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Attribute, ErrorSeverity.Tolerable, "Missing grade in item attributes (itm_att_Grade).");
                grade = metaGrade;
                if (string.IsNullOrEmpty(grade))
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Missing <IntendedGrade> in item metadata.");
            }
            else
            {
                if (!string.Equals(grade, metaGrade, StringComparison.Ordinal))
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Grade mismatch between item and metadata.", "ItemGrade='{0}', MetadataGrade='{1}'", grade, metaGrade);
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
                        ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe, "Machine scoring file not found.", "Filename='{0}'", machineScoringFilename);
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
                            ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe, "Unexpected MC answer key attribute.", "itm_att_Answer Key='{0}'", answerKeyValue);
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
                            if (!validAnswer) ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe, "Unexpected MS answer attribute.", "itm_att_Answer Key='{0}'", answerKeyValue);
                            answerKey = answerKeyValue;
                            scoringType = ScoringType.Basic;
                        }
                        break;

                    case "EBSR":    // Evidence-based selected response
                        {
                            metadataExpected = "Automatic with Key(s)";
                            if (answerKeyValue.Length != 1 || answerKeyValue[0] < 'A' || answerKeyValue[0] > 'Z')
                                ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe, "Unexpected EBSR answer key attribute.", "itm_att_Answer Key='{0}'", answerKeyValue);

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
                                ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Benign, "Missing EBSR answer key part II attribute.");
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
                                    ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe, "Unexpected EBSR Key Part II attribute.", "itm_att_Answer Key (Part II)='{0}'", answerKeyPart2);
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
                        metadataExpected = (machineScoringFilename != null) ? "Automatic with Machine Rubric" : "HandScored";
                        answerKey = machineScoringType;
                        if (!string.Equals(answerKeyValue, it.ItemType.ToUpperInvariant()))
                            ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe, "Unexpected answer key attribute.", "Value='{0}' Expected='{1}'", answerKeyValue, it.ItemType.ToUpperInvariant());
                        scoringType = ScoringType.Qrx;
                        break;

                    case "er":          // Extended-Response
                    case "sa":          // Short Answer
                    case "wer":         // Writing Extended Response
                        metadataExpected = "HandScored";
                        if (!string.Equals(answerKeyValue, it.ItemType.ToUpperInvariant()))
                            ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Tolerable, "Unexpected answer key attribute.", "Value='{0}' Expected='{1}'", answerKeyValue, it.ItemType.ToUpperInvariant());
                        answerKey = ScoringType.Hand.ToString();
                        scoringType = ScoringType.Hand;
                        break;

                    default:
                        ReportingUtility.ReportError(it, ErrorCategory.Unsupported, ErrorSeverity.Benign, "Validation of scoring keys for this type is not supported.");
                        answerKey = string.Empty;
                        scoringType = ScoringType.Basic;    // We don't really know.
                        break;
                }

                // Count the answer key types
                mAnswerKeyCounts.Increment(string.Concat(it.ItemType, " '", answerKey, "'"));

                // Check Scoring Engine metadata
                if (metadataExpected != null && !string.Equals(metadataScoringEngine, metadataExpected, StringComparison.Ordinal))
                {
                    if (string.Equals(metadataScoringEngine, metadataExpected, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign, "Capitalization error in ScoringEngine metadata.", "Found='{0}' Expected='{1}'", metadataScoringEngine, metadataExpected);
                    }
                    else
                    {
                        // If first word of scoring engine metadata is the same (e.g. both are "Automatic" or both are "HandScored") then error is benign, otherwise error is tolerable
                        if (string.Equals(metadataScoringEngine.FirstWord(), metadataExpected.FirstWord(), StringComparison.OrdinalIgnoreCase))
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign, "Incorrect ScoringEngine metadata.", "Found='{0}' Expected='{1}'", metadataScoringEngine, metadataExpected);
                        }
                        else
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Automatic/HandScored scoring metadata error.", "Found='{0}' Expected='{1}'", metadataScoringEngine, metadataExpected);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(machineScoringFilename) && scoringType != ScoringType.Qrx)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Benign,
                        "Unexpected machine scoring file found for HandScored item type.", "Filename='{0}'",
                        machineScoringFilename);
                }

                // Check for unreferenced machine scoring files
                foreach (var fi in it.FfItem.Files)
                {
                    if (string.Equals(fi.Extension, ".qrx", StringComparison.OrdinalIgnoreCase)
                        && (machineScoringFilename == null || !string.Equals(fi.Name, machineScoringFilename, StringComparison.OrdinalIgnoreCase)))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe, 
                            "Machine scoring file found but not referenced in <MachineRubric> element.", 
                            "Filename='{0}'", fi.Name);
                    }
                }

            } // Answer key

            // Rubric
            // If non-embedded answer key (either hand-scored or QRX scoring but not EBSR type check for a rubric (human scoring guidance)
            // We only care about english rubrics (at least for the present)
            if (scoringType != ScoringType.Basic && !it.ItemType.Equals("EBSR", StringComparison.OrdinalIgnoreCase))
            {
                using (var rubricStream = new MemoryStream())
                {
                    if (!RubricExtractor.ExtractRubric(xml, rubricStream))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Tolerable,
                            "Rubric is missing for Hand-scored or QRX-scored item.",
                            $"AnswerKey: '{answerKey}'");
                    }
                    else
                    {
                        rubricStream.Position = 0;
                        var hash = new ShaHash(rubricStream);

                        // See if we've aready encountered an identical rubric
                        ItemIdentifier otherId;
                        if (mRubrics.TryGetValue(hash, out otherId))
                        {
                            // If the dictionary shows a non-blank itemId, report the error on the other item with a matching hash.
                            if (!otherId.Equals(cBlankItemId))
                            {
                                ReportingUtility.ReportError(otherId, ErrorCategory.Item, ErrorSeverity.Tolerable,
                                    "Rubric is likely to be a blank template. Identical to the rubric of at least one other item.", $"rubricHash={hash}");

                                // Set the id to blanks so that we don't report repeated errors on the prior item
                                mRubrics[hash] = cBlankItemId;
                            }

                            // Report the error on the current item.
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable,
                                "Rubric is likely to be a blank template. Identical to the rubric of at least one other item.", $"rubricHash={hash}");
                        }
                        else
                        {
                            mRubrics.Add(hash, new ItemIdentifier(it));
                        }
                    }
                }
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
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded, "PerformanceTaskComponentItem metadata should be 'Y' or 'N'.", "Value='{0}'", meta);
                }
            }

            // Standards Alignment
            var primaryStandards = new List<ItemStandard>();
            var secondaryStandards = new List<ItemStandard>();
            if (!string.IsNullOrEmpty(xmlMetadata.OuterXml))
            {
                primaryStandards = ItemStandardExtractor.Extract(xmlMetadata.MapToXElement()).ToList();
                secondaryStandards =
                    ItemStandardExtractor.Extract(xmlMetadata.MapToXElement(), "SecondaryStandard").ToList();
            }
            if (primaryStandards.Any(x => string.IsNullOrEmpty(x.Standard)))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded, "Common Core Standard not included in PrimaryStandard metadata.");
            }

            // Validate claim
            if (primaryStandards.Any(x => !sValidClaims.Contains(x.Claim)))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded, "Unexpected claim value (Should be 1, 2, 3, or 4 with possible suffix).", "Claim='{0}'", primaryStandards.First(x => !sValidClaims.Contains(x.Claim)).Claim);
            }

            // Validate target grade suffix (Generating lots of errors. Need to follow up.)
            primaryStandards.ForEach(x =>
                    {
                        var parts = x.Target.Split('-');
                        if (parts.Length == 2 &&
                            !string.Equals(parts[1].Trim(), grade, StringComparison.OrdinalIgnoreCase))
                        {
                            ReportingUtility.ReportError("tgs", it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                                "Target suffix indicates a different grade from item attribute.",
                                "ItemAttributeGrade='{0}' TargetSuffixGrade='{1}'", grade, parts[1]);
                        }
                    }
                )
            ;

            var standardClaimTarget = new ReportingStandard(primaryStandards, secondaryStandards);

            // Validate content segments
            var wordlistId = ValidateContentAndWordlist(it, xml);

            // Other content validation
            // TODO: this should be combined with ValidateContentAndWordlist
            if (Program.gValidationOptions.IsEnabled("cdt"))
            {
                var isCDataValid = CDataExtractor.ExtractCData(new XDocument().LoadXml(xml.OuterXml).Root)
                    .Select(
                        x =>
                            CDataValidator.IsValid(x, it,
                                x.Parent.Name.LocalName.Equals("val", StringComparison.OrdinalIgnoreCase)
                                    ? ErrorSeverity.Benign
                                    : ErrorSeverity.Degraded)).ToList();
            }

            // ASL
            var asl = GetAslType(it, xml, xmlMetadata);

            // BrailleType
            var brailleType = GetBrailleType(it, xml, xmlMetadata);

            // Translation
            var translation = GetTranslation(it, xml, xmlMetadata);

            // Media
            var media = GetMedia(it, xml);

            // Size
            var size = GetItemSize(it);

            // DepthOfKnowledge
            var depthOfKnowledge = DepthOfKnowledgeFromMetadata(xmlMetadata, sXmlNs);

            // Check for silencing tags
            if (Program.gValidationOptions.IsEnabled("tss"))
            {
                var primaryStandard = primaryStandards.FirstOrDefault();
                if (primaryStandard != null && HasTtsSilencingTags(xml) 
                    && !primaryStandard.Claim.Split('-').FirstOrDefault().Equals("2")
                    && !primaryStandard.Target.Split('-').FirstOrDefault().Equals("9"))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable, "Item has improper TTS Silencing Tag", "subject='{0}' claim='{1}' target='{2}'", subject, 
                        primaryStandard.Claim, primaryStandard.Target);
                }
            }

            if (!it.IsStimulus && Program.gValidationOptions.IsEnabled("asl") && CheckForAttachment(it, xml, "ASL", "MP4"))
            {
                AslVideoValidator.Validate(it, xml);
            }

            Logger.Info($"Tabulating {it.ItemId}");

            var scoringSeparation = scoringInformation.GroupBy(
                x => !string.IsNullOrEmpty(x.Domain) && x.Domain.Equals("paper", StringComparison.OrdinalIgnoreCase)).ToList();

            // Folder,BankKey,ItemId,ItemType,Version,Subject,Grade,AnswerKey,AsmtType,WordlistId,ASL,BrailleType,Translation,Media,Size,DepthOfKnowledge,AllowCalculator,
            // MathematicalPractice, MaxPoints, CommonCore, ClaimContentTarget, SecondaryCommonCore, SecondaryClaimContentTarget, measurementmodel, scorepoints,
            // dimension, weight, parameters
            mItemReport.WriteLine(string.Join(",", CsvEncode(it.FolderDescription), it.BankKey.ToString(), it.ItemId.ToString(), CsvEncode(it.ItemType), CsvEncode(version), CsvEncode(subject), 
                CsvEncode(grade), CsvEncode(answerKey), CsvEncode(assessmentType), CsvEncode(wordlistId), 
                CsvEncode(asl), CsvEncode(brailleType), CsvEncode(translation), CsvEncode(media), size.ToString(), CsvEncode(depthOfKnowledge), CsvEncode(allowCalculator), 
                CsvEncode(mathematicalPractice), CsvEncode(maximumNumberOfPoints), CsvEncode(standardClaimTarget.PrimaryCommonCore), CsvEncode(standardClaimTarget.PrimaryClaimsContentTargets),
                CsvEncode(standardClaimTarget.SecondaryCommonCore), CsvEncode(standardClaimTarget.SecondaryClaimsContentTargets), 
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

            // Points
            {
                var itemPoint = xml.XpEval("itemrelease/item/attriblist/attrib[@attid='itm_att_Item Point']/val");
                if (string.IsNullOrEmpty(itemPoint))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable, "Item Point attribute (item_att_Item Point) not found.");
                }
                else
                {
                    // Item Point attribute may have a suffix such as "pt", "pt.", " pt", " pts" and other variations.
                    // TODO: In seeking consistency, we may make this more picky in the future.
                    itemPoint = itemPoint.Trim();
                    if (!char.IsDigit(itemPoint[0]))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable, "Item Point attribute does not begin with an integer.", "itm_att_Item Point='{0}'", itemPoint);
                    }
                    else
                    {
                        var points = itemPoint.ParseLeadingInteger();

                        // See if matches MaximumNumberOfPoints (defined as optional in metadata)
                        var metaPoint = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:MaximumNumberOfPoints", sXmlNs);
                        if (metaPoint == null)
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "MaximumNumberOfPoints not found in metadata.");
                        }
                        else
                        {
                            int mpoints;
                            if (!int.TryParse(metaPoint, out mpoints))
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Metadata MaximumNumberOfPoints value is not integer.", "MaximumNumberOfPoints='{0}'", metaPoint);
                            }
                            else if (mpoints != points)
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Metadata MaximumNumberOfPoints does not match item point attribute.", "MaximumNumberOfPoints='{0}' itm_att_Item Point='{0}'", mpoints, points);
                            }
                        }

                        // See if matches ScorePoints (defined as optional in metadata)
                        var scorePoints = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:ScorePoints", sXmlNs);
                        if (scorePoints == null)
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign, "ScorePoints not found in metadata.");
                        }
                        else
                        {
                            scorePoints = scorePoints.Trim();
                            if (scorePoints[0] == '"')
                                scorePoints = scorePoints.Substring(1);
                            else
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "ScorePoints value missing leading quote.");
                            if (scorePoints[scorePoints.Length - 1] == '"')
                                scorePoints = scorePoints.Substring(0, scorePoints.Length - 1);
                            else
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "ScorePoints value missing trailing quote.");

                            var maxspoints = -1;
                            var minspoints = 100000;
                            foreach (string sp in scorePoints.Split(','))
                            {
                                int spoints;
                                if (!int.TryParse(sp.Trim(), out spoints))
                                {
                                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Metadata ScorePoints value is not integer.", "ScorePoints='{0}' value='{1}'", scorePoints, sp);
                                }
                                else if (spoints < 0 || spoints > points)
                                {
                                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Severe, "Metadata ScorePoints value is out of range.", "ScorePoints='{0}' value='{1}' min='0' max='{2}'", scorePoints, spoints, points);
                                }
                                else
                                {
                                    if (maxspoints < spoints)
                                    {
                                        maxspoints = spoints;
                                    }
                                    else
                                    {
                                        ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign, "Metadata ScorePoints are not in ascending order.", "ScorePoints='{0}'", scorePoints);
                                    }
                                    if (minspoints > spoints) minspoints = spoints;
                                }
                            }
                            if (minspoints > 0) ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign, "Metadata ScorePoints doesn't include a zero score.", "ScorePoints='{0}'", scorePoints);
                            if (maxspoints < points) ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Metadata ScorePoints doesn't include a maximum score.", "ScorePoints='{0}' max='{1}'", scorePoints, points);
                        }
                    }
                }
            }

            // Retrieve the stimulus ID (and record the dependency)
            var stimId = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='stm_pass_id']/val");
            if (stimId == null)
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "PT Item missing associated passage ID (stm_pass_id).");
            }

            if (!string.IsNullOrEmpty(stimId))
            {
                var metaStimId = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AssociatedStimulus", sXmlNs);
                if (!string.Equals(stimId, metaStimId, StringComparison.OrdinalIgnoreCase))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Item stimulus ID doesn't match metadata AssociatedStimulus.", "Item stm_pass_id='{0}' Metadata AssociatedStimulus='{1}'", stimId, metaStimId);
                }

                int nStimId;
                if (!int.TryParse(stimId, out nStimId))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Item stimulus ID is not an integer.", $"Item stm_pass_id='{stimId}'");
                }
                else
                {
                    // Look for the stimulus
                    var iiStimulus = new ItemIdentifier(cItemTypeStim, it.BankKey, nStimId);
                    if (!mPackage.ItemExists(iiStimulus))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Item stimulus not found.", "StimulusId='{0}'", stimId);
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
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "PT Item missing associated passage ID (stm_pass_id).");
                }

                // PtSequence
                int seq;
                var ptSequence = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PtSequence", sXmlNs);
                if (ptSequence == null)
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded, "Metadata for PT item is missing <PtSequence> element.");
                else if (!int.TryParse(ptSequence.Trim(), out seq))
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded, "Metadata <PtSequence> is not an integer.", "PtSequence='{0}'", ptSequence);

                // PtWritingType Metadata (defined as optional in metadata but we'll still report a benign error if it's not on PT WER items)
                if (string.Equals(it.ItemType, "wer", StringComparison.OrdinalIgnoreCase))
                {
                    var ptWritingType = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PtWritingType", sXmlNs);
                    if (ptWritingType == null)
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign, "Metadata for PT item is missing <PtWritingType> element.");
                    }
                    else
                    {
                        ptWritingType = ptWritingType.Trim();
                        if (!sValidWritingTypes.Contains(ptWritingType))
                        {
                            // Fix capitalization
                            var normalized = string.Concat(ptWritingType.Substring(0, 1).ToUpperInvariant(), ptWritingType.Substring(1).ToLowerInvariant());

                            // Report according to type of error
                            if (!sValidWritingTypes.Contains(normalized))
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign, "PtWritingType metadata has invalid value.", "PtWritingType='{0}'", ptWritingType);
                            else
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign, "Capitalization error in PtWritingType metadata.", "PtWritingType='{0}' expected='{1}'", ptWritingType, normalized);
                        }
                    }
                }
            } // if Performance Task

            // Check for tutorial
            {
                var tutorialId = xml.XpEval("itemrelease/item/tutorial/@id");
                if (tutorialId == null)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded, "Tutorial id missing from item.");
                }
                else if (Program.gValidationOptions.IsEnabled("trd"))
                {
                    var bankKey = xml.XpEval("itemrelease/item/tutorial/@bankkey");

                    // Look for the tutorial
                    var iiTutorial = new ItemIdentifier(cItemTypeTutorial, bankKey, tutorialId);
                    if (!mPackage.ItemExists(iiTutorial))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Tutorial not found.", "TutorialId='{0}'", tutorialId);
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
            }
        } // TabulateInteraction

        void TabulateStimulus(ItemIdentifier ii)
        {
            // Get the item context
            ItemContext it;
            if (!ItemContext.TryCreate(mPackage, ii, out it))
            {
                ReportingUtility.ReportError(ii, ErrorCategory.Item, ErrorSeverity.Severe, "Stimulus not found in package.");
                return;
            }

            // Count the stimulus
            ++mStimCount;

            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid item file.", LoadXmlErrorDetail);
                return;
            }

            // Load the metadata
            XmlDocument xmlMetadata = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, "metadata.xml", xmlMetadata))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid metadata.xml.", LoadXmlErrorDetail);
            }

            // Check interaction type
            string metaItemType = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:InteractionType", sXmlNs);
            if (!string.Equals(metaItemType, cStimulusInteractionType, StringComparison.Ordinal))
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Incorrect metadata <InteractionType>.", "InteractionType='{0}' Expected='{1}'", metaItemType, cStimulusInteractionType);

            // Get the version
            string version = xml.XpEvalE("itemrelease/passage/@version");

            // Subject
            string subject = xml.XpEvalE("itemrelease/passage/attriblist/attrib[@attid='itm_item_subject']/val");
            string metaSubject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            if (string.IsNullOrEmpty(subject))
            {
                // For the present, we don't expect the subject in the item attributes on passages
                //ReportingUtility.ReportError(it, ErrorCategory.Attribute, ErrorSeverity.Tolerable, "Missing subject in item attributes (itm_item_subject).");
                subject = metaSubject;
                if (string.IsNullOrEmpty(subject))
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Missing subject in item metadata.");
            }
            else
            {
                if (!string.Equals(subject, metaSubject, StringComparison.Ordinal))
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Subject mismatch between item and metadata.", "ItemSubject='{0}' MetadataSubject='{1}'", subject, metaSubject);
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
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded, "PerformanceTaskComponentItem metadata should be 'Y' or 'N'.", "Value='{0}'", meta);
                }
            }
            */

            // Validate content segments
            string wordlistId = ValidateContentAndWordlist(it, xml);

            // ASL
            string asl = GetAslType(it, xml, xmlMetadata);

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

            // Folder,BankKey,StimulusId,Version,Subject,WordlistId,ASL,BrailleType,Translation,Media,Size,WordCount
            mStimulusReport.WriteLine(string.Join(",", CsvEncode(it.FolderDescription), it.BankKey.ToString(), it.ItemId.ToString(), CsvEncode(version), CsvEncode(subject), CsvEncode(wordlistId), CsvEncode(asl), CsvEncode(brailleType), CsvEncode(translation), CsvEncode(media), size.ToString(), wordCount.ToString()));

        } // TabulateStimulus

        
        void TabulateTutorial(ItemIdentifier ii)
        {
            // Get the item context
            ItemContext it;
            if (!ItemContext.TryCreate(mPackage, ii, out it))
            {
                ReportingUtility.ReportError(ii, ErrorCategory.Item, ErrorSeverity.Severe, "Tutorial not found in package.");
                return;
            }

            // Count the item
            ++mItemCount;
            mTypeCounts.Increment(cItemTypeTutorial);

            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid item file.", LoadXmlErrorDetail);
                return;
            }

            // Read the metadata
            XmlDocument xmlMetadata = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, "metadata.xml", xmlMetadata))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid metadata.xml.", LoadXmlErrorDetail);
            }

            // Get the version
            string version = xml.XpEvalE("itemrelease/item/@version");

            // Subject
            string subject = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_item_subject']/val");
            string metaSubject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            if (string.IsNullOrEmpty(subject))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Attribute, ErrorSeverity.Tolerable, "Missing subject in item attributes (itm_item_subject).");
                subject = metaSubject;
                if (string.IsNullOrEmpty(subject))
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Missing subject in item metadata.");
            }
            else
            {
                if (!string.Equals(subject, metaSubject, StringComparison.Ordinal))
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Subject mismatch between item and metadata.", "ItemSubject='{0}' MetadataSubject='{1}'", subject, metaSubject);
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

            // Validate content segments
            var wordlistId = ValidateContentAndWordlist(it, xml);

            // ASL
            var asl = GetAslType(it, xml, xmlMetadata);

            // BrailleType
            var brailleType = GetBrailleType(it, xml, xmlMetadata);

            // Translation
            var translation = GetTranslation(it, xml, xmlMetadata);

            // Folder,BankKey,ItemId,ItemType,Version,Subject,Grade,AnswerKey,AsmtType,WordlistId,ASL,BrailleType,Translation,Media,Size,DepthOfKnowledge,AllowCalculator,MathematicalPractice, MaxPoints, 
            // CommonCore, ClaimContentTarget, SecondaryCommonCore, SecondaryClaimContentTarget, CAT_MeasurementModel,
            // CAT_ScorePoints, CAT_Dimension, CAT_Weight,CAT_Parameters, PP_MeasurementModel
            // PP_ScorePoints,PP_Dimension,PP_Weight,PP_Parameters
            mItemReport.WriteLine(string.Join(",", CsvEncode(it.FolderDescription), it.BankKey.ToString(), it.ItemId.ToString(), CsvEncode(it.ItemType), CsvEncode(version),
                CsvEncode(subject), CsvEncode(grade), CsvEncode(answerKey), CsvEncode(assessmentType), CsvEncode(wordlistId), CsvEncode(asl), CsvEncode(brailleType), CsvEncode(translation),
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));

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
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Dangling reference to attached file that does not exist.", "attachType='{0}' Filename='{1}'", attachType, fileName);
                return false;
            }

            var extension = Path.GetExtension(fileName);
            if (extension.Length > 0) extension = extension.Substring(1); // Strip leading "."
            if (!string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded, "Unexpected extension for attached file.", "attachType='{0}' extension='{1}' expected='{2}' filename='{3}'", attachType, extension, expectedExtension, fileName);
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
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Benign, "Unreferenced file found.", "fileType='{0}', filename='{1}'", fileType, file.Name);
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
                ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign, "Item not found in manifest.");
            }

            // Look up dependency in the manifest
            string dependencyResourceId = null;
            if (!mFilenameToResourceId.TryGetValue(NormalizeFilenameInManifest(dependencyFilename), out dependencyResourceId))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign, dependencyType + " not found in manifest.", "DependencyFilename='{0}'", dependencyFilename);
            }

            // Check for dependency in manifest
            if (!string.IsNullOrEmpty(itemResourceId) && !string.IsNullOrEmpty(dependencyResourceId))
            {
                if (!mResourceDependencies.Contains(ToDependsOnString(itemResourceId, dependencyResourceId)))
                    ReportingUtility.ReportError("pmd", it, ErrorCategory.Manifest, ErrorSeverity.Benign, string.Format("Manifest does not record dependency between item and {0}.", dependencyType), "ItemResourceId='{0}' {1}ResourceId='{2}'", itemResourceId, dependencyType, dependencyResourceId);
            }
        }

        private string GetAslType(ItemContext it, XmlDocument xml, XmlDocument xmlMetadata)
        {
            var aslFound = CheckForAttachment(it, xml, "ASL", "MP4");
            if (!aslFound)
            {
                ReportUnexpectedFiles(it, "ASL video", "^item_{0}_ASL", it.ItemId);
            }

            var aslInMetadata = string.Equals(xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AccessibilityTagsASLLanguage", sXmlNs), "Y", StringComparison.OrdinalIgnoreCase);
            if (aslInMetadata && !aslFound) ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Severe, "Item metadata specifies ASL but no ASL in item.");
            if (!aslInMetadata && aslFound) ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable, "Item has ASL but not indicated in the metadata.");

            return (aslFound && aslInMetadata) ? "MP4" : string.Empty;
        }

        public static string GetBrailleType(ItemContext it, XmlDocument xml, XmlDocument xmlMetadata)
        {
            // First, check metadata
            var brailleTypeMeta = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:BrailleType", sXmlNs);

            var brailleTypes = new SortedSet<string>(new BrailleTypeComparer());
            var brailleFiles = new List<BrailleFile>();

            var validTypes = Enum.GetNames(typeof(BrailleCode)).ToList();

            // Enumerate all of the braille attachments
            {
                var type = it.IsStimulus ? "passage" : "item";
                var attachmentXPath = $"itemrelease/{type}/content/attachmentlist/attachment";
                var fileExtensionsXPath = $"itemrelease/{type}/content/attachmentlist/attachment/@file";
                var extensions = xml.SelectNodes(fileExtensionsXPath)?
                    .Cast<XmlNode>().Select(x => x.InnerText)
                    .GroupBy(x => x.Split('.').LastOrDefault());
                var fileTypes = extensions.Select(x => x.Key.ToLower()).ToList();
                if (fileTypes.Contains("brf") && fileTypes.Contains("prn")) // We have more than one Braille file extension present
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded, 
                        "More than one braille embossing file type extension present in attachment list", $"File Types: [{fileTypes.Aggregate((x,y) => $"{x}|{y}")}]");
                }

                var processedIds = new List<string>();

                foreach (XmlElement xmlEle in xml.SelectNodes(attachmentXPath))
                {
                    // All attachments must have an ID and those IDs must be unique within their item
                    var id = xmlEle.GetAttribute("id");
                    if (string.IsNullOrEmpty(id))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Attachment missing id attribute.");
                    } else if (processedIds.Contains(id))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, 
                            "Duplicate attachment IDs in attachmentlist element", 
                            $"ID: {id}");
                    }
                    else
                    {
                        processedIds.Add(id);
                    }

                    // Get attachment type and check if braille
                    var attachType = xmlEle.GetAttribute("type");
                    if (string.IsNullOrEmpty(attachType))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Attachment missing type attribute.");
                        continue;
                    }
                    BrailleFileType attachmentType;
                    if (!Enum.TryParse(attachType, out attachmentType))
                    {
                        continue; // Not braille attachment
                    }

                    // === From here forward we are only dealing with Braille attachments and the error messages reflect that ===

                    if (!attachType.Equals(brailleTypeMeta))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Severe, "Braille metadata does not match attachment type.", "metadata='{0}', fileType='{1}'", brailleTypeMeta, attachType);
                    }

                    // Check that the file exists
                    var filename = xmlEle.GetAttribute("file");
                    const string validFilePattern = @"(stim|item|passage)_(\d+)_(enu)_(\D{3}|uncontracted|contracted|nemeth)(_transcript)*\.(brf|prn)";
                    if (string.IsNullOrEmpty(filename))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Attachment missing file attribute.", "attachType='{0}'", attachType);
                        continue;
                    }
                    if (!it.FfItem.FileExists(filename))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable, "Braille embossing file is missing.", "attachType='{0}' Filename='{1}'", attachType, filename);
                    }

                    // Check the extension
                    var extension = Path.GetExtension(filename);
                    if (extension.Length > 0) extension = extension.Substring(1); // Strip leading "."
                    if (!string.Equals(extension, attachType, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded, "Braille ebossing filename has unexpected extension.", "extension='{0}' expected='{1}' filename='{2}'", extension, attachType, filename);
                    }

                    // Get the subtype (if any)
                    var subtype = xmlEle.GetAttribute("subtype");
                    BrailleCode attachmentSubtype;
                    if (!string.IsNullOrEmpty(subtype) && !subtype.Contains('_') &&
                        Enum.TryParse(subtype.ToUpperInvariant(), out attachmentSubtype))
                    {
                        brailleFiles.Add(new BrailleFile
                        {
                            Type = attachmentType,
                            Code = attachmentSubtype
                        });
                    }
                    else
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded, 
                            "Braille embossing attachment has unknown subtype.", subtype ?? "Subtype not present");
                    }

                    var match = Regex.Match(filename, validFilePattern);
                    if (match.Success)
                    // We are not checking for these values if it's not a match.
                    {
                        var itemOrStimText = it.IsStimulus ? "stim" : "item";
                        if (!match.Groups[1].Value.Equals(itemOrStimText, StringComparison.OrdinalIgnoreCase))
                        // item or stim
                        {
                            if (it.IsStimulus &&
                                !match.Groups[1].Value.Equals("passage", StringComparison.OrdinalIgnoreCase))
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                                    "Braille embossing filename indicates item/stim mismatch.",
                                    $"Target: {itemOrStimText} Filename: {filename} Actual Target: {match.Groups[1].Value}");
                            }
                            // According to the documentation, all stimuli braille attachments must be prefixed with "stim", 
                            // but functionally, they may be "passage". Indicate a benign error.
                            else
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Benign,
                                    "Braille embossing filename designated as a \"passage\", should be \"stim\".",
                                    filename);
                            }
                        }
                        if (!match.Groups[2].Value.Equals(it.ItemId.ToString(), StringComparison.OrdinalIgnoreCase))
                        // item id
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                                "Braille embossing filename indicates item ID mismatch.",
                                $"ItemId: {it.ItemId} FilenameId: {match.Groups[2].Value} Filename: {filename}");
                        }
                        if (!match.Groups[3].Value.Equals("enu", StringComparison.OrdinalIgnoreCase))
                        // this is hard-coded 'enu' English for now. No other values are valid
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                                "Braille embossing filename indicates language other than \"ENU\".",
                                $"Attachment Language: {match.Groups[3].Value} Filename: {filename}");
                        }

                        if (!validTypes.Select(x => x.ToLower()).Contains(match.Groups[4].Value.ToLower()))
                        // code, uncontracted, contracted, nemeth
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                                "Braille embossing filename indicates unknown braille type.",
                                $"Braille Code: {match.Groups[4].Value} Filename: {filename}");
                        }
                        else if (!string.IsNullOrEmpty(subtype) && !match.Groups[4].Value.Equals(subtype.Split('_').First(),
                          StringComparison.OrdinalIgnoreCase))
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                "Braille embossing filename doesn't match expected braille type.",
                                $"Embossing Braille Type: {match.Groups[4].Value} Expected Braille Type: {subtype.Split('_').First()} Filename {filename}");
                        }
                        if (!string.IsNullOrEmpty(match.Groups[5].Value) &&
                            !match.Groups[5].Value.Equals("_transcript", StringComparison.OrdinalIgnoreCase))
                        // this item has a braille transcript
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                                "Braille embossing filename suffix must be either 'transcript' or blank",
                                $"Suffix: {match.Groups[5].Value} Filename: {filename}");
                        }
                        if (!match.Groups[6].Value.Equals(attachType, StringComparison.OrdinalIgnoreCase))
                        // Must match the type listed
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                                "Braille embossing filename extension is invalid; must either be 'brf' or 'prn'",
                                $"Extension: {match.Groups[6].Value} Filename: {filename}");
                        }
                    }
                    else
                    {
                        /*
                         *       Report a ‘degraded’ error if the set of braille attachments doesn’t match one of these patterns.
                         *       Report a ‘degraded’ error if the set of braille transcript attachments doesn’t match one of these patterns.
                         *       Report a ‘warning’ error if the braille file extensions don’t match (e.g. some are BRF and others are PRN).
                         *       Concatenate the pattern code from the table above to the “Braille” column in ItemReport and StimulusReport. For example, “BRF UEB2” or “PRN UEB4”  
                         *       If the item has braille transcripts then concatenate both pattern codes. For example, “BRF Both4 Both6”. 
                         */
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                            "Braille embossing filename does not match naming convention.",
                            $"Filename: {filename}"
                            );
                    }

                    // Report the result
                    var brailleFile = (string.IsNullOrEmpty(subtype)) ? attachType.ToUpperInvariant() : string.Concat(attachType.ToUpperInvariant(), "(", subtype.ToLowerInvariant(), ")");
                    if (!brailleTypes.Add(brailleFile))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable, 
                            "Multiple braille embossing files of same type and subtype.", "type='{0}'", brailleFile);
                    }
                }
            }

            // Enumerate all embedded braille.
            if (Program.gValidationOptions.IsEnabled("ebt"))
            {
                var emptyBrailleTextFound = false;
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

                if (emptyBrailleTextFound)
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Benign, "brailleTextString and/or brailleCode element is empty.");
            }

            var brailleList = BrailleUtility.GetSupportByCode(brailleFiles);
            // Check for match with metadata
            // Metadata MUST take precedence over contents.
            if (string.Equals(brailleTypeMeta, "Not Braillable", StringComparison.OrdinalIgnoreCase))
            {
                if (brailleTypes.Count != 0)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign, "Metadata indicates not braillable but braille content included.", "brailleTypes='{0}'", string.Join(";", brailleTypes));
                }
                brailleTypes.Clear();
                brailleTypes.Add("NotBraillable");
                brailleList.Clear();
                brailleList.Add(BrailleSupport.NOTBRAILLABLE);
            }
            else if (string.IsNullOrEmpty(brailleTypeMeta))
            {
                brailleTypes.Clear();   // Don't report embedded braille markup if there is no attachment
                brailleList.Clear();
            }

            return brailleFiles.FirstOrDefault()?.Type + (brailleList.Any() ?
                "|" + brailleList
                    .Select(x => x.ToString())
                    .Distinct()
                    .Aggregate((y, z) => $"{y};{z}")
                    : string.Empty);
        }

        private static bool HasTtsSilencingTags(XmlNode xml)
        {
            return xml.SelectNodes("//readAloud/textToSpeechPronunciation")
                .Cast<XmlElement>()
                .Any(node => node.InnerText.Length == 0);
        }

        private class BrailleTypeComparer : IComparer<string>
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
        string ValidateContentAndWordlist(ItemContext it, XmlDocument xml)
        {
            // Compose lists of referenced term Indices and Names
            var termIndices = new List<int>();
            var terms = new List<string>();

            // Process all CDATA (embedded HTML) sections in the content
            {
                var contentNode = xml.SelectSingleNode(it.IsStimulus ? "itemrelease/passage/content" : "itemrelease/item/content");
                if (contentNode == null)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Item has no content element.");
                }
                else
                {
                    foreach (var node in new XmlSubtreeEnumerable(contentNode))
                    {
                        if (node.NodeType == XmlNodeType.CDATA)
                        {
                            var html = LoadHtml(it, node);
                            ValidateContentCData(it, termIndices, terms, html);
                        }
                    }
                }
            }

            // Get the wordlist ID (and check for multiple instances)
            string wordlistId = string.Empty;
            string wordlistBankkey = string.Empty;
            string xp = it.IsStimulus
                ? "itemrelease/passage/resourceslist/resource[@type='wordList']"
                : "itemrelease/item/resourceslist/resource[@type='wordList']";

            foreach (XmlElement xmlRes in xml.SelectNodes(xp))
            {
                string witId = xmlRes.GetAttribute("id");
                string witBankkey = xmlRes.GetAttribute("bankkey");
                if (string.IsNullOrEmpty(witId) || string.IsNullOrEmpty(witBankkey))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded, "Item references blank wordList id or bankkey.");
                }
                else
                {
                    if (!string.IsNullOrEmpty(wordlistId))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded, "Item references multiple wordlists.");
                    }
                    else
                    {
                        wordlistId = witId;
                        wordlistBankkey = witBankkey;
                    }

                    // TODO: Update this to use an itemIdentifier so that bankKey is a factor
                    // That will require modifying the extensions (Increment, Count, etc.) to work with key types other than string.
                    mWordlistRefCounts.Increment(witId);
                }
            }

            if (string.IsNullOrEmpty(wordlistId))
            {
                if (termIndices.Count > 0)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Benign, "Item has terms marked for glossary but does not reference a wordlist.");
                }
                return string.Empty;
            }

            ValidateWordlistVocabulary(wordlistBankkey, wordlistId, it, termIndices, terms);

            return wordlistId;
        }

        static readonly char[] s_WhiteAndPunct = { '\t', '\n', '\r', ' ', '!', '"', '#', '$', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/', ':', ';', '<', '=', '>', '?', '@', '[', '\\', ']', '^', '_', '`', '{', '|', '~' };

        private void ValidateContentCData(ItemContext it, IList<int> termIndices, IList<string> terms, XmlDocument html)
        {
            /* Word list references look like this:
            <span id="item_998_TAG_2" class="its-tag" data-tag="word" data-tag-boundary="start" data-word-index="1"></span>
            What
            <span class="its-tag" data-tag-ref="item_998_TAG_2" data-tag-boundary="end"></span>
            */

            // Extract all wordlist references
            foreach (XmlElement node in html.SelectNodes("//span[@data-tag='word' and @data-tag-boundary='start']"))
            {

                // For a word reference, get attributes and look for the end tag
                var id = node.GetAttribute("id");
                if (string.IsNullOrEmpty(id))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "WordList reference lacks an ID");
                    continue;
                }
                var scratch = node.GetAttribute("data-word-index");
                int termIndex;
                if (!int.TryParse(scratch, out termIndex))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "WordList reference term index is not integer", "id='{0} index='{1}'", id, scratch);
                    continue;
                }

                var term = string.Empty;
                var snode = node.NextNode();
                for (;;)
                {
                    // If no more siblings but didn't find end tag, report.
                    if (snode == null)
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable, "WordList reference missing end tag.", "id='{0}' index='{1}' term='{2}'", id, termIndex, term);
                        break;
                    }

                    // Look for end tag
                    XmlElement enode = snode as XmlElement;
                    if (enode != null
                        && enode.GetAttribute("data-tag-boundary").Equals("end", StringComparison.Ordinal)
                        && enode.GetAttribute("data-tag-ref").Equals(id, StringComparison.Ordinal))
                    {
                        break;
                    }

                    // Collect term plain text
                    if (snode.NodeType == XmlNodeType.Text || snode.NodeType == XmlNodeType.SignificantWhitespace)
                    {
                        term += snode.Value;
                    }

                    snode = snode.NextNode();
                }
                term = term.Trim(s_WhiteAndPunct);
                termIndices.Add(termIndex);
                terms.Add(term);
            }

            // Img tag validation
            if (Program.gValidationOptions.IsEnabled("iat"))
            {
                //Temporarily disabled to prevent double reporting
                //ReportMissingImgAltTags(it, xml, ExtractImageList(html));
            }
        }

        static XmlDocument LoadHtml(ItemContext it, XmlNode content)
        {
            // Parse the HTML into an XML DOM
            XmlDocument html = null;
            try
            {
                var settings = new Html.HtmlReaderSettings
                {
                    CloseInput = true,
                    EmitHtmlNamespace = false,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                    IgnoreInsignificantWhitespace = true
                };
                using (var reader = new Html.HtmlReader(new StringReader(content.InnerText), settings))
                {
                    html = new XmlDocument();
                    html.Load(reader);
                }
            }
            catch (Exception err)
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, 
                    "Invalid html content.", "context='{0}' error='{1}'", GetXmlContext(content), err.Message);
            }
            return html;
        }

        List<HtmlImageTag> ExtractImageList(XmlDocument htmlDocument)
        {
            // Assemble img tags and map their src and id attributes for validation
            var imgList = new List<HtmlImageTag>();
            imgList.AddRange(htmlDocument.SelectNodes("//img").Cast<XmlNode>()
                .Select(x => new HtmlImageTag
                {
                    Source = x.Attributes["src"]?.InnerText ?? string.Empty,
                    Id = x.Attributes["id"]?.InnerText ?? string.Empty,
                    // Check to see if the enclosing element is a span. If so, add the id
                    EnclosingSpanId = x.ParentNode.Name.Equals("span", StringComparison.OrdinalIgnoreCase) ?
                        x.ParentNode.Attributes["id"]?.InnerText ?? string.Empty :
                        string.Empty
                }));
            return imgList;
        }

        // Acceptable sub-elements: textToSpeechPronunciation, textToSpeechPronunciationAlternate, audioText, audioSortDesc, audioLongDesc
        void CheckForNonEmptyReadAloudSubElement(ItemContext it, XmlNode xml, string id, string src, string enclosingSpanId)
        {
            if(!new List<string> {"textToSpeechPronunciation", "textToSpeechPronunciationAlternate", "audioText", "audioShortDesc", "audioLongDesc"}
                .Select(t => $"relatedElementInfo/readAloud/{t}") // Select sub-elements from list above
                .Any(element => ElementExistsAndIsNonEmpty(xml, element))) // Check if the sub-element exists and has a value
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded, 
                    "Img tag is missing alternative text in the <readAloud> accessibility element.", 
                    "id ='{0}' src='{1}' spanId='{2}'", id, src, enclosingSpanId ?? string.Empty);
            }

        }

        private static bool ElementExistsAndIsNonEmpty(XmlNode xml, string path)
        {
            var node = xml.SelectSingleNode(path);
            return !string.IsNullOrEmpty(node?.InnerText);
        }

        void ReportMissingImgAltTags(ItemContext it, XmlDocument xml, List<HtmlImageTag> imgList)
        {
            foreach (var img in imgList)
            {
                if (string.IsNullOrEmpty(img.Source))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded, "Img tag is missing src attribute");
                }
                if (string.IsNullOrEmpty(img.Id))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded, 
                        "Img tag is missing id attribute to associate with alternative text.", "src = '{0}'", img.Source);
                }
                else
                {
                    var xpAccessibility = $"itemrelease/{(it.IsStimulus ? "passage" : "item")}/content/apipAccessibility/accessibilityInfo/accessElement/contentLinkInfo";
                    // Search for matching ID in the accessibility nodes. If none exist, record an error.
                    var accessibilityNodes = xml.SelectNodes(xpAccessibility)
                        .Cast<XmlNode>()
                        .Where(accessibilityNode => accessibilityNode.Attributes["itsLinkIdentifierRef"].Value.Equals(img.Id)
                            || (!string.IsNullOrEmpty(img.EnclosingSpanId) 
                            && accessibilityNode.Attributes["itsLinkIdentifierRef"].Value.Equals(img.EnclosingSpanId)))
                        .ToList();
                    if (!accessibilityNodes.Any())
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded, 
                            "Img tag does not have associated alternative text.", "id ='{0}' src='{1}'", img.Id, img.Source);
                    }
                    else
                    {
                        foreach (var node in accessibilityNodes)
                        {
                            CheckForNonEmptyReadAloudSubElement(it, node.ParentNode, img.Id, img.Source, img.EnclosingSpanId);
                        }
                    }
                }
            }
        }

        static string GetXmlContext(XmlNode node)
        {
            string context = string.Empty;
            while (node != null && node.NodeType != XmlNodeType.Document)
            {
                context = string.Concat("/", node.Name, context);
                node = node.ParentNode;
            }
            return context;
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
                if (node == null) ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign, "Item content includes language but metadata does not have a corresponding <Language> entry.", "Language='{0}'", language);
            }

            string translation = string.Empty;

            // Now, search the metadata for translations and make sure all exist in the content
            foreach (XmlElement xmlEle in xmlMetadata.SelectNodes("metadata/sa:smarterAppMetadata/sa:Language", sXmlNs))
            {
                string language = xmlEle.InnerText;
                if (!languages.Contains(language))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Severe, "Item metadata indicates language but item content does not include that language.", "Language='{0}'", language);
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
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Benign, "Media file not referenced in item.", "Filename='{0}'", filename);
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

        private void TabulateWordList(ItemIdentifier ii)
        {
            // Get the item context
            ItemContext it;
            if (!ItemContext.TryCreate(mPackage, ii, out it))
            {
                ReportingUtility.ReportError(ii, ErrorCategory.Item, ErrorSeverity.Severe, "WordList not found in package.");
                return;
            }

            // Read the item XML
            XmlDocument xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", xml))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid wordlist file.", LoadXmlErrorDetail);
                return;
            }

            // Count this wordlist
            ++mWordlistCount;

            // See if the wordlist has been referenced
            int refCount = mWordlistRefCounts.Count(it.ItemId.ToString());
            if (refCount == 0)
            {
                ReportingUtility.ReportError(it, ErrorCategory.Wordlist, ErrorSeverity.Benign, "Wordlist is not referenced by any item.");
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
            mWordlistReport.WriteLine(string.Join(",", CsvEncode(it.FolderDescription), it.BankKey.ToString(), it.ItemId.ToString(), refCount.ToString(), termcount.ToString(), maxgloss.ToString(), mingloss.ToString(), (termcount > 0) ? (((double)totalgloss)/((double)termcount)).ToString("f2") : "0" ));
        }

        static readonly Regex sRxAudioAttachment = new Regex(@"<a[^>]*href=""([^""]*)""[^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        static readonly Regex sRxImageAttachment = new Regex(@"<img[^>]*src=""([^""]*)""[^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // Attachments don't have to follow the naming convention but they usually do. When they match then we compare values.
        // Sample: item_116605_v1_116605_01btagalog_glossary_ogg_m4a.m4a
        static readonly Regex sRxAttachmentNamingConvention = new Regex(@"^item_(\d+)_v\d+_(\d+)_(\d+)([a-zA-Z]+)_glossary(?:_ogg)?(?:_m4a)?(?:_ogg)?\.(?:ogg|m4a)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private void ValidateWordlistVocabulary(string bankKey, string wordlistId, ItemContext itemIt, List<int> termIndices, List<string> terms)
        {
            // Make sure the wordlist exists
            ItemIdentifier ii = new ItemIdentifier(cItemTypeWordlist, bankKey, wordlistId);
            FileFolder ff;
            if (!mPackage.TryGetItem(ii, out ff))
            {
                ReportingUtility.ReportError(itemIt, ErrorCategory.Item, ErrorSeverity.Degraded, "Item references non-existent wordlist (WIT)", "wordlistId='{0}'", wordlistId);
                return;
            }
 
            // Read the wordlist XML
            var xml = new XmlDocument(sXmlNt);
            if (!TryLoadXml(ff, ii.FullId + ".xml", xml))
            {
                ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Severe, "Invalid wordlist file.", LoadXmlErrorDetail);
                return;
            }

            // Make sure this is a wordlist
            if (!string.Equals(xml.XpEvalE("itemrelease/item/@type"), cItemTypeWordlist))
            {
                ReportingUtility.ReportError(itemIt, ErrorCategory.Item, ErrorSeverity.Severe, "WordList reference is to a non-wordList item.", $"referencedId='{ii.ItemId}'");
                return;
            }

            // Sanity check
            if (!string.Equals(xml.XpEvalE("itemrelease/item/@id"), ii.ItemId.ToString()))
            {
                ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Severe, "Wordlist file id mismatch.", $"wordListId='{xml.XpEval("itemrelease/item/@id")}' expected='{ii.ItemId}'");
                return;
            }

            // Add this to the wordlist tabulation queue (while keeping counts clean)
            if (mWordlistQueue.Add(ii))
            {
                if (mItemQueue.Contains(ii)) ++mTransferCount;
            };

            // Create a dictionary of attachment files
            Dictionary <string, long> attachmentFiles = new Dictionary<string, long>();
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
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Severe, "Wordlist has multiple terms with the same index.", "index='{0}'", index);
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
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Benign, "Wordlist term is not referenced by item.", "term='{0}' termIndex='{1}'", term, index);
                }

                // Find the attachment references and enumberate the translations
                int translationBitflags = 0;
                foreach (XmlNode htmlNode in kwNode.SelectNodes("html"))
                {
                    var listType = htmlNode.XpEval("@listType");
                    mTranslationCounts.Increment(listType);

                    var nTranslation = -1;
                    if (sExpectedTranslationsIndex.TryGetValue(listType, out nTranslation))
                    {
                        translationBitflags |= (1 << nTranslation);
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
                                ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Degraded, "Wordlist attachment filename indicates wordlist ID mismatch.", "filename='{0}' filenameItemId='{1}' expectedItemId='{2}'", filename, match2.Groups[1].Value, wordlistId);
                            }

                            // Check that the wordlist term index matches
                            /* While most filename indices match. It's quite common for them not to match and still be the correct audio
                               Disabling this check because it's mostly false alarms.

                            int filenameIndex;
                            if (!int.TryParse(match2.Groups[3].Value, out filenameIndex)) filenameIndex = -1;
                            if (filenameIndex != index && filenameIndex != ordinal
                                && (filenameIndex >= wordlistTerms.Count || !string.Equals(wordlistTerms[filenameIndex], term, StringComparison.OrdinalIgnoreCase)))
                            {
                                ReportingUtility.ReportWitError(ItemIt, it, ErrorSeverity.Degraded, "Wordlist attachment filename indicates term index mismatch.", "filename='{0}' filenameIndex='{1}' expectedIndex='{2}'", filename, filenameIndex, index);
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
                                ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Degraded, "Wordlist audio filename indicates attachment language mismatch.", "filename='{0}' filenameListType='{1}' expectedListType='{2}'", filename, filenameListType, listType);
                            }
                        }

                    }

                    // Look for an image glossary entry
                    match = sRxImageAttachment.Match(html);
                    if (match.Success)
                    {
                        // Use RegEx to find the audio glossary entry in the contents.
                        string filename = match.Groups[1].Value;
                        ProcessGlossaryAttachment(filename, itemIt, ii, index, listType, termReferenced, wordlistTerms, attachmentFiles, attachmentToReference, ref imageType, ref imageSize);
                    }

                    string folderDescription = string.Concat(mPackage.Name, "/", ii.FolderName);

                    // Folder,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize
                    if (Program.gValidationOptions.IsEnabled("gtr"))
                        mGlossaryReport.WriteLine(string.Join(",", CsvEncode(folderDescription), ii.BankKey.ToString(), ii.ItemId.ToString(), itemIt.ItemId.ToString(), index.ToString(), CsvEncodeExcel(term), CsvEncode(listType), html.Length.ToString(), audioType, audioSize.ToString(), imageType, imageSize.ToString(), CsvEncode(html)));
                    else
                        mGlossaryReport.WriteLine(string.Join(",", CsvEncode(folderDescription), ii.BankKey.ToString(), ii.ItemId.ToString(), itemIt.ItemId.ToString(), index.ToString(), CsvEncodeExcel(term), CsvEncode(listType), html.Length.ToString(), audioType, audioSize.ToString(), imageType, imageSize.ToString()));
                }

                // Report any expected translations that weren't found
                if (termReferenced && translationBitflags != 0 && translationBitflags != sExpectedTranslationsBitflags)
                {
                    // Make a list of translations that weren't found
                    List<string> missedTranslations = new List<string>();
                    for (int i = 0; i < sExpectedTranslations.Length; ++i)
                    {
                        if ((translationBitflags & (1 << i)) == 0) missedTranslations.Add(sExpectedTranslations[i]);
                    }
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Tolerable, "Wordlist does not include all expected translations.", "term='{0}' missing='{1}'", term, string.Join(", ", missedTranslations));
                }
            }

            Porter.Stemmer stemmer = new Porter.Stemmer();

            // Make sure terms match references
            for (int i=0; i<termIndices.Count; ++i)
            {
                int index = termIndices[i];
                if (index >= wordlistTerms.Count || string.IsNullOrEmpty(wordlistTerms[index]))
                {
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Benign, "Item references non-existent wordlist term.", "text='{0}' termIndex='{1}'", terms[i], index);
                }
                else
                {
                    if (!stemmer.TermsMatch(terms[i], wordlistTerms[index]))
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Degraded, "Item text does not match wordlist term.", "text='{0}' term='{1}' termIndex='{2}'", terms[i], wordlistTerms[index], index);
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
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Benign, "Unreferenced wordlist attachment file.", "filename='{0}'", pair.Key);
                    }
                }
            }
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
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Severe, "Wordlist attachment not found.",
                            "filename='{0}' term='{1}' termIndex='{2}'", filename, wordlistTerms[termIndex], termIndex);
                    }
                    else
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Degraded, "Wordlist audio filename differs in capitalization (will fail on certain platforms).",
                            "referenceFilename='{0}' actualFilename='{1}' termIndex='{2}'", filename, caseMismatchFilename, termIndex);
                    }
                }

                else if (Program.gValidationOptions.IsEnabled("mwa")) // Term not referenced
                {
                    if (caseMismatchFilename == null)
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Benign, "Wordlist attachment not found. Benign because corresponding term is not referenced.",
                            "filename='{0}' term='{1}' termIndex='{2}'", filename, wordlistTerms[termIndex], termIndex);
                    }
                    else
                    {
                        ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Benign, "Wordlist attachment filename differs in capitalization. Benign because corresponding term is not referenced.",
                            "referenceFilename='{0}' actualFilename='{1}' termIndex='{2}'", filename, caseMismatchFilename, termIndex);
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
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Severe, "Two different wordlist terms reference the same attachment.",
                        "filename='{0}' termA='{1}' termB='{2}' termIndexA='{3}' termIndexB='{4}",
                        filename, wordlistTerms[previousTerm.TermIndex], wordlistTerms[termIndex], previousTerm.TermIndex, termIndex);
                }

                // Error if different listTypes (language or image)
                if (!string.Equals(listType, previousTerm.ListType, StringComparison.Ordinal))
                {
                    ReportingUtility.ReportWitError(itemIt, ii, ErrorSeverity.Severe, "Same wordlist attachment used for different languages or types.",
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

        bool ValidateManifest(FileFolder rootFolder)
        {
            try
            {
                if (!rootFolder.FileExists(cImsManifest))
                {
                    Console.WriteLine($"   Not a content package; '{cImsManifest}' must exist in root.");
                    ReportingUtility.ReportError(string.Empty, ErrorCategory.Manifest, ErrorSeverity.Severe, $"Not a content package; '{cImsManifest}' must exist in root.");
                    return false;
                }

                // Load the manifest
                XmlDocument xmlManifest = new XmlDocument(sXmlNt);
                if (!TryLoadXml(rootFolder, cImsManifest, xmlManifest))
                {
                    ReportingUtility.ReportError(string.Empty, ErrorCategory.Manifest, ErrorSeverity.Benign, "Invalid manifest.", LoadXmlErrorDetail);
                    return true;
                }

                // Keep track of every resource id mentioned in the manifest
                HashSet<string> ids = new HashSet<string>();

                // Enumerate all resources in the manifest
                foreach (XmlElement xmlRes in xmlManifest.SelectNodes("ims:manifest/ims:resources/ims:resource", sXmlNs))
                {
                    string id = xmlRes.GetAttribute("identifier");
                    if (string.IsNullOrEmpty(id))
                        ReportingUtility.ReportError(cImsManifest, ErrorCategory.Manifest, ErrorSeverity.Benign, "Resource in manifest is missing id.", $"Filename='{xmlRes.XpEvalE("ims: file / @href", sXmlNs)}'");
                    string filename = xmlRes.XpEval("ims:file/@href", sXmlNs);
                    if (string.IsNullOrEmpty(filename))
                    {
                        ReportingUtility.ReportError(cImsManifest, ErrorCategory.Manifest, ErrorSeverity.Benign, "Resource specified in manifest has no filename.", $"ResourceId='{id}'");
                    }
                    else if (!rootFolder.FileExists(filename))
                    {
                        ReportingUtility.ReportError(cImsManifest, ErrorCategory.Manifest, ErrorSeverity.Benign, "Resource specified in manifest does not exist.", $"ResourceId='{id}' Filename='{filename}'");
                    }

                    if (ids.Contains(id))
                    {
                        ReportingUtility.ReportError(cImsManifest, ErrorCategory.Manifest, ErrorSeverity.Benign, "Resource listed multiple times in manifest.", $"ResourceId='{id}'");
                    }
                    else
                    {
                        ids.Add(id);
                    }

                    // Normalize the filename
                    filename = NormalizeFilenameInManifest(filename);
                    if (mFilenameToResourceId.ContainsKey(filename))
                    {
                        ReportingUtility.ReportError(cImsManifest, ErrorCategory.Manifest, ErrorSeverity.Benign, "File listed multiple times in manifest.", $"ResourceId='{id}' Filename='{filename}'");
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
                            ReportingUtility.ReportError(cImsManifest, ErrorCategory.Manifest, ErrorSeverity.Benign, "Dependency in manifest is missing identifierref attribute.", $"ResourceId='{id}'");
                        }
                        else
                        {
                            string dependency = ToDependsOnString(id, dependsOnId);
                            if (mResourceDependencies.Contains(dependency))
                            {
                                ReportingUtility.ReportError(cImsManifest, ErrorCategory.Manifest, ErrorSeverity.Benign, "Dependency in manifest repeated multiple times.", $"ResourceId='{id}' DependsOnId='{dependsOnId}'");
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
                    ReportingUtility.ReportError(cImsManifest, ErrorCategory.Manifest, ErrorSeverity.Benign, "Manifest is empty.");
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
                ReportingUtility.ReportError(string.Empty, ErrorCategory.Exception, ErrorSeverity.Severe, err.GetType().Name, err.ToString());
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
                        ReportingUtility.ReportError(cImsManifest, ErrorCategory.Manifest, ErrorSeverity.Benign, "Item does not appear in the manifest.", $"ItemFilename='{itemFileName}'");
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
                    ReportingUtility.ReportError(cImsManifest, ErrorCategory.Manifest, ErrorSeverity.Benign, "Resource does not appear in the manifest.", $"Filename='{filename}'");
                }

                // If in an item, see if dependency is expressed
                else if (itemId != null && !string.Equals(itemId, resourceId, StringComparison.Ordinal))
                {
                    // Check for dependency
                    if (!mResourceDependencies.Contains(ToDependsOnString(itemId, resourceId)))
                        ReportingUtility.ReportError(cImsManifest, ErrorCategory.Manifest, ErrorSeverity.Benign, "Manifest does not express resource dependency.", $"ResourceId='{itemId}' DependesOnId='{resourceId}'");
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

    /*
    class Timer
    {
        static TextWriter s_writer;

        public static void Init(string prefix)
        {
            s_writer = new StreamWriter(prefix + "Timings.csv");
        }

        public static void Conclude()
        {
            if (s_writer != null)
            {
                s_writer.Dispose();
                s_writer = null;
            }
        }

        List<int> m_ticks = new List<int>();
        string m_type;

        public Timer(string type)
        {
            m_type = type;
            Lap();
        }

        public void Lap()
        {
            m_ticks.Add(Environment.TickCount);
        }

        public void Report()
        {
            s_writer.Write(m_type);
            for (int i = 0; i < m_ticks.Count - 1; ++i)
            {
                uint elapsed = unchecked((uint)m_ticks[i + 1] - (uint)m_ticks[i]);
                s_writer.Write(",{0}.{1:d3}", elapsed / 1000, elapsed % 1000);
            }
            s_writer.WriteLine();
        }
    }
    */

}
