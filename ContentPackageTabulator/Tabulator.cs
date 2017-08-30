using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using ContentPackageTabulator.Extensions;
using ContentPackageTabulator.Extractors;
using ContentPackageTabulator.Models;
using ContentPackageTabulator.Utilities;
using ContentPackageTabulator.Validators;

namespace ContentPackageTabulator
{
    public class Tabulator
    {
        public const string cImsManifest = "imsmanifest.xml";

        public const string cStimulusInteractionType = "Stimulus";

        // Filenames
        public const string cSummaryReportFn = "SummaryReport.txt";
        public const string cItemReportFn = "ItemReport.csv";
        public const string cStimulusReportFn = "StimulusReport.csv";
        public const string cWordlistReportFn = "WordlistReport.csv";
        public const string cGlossaryReportFn = "GlossaryReport.csv";
        public const string cErrorReportFn = "ErrorReport.csv";
        public static NameTable sXmlNt;
        public static XmlNamespaceManager sXmlNs;
        public static Dictionary<string, int> sExpectedTranslationsIndex;

        public static string[] sExpectedTranslations =
        {
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

        public static int sExpectedTranslationsBitflags;

        public static readonly HashSet<string> sValidWritingTypes = new HashSet<string>(
            new[]
            {
                "Explanatory",
                "Opinion",
                "Informative",
                "Argumentative",
                "Narrative"
            });

        public static readonly HashSet<string> sValidClaims = new HashSet<string>(
            new[]
            {
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


        public static int mItemCount;
        public static int mWordlistCount;
        public static int mGlossaryTermCount;
        public static int mGlossaryM4aCount;
        public static int mGlossaryOggCount;
        public static Dictionary<string, int> mTypeCounts = new Dictionary<string, int>();
        public static Dictionary<string, int> mTermCounts = new Dictionary<string, int>();
        public static Dictionary<string, int> mTranslationCounts = new Dictionary<string, int>();
        public static Dictionary<string, int> mAnswerKeyCounts = new Dictionary<string, int>();
        public static FileFolder mPackageFolder;
        public static Dictionary<string, string> mFilenameToResourceId = new Dictionary<string, string>();
        public static HashSet<string> mResourceDependencies = new HashSet<string>();

        public static Dictionary<string, int> mWordlistRefCounts = new Dictionary<string, int>();
        // Reference count for wordlist IDs

        public static Dictionary<string, ItemContext> mIdToItemContext = new Dictionary<string, ItemContext>();
        public static LinkedList<ItemContext> mStimContexts = new LinkedList<ItemContext>();

        // Per report variables
        public static TextWriter mItemReport;
        public static TextWriter mStimulusReport;
        public static TextWriter mWordlistReport;
        public static TextWriter mGlossaryReport;
        public static string mSummaryReportPath;

        private static readonly HashSet<string> sMediaFileTypes = new HashSet<string>(
            new[] {"MP4", "MP3", "M4A", "OGG", "VTT", "M4V", "MPG", "MPEG"});

        public static readonly Regex sRxAudioAttachment = new Regex(@"<a[^>]*href=""([^""]*)""[^>]*>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static readonly Regex sRxImageAttachment = new Regex(@"<img[^>]*src=""([^""]*)""[^>]*>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // Attachments don't have to follow the naming convention but they usually do. When they match then we compare values.
        // Sample: item_116605_v1_116605_01btagalog_glossary_ogg_m4a.m4a
        public static readonly Regex sRxAttachmentNamingConvention =
            new Regex(@"^item_(\d+)_v\d+_(\d+)_(\d+)([a-zA-Z]+)_glossary(?:_ogg)?(?:_m4a)?(?:_ogg)?\.(?:ogg|m4a)$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public Tabulator()
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
                sExpectedTranslationsBitflags |= 1 << i;
            }
        }

        // Per Package variables
        public static string mPackageName { get; set; }

        public static string LoadXmlErrorDetail { get; set; }

        // Tabulate a package in the specified directory
        public void TabulateOne(string path)
        {
            var result = new List<TabulationError>();
            try
            {
                if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var fi = new FileInfo(path);
                    Console.WriteLine("Tabulating " + fi.Name);
                    if (!fi.Exists)
                    {
                        throw new FileNotFoundException($"Package '{path}' file not found!");
                    }

                    var filepath = fi.FullName;
                    Initialize(filepath.Substring(0, filepath.Length - 4));
                    using (var tree = new ZipFileTree(filepath))
                    {
                        TabulatePackage(string.Empty, tree);
                    }
                }
                else
                {
                    var folderpath = Path.GetFullPath(path);
                    Console.WriteLine("Tabulating " + Path.GetFileName(folderpath));
                    if (!Directory.Exists(folderpath))
                    {
                        throw new FileNotFoundException(
                            $"Package '{folderpath}' directory not found!");
                    }

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
            var diRoot = new DirectoryInfo(rootPath);

            // Tablulate unpacked packages
            foreach (var diPackageFolder in diRoot.GetDirectories())
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
            foreach (var fiPackageFile in diRoot.GetFiles("*.zip"))
            {
                var filepath = fiPackageFile.FullName;
                Console.WriteLine("Opening " + fiPackageFile.Name);
                using (var tree = new ZipFileTree(filepath))
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
            var diRoot = new DirectoryInfo(rootPath);
            try
            {
                Initialize(Path.Combine(rootPath, "Aggregate"));

                // Tabulate unpacked packages
                foreach (var diPackageFolder in diRoot.GetDirectories())
                {
                    if (File.Exists(Path.Combine(diPackageFolder.FullName, cImsManifest)))
                    {
                        Console.WriteLine("Tabulating " + diPackageFolder.Name);
                        TabulatePackage(diPackageFolder.Name, new FsFolder(diPackageFolder.FullName));
                    }
                }

                // Tabulate packed packages
                foreach (var fiPackageFile in diRoot.GetFiles("*.zip"))
                {
                    var filepath = fiPackageFile.FullName;
                    Console.WriteLine("Opening " + fiPackageFile.Name);
                    using (var tree = new ZipFileTree(filepath))
                    {
                        if (tree.FileExists(cImsManifest))
                        {
                            Console.WriteLine("Tabulating " + fiPackageFile.Name);
                            var packageName = fiPackageFile.Name;
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

        public IEnumerable<TabulationError> TabulateErrors(string path)
        {
            var result = new List<TabulationError>();
            Program.gValidationOptions.EnableAll();
            return result;
        }

        // Initialize all files and collections for a tabulation run
        private void Initialize(string reportPrefix)
        {
            if (Program.gValidationOptions.IsEnabled("dsk"))
            {
                reportPrefix = string.Concat(reportPrefix, "_");
                ReportingUtility.ErrorReportPath = string.Concat(reportPrefix, cErrorReportFn);
                if (File.Exists(ReportingUtility.ErrorReportPath))
                {
                    File.Delete(ReportingUtility.ErrorReportPath);
                }

                mItemReport = new StreamWriter(
                    File.Open(string.Concat(reportPrefix, cItemReportFn), FileMode.OpenOrCreate), Encoding.UTF8);
                // DOK is "Depth of Knowledge"
                // In the case of multiple standards/claims/targets, these headers will not be sufficient
                // TODO: Add CsvHelper library to allow expandable headers
                mItemReport.WriteLine(
                    "Folder,ItemId,ItemType,Version,Subject,Grade,AnswerKey,AsmtType,WordlistId,ASL," +
                    "BrailleType,Translation,Media,Size,DOK,AllowCalculator,MathematicalPractice,MaxPoints," +
                    "CommonCore,ClaimContentTarget,SecondaryCommonCore,SecondaryClaimContentTarget, CAT_MeasurementModel," +
                    "CAT_ScorePoints,CAT_Dimension,CAT_Weight,CAT_Parameters, PP_MeasurementModel," +
                    "PP_ScorePoints,PP_Dimension,PP_Weight,PP_Parameters");

                mStimulusReport =
                    new StreamWriter(File.Open(string.Concat(reportPrefix, cStimulusReportFn), FileMode.OpenOrCreate),
                        Encoding.UTF8);
                mStimulusReport.WriteLine(
                    "Folder,StimulusId,Version,Subject,WordlistId,ASL,BrailleType,Translation,Media,Size,WordCount");

                mWordlistReport =
                    new StreamWriter(File.Open(string.Concat(reportPrefix, cWordlistReportFn), FileMode.OpenOrCreate),
                        Encoding.UTF8);
                mWordlistReport.WriteLine("Folder,WIT_ID,RefCount,TermCount,MaxGloss,MinGloss,AvgGloss");

                mGlossaryReport =
                    new StreamWriter(File.Open(string.Concat(reportPrefix, cGlossaryReportFn), FileMode.OpenOrCreate),
                        Encoding.UTF8);
                mGlossaryReport.WriteLine(Program.gValidationOptions.IsEnabled("gtr")
                    ? "Folder,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize,Text"
                    : "Folder,WIT_ID,ItemId,Index,Term,Language,Length,Audio,AudioSize,Image,ImageSize");

                mSummaryReportPath = string.Concat(reportPrefix, cSummaryReportFn);
                if (File.Exists(mSummaryReportPath))
                {
                    File.Delete(mSummaryReportPath);
                }
            }

            ReportingUtility.ErrorCount = 0;
            mItemCount = 0;
            mWordlistCount = 0;
            mGlossaryTermCount = 0;
            mGlossaryM4aCount = 0;
            mGlossaryOggCount = 0;

            mTypeCounts.Clear();
            mTermCounts.Clear();
            mTranslationCounts.Clear();
            mAnswerKeyCounts.Clear();
        }

        private void Conclude()
        {
            try
            {
                if (mSummaryReportPath != null)
                {
                    using (
                        var summaryReport = new StreamWriter(File.Open(mSummaryReportPath, FileMode.OpenOrCreate),
                            Encoding.UTF8))
                    {
                        SummaryReport(summaryReport);
                    }

                    // Report aggregate results to the console
                    Console.WriteLine("{0} Errors reported.", ReportingUtility.ErrorCount);
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
                if (ReportingUtility.ErrorReport != null)
                {
                    ReportingUtility.ErrorReport.Dispose();
                    ReportingUtility.ErrorReport = null;
                }
            }
        }

        public void TabulatePackage(string packageName, FileFolder packageFolder)
        {
            mPackageName = packageName;

            FileFolder dummy;
            if (!packageFolder.FileExists(cImsManifest)
                &&
                (!packageFolder.TryGetFolder("Items", out dummy) || !packageFolder.TryGetFolder("Stimuli", out dummy)))
            {
                throw new ArgumentException(
                    "Not a valid content package path. Should have 'Items' and 'Stimuli' folders.");
            }

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
                ReportingUtility.ReportError(new ItemContext(this, packageFolder, null, null), ErrorCategory.Exception,
                    ErrorSeverity.Severe, err.ToString());
            }

            // First pass through items
            FileFolder ffItems;
            if (packageFolder.TryGetFolder("Items", out ffItems))
            {
                foreach (var ffItem in ffItems.Folders)
                {
                    try
                    {
                        TabulateItem_Pass1(ffItem);
                    }
                    catch (Exception err)
                    {
                        ReportingUtility.ReportError(new ItemContext(this, ffItem, null, null), ErrorCategory.Exception,
                            ErrorSeverity.Severe, err.ToString());
                    }
                }
            }

            // First pass through stimuli
            if (packageFolder.TryGetFolder("Stimuli", out ffItems))
            {
                foreach (var ffItem in ffItems.Folders)
                {
                    try
                    {
                        TabulateStim_Pass1(ffItem);
                    }
                    catch (Exception err)
                    {
                        ReportingUtility.ReportError(new ItemContext(this, ffItem, null, null), ErrorCategory.Exception,
                            ErrorSeverity.Severe, err.ToString());
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
                    ReportingUtility.ReportError(entry.Value, ErrorCategory.Exception, ErrorSeverity.Severe,
                        err.ToString());
                }
            }

            // Second pass through stimuli
            foreach (var it in mStimContexts)
            {
                try
                {
                    TabulateItem_Pass2(it);
                }
                catch (Exception err)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Exception, ErrorSeverity.Severe, err.ToString());
                }
            }
        }

        private void TabulateItem_Pass1(FileFolder ffItem)
        {
            // Read the item XML
            var xml = new XDocument();
            if (!TryLoadXml(ffItem, ffItem.Name + ".xml", out xml))
            {
                ReportingUtility.ReportError(new ItemContext(this, ffItem, null, null), ErrorCategory.Item,
                    ErrorSeverity.Severe, "Invalid item file.", LoadXmlErrorDetail);
                return;
            }

            // Get the details
            var itemType = xml.XpEval("itemrelease/item/@format") ?? xml.XpEval("itemrelease/item/@type");
            if (itemType == null)
            {
                ReportingUtility.ReportError(new ItemContext(this, ffItem, null, null), ErrorCategory.Item,
                    ErrorSeverity.Severe, "Item type not specified.", LoadXmlErrorDetail);
                return;
            }
            var itemId = xml.XpEval("itemrelease/item/@id");
            if (string.IsNullOrEmpty(itemId))
            {
                ReportingUtility.ReportError(new ItemContext(this, ffItem, null, null), ErrorCategory.Item,
                    ErrorSeverity.Severe, "Item ID not specified.", LoadXmlErrorDetail);
                return;
            }

            // Create and save the item context
            var it = new ItemContext(this, ffItem, itemId, itemType);

            var bankKey = xml.XpEvalE("itemrelease/item/@bankkey");

            // Add to the item count and the type count
            ++mItemCount;
            mTypeCounts.Increment(itemType);

            if (mIdToItemContext.ContainsKey(itemId))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                    "Multiple items with the same ID.");
            }
            else
            {
                mIdToItemContext.Add(itemId, it);
            }

            // Check for filename match
            if (!ffItem.Name.Equals($"item-{bankKey}-{itemId}", StringComparison.OrdinalIgnoreCase))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                    "Item ID doesn't match file/folder name", "bankKey='{0}' itemId='{1}' foldername='{2}'", bankKey,
                    itemId, ffItem);
            }

            // count wordlist reference
            CountWordlistReferences(it, xml);
        }

        private void TabulateStim_Pass1(FileFolder ffItem)
        {
            // Read the item XML
            var xml = new XDocument();
            if (!TryLoadXml(ffItem, ffItem.Name + ".xml", out xml))
            {
                ReportingUtility.ReportError(new ItemContext(this, ffItem, null, null), ErrorCategory.Item,
                    ErrorSeverity.Severe, "Invalid stimulus file.", LoadXmlErrorDetail);
                return;
            }

            // See if passage
            var xmlPassage = xml.SelectSingleNode("itemrelease/passage") as XElement;
            if (xmlPassage == null)
            {
                throw new InvalidDataException("Stimulus does not have passage xml.");
            }

            var itemType = "pass";
            var itemId = xmlPassage.GetAttribute("id");
            if (string.IsNullOrEmpty(itemId))
            {
                throw new InvalidDataException("Item id not found");
            }
            var bankKey = xmlPassage.GetAttribute("bankkey");

            // Add to the item count and the type count
            ++mItemCount;
            mTypeCounts.Increment(itemType);

            // Create and save the stimulus context
            var it = new ItemContext(this, ffItem, itemId, itemType);
            mStimContexts.AddLast(it);

            // Check for filename match
            if (!ffItem.Name.Equals(string.Format("stim-{0}-{1}", bankKey, itemId), StringComparison.OrdinalIgnoreCase))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                    "Stimulus ID doesn't match file/folder name", "bankKey='{0}' itemId='{1}' foldername='{2}'", bankKey,
                    itemId, ffItem);
            }

            // count wordlist reference
            CountWordlistReferences(it, xml);
        }

        private void TabulateItem_Pass2(ItemContext it)
        {
            switch (it.ItemType)
            {
                case "EBSR": // Evidence-Based Selected Response
                case "eq": // Equation
                case "er": // Extended-Response
                case "gi": // Grid Item (graphic)
                case "htq": // Hot Text (QTI)
                case "mc": // Multiple Choice
                case "mi": // Match Interaction
                case "ms": // Multi-Select
                case "sa": // Short Answer
                case "ti": // Table Interaction
                case "wer": // Writing Extended Response
                    TabulateInteraction(it);
                    break;

                case "nl": // Natural Language
                case "SIM": // Simulation
                    ReportingUtility.ReportError(it, ErrorCategory.Unsupported, ErrorSeverity.Severe,
                        "Item type is not fully supported by the open source TDS.", "itemType='{0}'", it.ItemType);
                    TabulateInteraction(it);
                    break;

                case "wordList": // Word List (Glossary)
                    TabulateWordList(it);
                    break;

                case "pass": // Passage
                    TabulatePassage(it);
                    break;

                case "tut": // Tutorial
                    TabulateTutorial(it);
                    break;

                default:
                    ReportingUtility.ReportError(it, ErrorCategory.Unsupported, ErrorSeverity.Severe,
                        "Unexpected item type.", "itemType='{0}'", it.ItemType);
                    break;
            }
        }

        private void TabulateInteraction(ItemContext it)
        {
            // Read the item XML
            var xml = new XDocument();
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", out xml))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid item file.",
                    LoadXmlErrorDetail);
                return;
            }

            if (Program.gValidationOptions.IsEnabled("cdt"))
            {
                var isCDataValid = CDataExtractor.ExtractCData(xml.Root)
                    .Select(
                        x =>
                            CDataValidator.IsValid(x, it,
                                x.Parent.Name.LocalName.Equals("val", StringComparison.OrdinalIgnoreCase)
                                    ? ErrorSeverity.Benign
                                    : ErrorSeverity.Degraded)).ToList();
                // These are the legacy checks against the CData values
                if (!it.ItemType.Equals("wordlist", StringComparison.OrdinalIgnoreCase))
                {
                    var contentAndWordlist = CDataValidator.ValidateContentAndWordlist(it, xml.Root);
                }
            }

            IList<ItemScoring> scoringInformation = new List<ItemScoring>();
            // Load metadata
            var xmlMetadata = new XDocument();
            if (!TryLoadXml(it.FfItem, "metadata.xml", out xmlMetadata))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid metadata.xml.",
                    LoadXmlErrorDetail);
            }
            else
            {
                scoringInformation = IrtExtractor.RetrieveIrtInformation(xmlMetadata).ToList();
            }
            if (!scoringInformation.Any())
            {
                scoringInformation.Add(new ItemScoring());
            }

            // Check interaction type
            var metaItemType = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:InteractionType", sXmlNs);
            if (!string.Equals(metaItemType, it.ItemType.ToUpperInvariant(), StringComparison.Ordinal))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                    "Incorrect metadata <InteractionType>.", "InteractionType='{0}' Expected='{1}'", metaItemType,
                    it.ItemType.ToUpperInvariant());
            }

            // DepthOfKnowledge
            var depthOfKnowledge = DepthOfKnowledgeFromMetadata(xmlMetadata, sXmlNs);

            // Get the version
            var version = xml.XpEvalE("itemrelease/item/@version");

            // Subject
            var subject = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_item_subject']/val");
            var metaSubject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            if (string.IsNullOrEmpty(subject))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Attribute, ErrorSeverity.Tolerable,
                    "Missing subject in item attributes (itm_item_subject).");
                subject = metaSubject;
                if (string.IsNullOrEmpty(subject))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                        "Missing subject in item metadata.");
                }
            }
            else
            {
                if (!string.Equals(subject, metaSubject, StringComparison.Ordinal))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                        "Subject mismatch between item and metadata.", "ItemSubject='{0}' MetadataSubject='{1}'",
                        subject, metaSubject);
                }
            }

            // AllowCalculator
            var allowCalculator = AllowCalculatorFromMetadata(xmlMetadata, sXmlNs);
            if (string.IsNullOrEmpty(allowCalculator) &&
                (string.Equals(metaSubject, "MATH", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(subject, "MATH", StringComparison.OrdinalIgnoreCase)))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded,
                    "Allow Calculator field not present for MATH subject item");
            }

            // MathematicalPractice
            var mathematicalPractice = MathematicalPracticeFromMetadata(xmlMetadata, sXmlNs);
            if (string.IsNullOrEmpty(mathematicalPractice) &&
                (string.Equals(metaSubject, "MATH", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(subject, "MATH", StringComparison.OrdinalIgnoreCase)))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded,
                    "Mathematical Practice field not present for MATH subject item");
            }

            // MaximumNumberOfPoints
            int testInt;
            var maximumNumberOfPoints = MaximumNumberOfPointsFromMetadata(xmlMetadata, sXmlNs);
            if (string.IsNullOrEmpty(maximumNumberOfPoints) || !int.TryParse(maximumNumberOfPoints, out testInt))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded,
                    "MaximumNumberOfPoints field not present in metadata");
            }

            // Grade
            var grade = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_att_Grade']/val").Trim();
            var metaGrade = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:IntendedGrade", sXmlNs);
            if (string.IsNullOrEmpty(grade))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Attribute, ErrorSeverity.Tolerable,
                    "Missing grade in item attributes (itm_att_Grade).");
                grade = metaGrade;
                if (string.IsNullOrEmpty(grade))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                        "Missing <IntendedGrade> in item metadata.");
                }
            }
            else
            {
                if (!string.Equals(grade, metaGrade, StringComparison.Ordinal))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                        "Grade mismatch between item and metadata.", "ItemGrade='{0}', MetadataGrade='{1}'", grade,
                        metaGrade);
                }
            }

            // Answer Key and Rubric
            var answerKey = string.Empty;
            {
                var answerKeyValue = string.Empty;
                var xmlEle =
                    xml.SelectSingleNode("itemrelease/item/attriblist/attrib[@attid='itm_att_Answer Key']") as XElement;
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
                    if (machineScoringType.Length > 0)
                    {
                        machineScoringType = machineScoringType.Substring(1);
                    }
                    if (!it.FfItem.FileExists(machineScoringFilename))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe,
                            "Machine scoring file not found.", "Filename='{0}'", machineScoringFilename);
                    }
                }

                var metadataScoringEngine = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:ScoringEngine",
                    sXmlNs);

                // Annswer key type is dictated by item type
                var scoringType = ScoringType.Basic;
                string metadataExpected = null;
                switch (it.ItemType)
                {
                    case "mc": // Multiple Choice
                        metadataExpected = "Automatic with Key";
                        if (answerKeyValue.Length != 1 || answerKeyValue[0] < 'A' || answerKeyValue[0] > 'Z')
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe,
                                "Unexpected MC answer key attribute.", "itm_att_Answer Key='{0}'", answerKeyValue);
                        }
                        answerKey = answerKeyValue;
                        scoringType = ScoringType.Basic;
                        break;

                    case "ms": // Multi-select
                        metadataExpected = "Automatic with Key(s)";
                    {
                        var parts = answerKeyValue.Split(',');
                        var validAnswer = parts.Length > 0;
                        foreach (var answer in parts)
                        {
                            if (answer.Length != 1 || answer[0] < 'A' || answer[0] > 'Z')
                            {
                                validAnswer = false;
                            }
                        }
                        if (!validAnswer)
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe,
                                "Unexpected MS answer attribute.", "itm_att_Answer Key='{0}'", answerKeyValue);
                        }
                        answerKey = answerKeyValue;
                        scoringType = ScoringType.Basic;
                    }
                        break;

                    case "EBSR": // Evidence-based selected response
                    {
                        metadataExpected = "Automatic with Key(s)";
                        if (answerKeyValue.Length != 1 || answerKeyValue[0] < 'A' || answerKeyValue[0] > 'Z')
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe,
                                "Unexpected EBSR answer key attribute.", "itm_att_Answer Key='{0}'", answerKeyValue);
                        }

                        // Retrieve the answer key for the second part of the EBSR
                        xmlEle =
                            xml.SelectSingleNode(
                                "itemrelease/item/attriblist/attrib[@attid='itm_att_Answer Key (Part II)']") as XElement;
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
                            ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Benign,
                                "Missing EBSR answer key part II attribute.");
                        }
                        else
                        {
                            var parts = answerKeyPart2.Split(',');
                            var validAnswer = parts.Length > 0;
                            foreach (var answer in parts)
                            {
                                if (answer.Length != 1 || answer[0] < 'A' || answer[0] > 'Z')
                                {
                                    validAnswer = false;
                                }
                            }
                            if (validAnswer)
                            {
                                answerKeyValue = string.Concat(answerKeyValue, ";", answerKeyPart2);
                            }
                            else
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe,
                                    "Unexpected EBSR Key Part II attribute.", "itm_att_Answer Key (Part II)='{0}'",
                                    answerKeyPart2);
                            }
                        }
                        answerKey = answerKeyValue;
                        scoringType = ScoringType.Qrx;
                        // Basic scoring could be achieved but the current implementation uses Qrx
                    }
                        break;

                    case "eq": // Equation
                    case "gi": // Grid Item (graphic)
                    case "htq": // Hot Text (in wrapped-QTI format)
                    case "mi": // Match Interaction
                    case "ti": // Table Interaction
                        metadataExpected = machineScoringFilename != null
                            ? "Automatic with Machine Rubric"
                            : "HandScored";
                        answerKey = machineScoringType;
                        if (!string.Equals(answerKeyValue, it.ItemType.ToUpperInvariant()))
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe,
                                "Unexpected answer key attribute.", "Value='{0}' Expected='{1}'", answerKeyValue,
                                it.ItemType.ToUpperInvariant());
                        }
                        scoringType = ScoringType.Qrx;
                        break;

                    case "er": // Extended-Response
                    case "sa": // Short Answer
                    case "wer": // Writing Extended Response
                        metadataExpected = "HandScored";
                        if (!string.Equals(answerKeyValue, it.ItemType.ToUpperInvariant()))
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Tolerable,
                                "Unexpected answer key attribute.", "Value='{0}' Expected='{1}'", answerKeyValue,
                                it.ItemType.ToUpperInvariant());
                        }
                        answerKey = ScoringType.Hand.ToString();
                        scoringType = ScoringType.Hand;
                        break;

                    default:
                        ReportingUtility.ReportError(it, ErrorCategory.Unsupported, ErrorSeverity.Benign,
                            "Validation of scoring keys for this type is not supported.");
                        answerKey = string.Empty;
                        scoringType = ScoringType.Basic; // We don't really know.
                        break;
                }

                // Count the answer key types
                mAnswerKeyCounts.Increment(string.Concat(it.ItemType, " '", answerKey, "'"));

                // Check Scoring Engine metadata
                if (metadataExpected != null &&
                    !string.Equals(metadataScoringEngine, metadataExpected, StringComparison.Ordinal))
                {
                    if (string.Equals(metadataScoringEngine, metadataExpected, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign,
                            "Capitalization error in ScoringEngine metadata.", "Found='{0}' Expected='{1}'",
                            metadataScoringEngine, metadataExpected);
                    }
                    else
                    {
                        // If first word of scoring engine metadata is the same (e.g. both are "Automatic" or both are "HandScored") then error is benign, otherwise error is tolerable
                        if (string.Equals(metadataScoringEngine.FirstWord(), metadataExpected.FirstWord(),
                            StringComparison.OrdinalIgnoreCase))
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign,
                                "Incorrect ScoringEngine metadata.", "Found='{0}' Expected='{1}'", metadataScoringEngine,
                                metadataExpected);
                        }
                        else
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                                "Automatic/HandScored scoring metadata error.", "Found='{0}' Expected='{1}'",
                                metadataScoringEngine, metadataExpected);
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
                        &&
                        (machineScoringFilename == null ||
                         !string.Equals(fi.Name, machineScoringFilename, StringComparison.OrdinalIgnoreCase)))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Severe,
                            "Machine scoring file found but not referenced in <MachineRubric> element.",
                            "Filename='{0}'", fi.Name);
                    }
                }

                // If non-embedded answer key (either hand-scored or QRX scoring but not EBSR type check for a rubric (human scoring guidance)
                // We only care about english rubrics (at least for the present)
                if (scoringType != ScoringType.Basic && !it.ItemType.Equals("EBSR", StringComparison.OrdinalIgnoreCase) &&
                    !(xml.SelectSingleNode("itemrelease/item/content[@language='ENU']/rubriclist/rubric/val") is
                        XElement))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.AnswerKey, ErrorSeverity.Tolerable,
                        "Hand-scored or QRX-scored item lacks a human-readable rubric",
                        $"AnswerKey: '{answerKey}'");
                }
            }

            // AssessmentType (PT or CAT)
            string assessmentType;
            {
                var meta = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PerformanceTaskComponentItem", sXmlNs);
                if (meta == null || string.Equals(meta, "N", StringComparison.Ordinal))
                {
                    assessmentType = "CAT";
                }
                else if (string.Equals(meta, "Y", StringComparison.Ordinal))
                {
                    assessmentType = "PT";
                }
                else
                {
                    assessmentType = "CAT";
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded,
                        "PerformanceTaskComponentItem metadata should be 'Y' or 'N'.", "Value='{0}'", meta);
                }
            }

            var primaryStandards = new List<ItemStandard>();
            var secondaryStandards = new List<ItemStandard>();
            if (!string.IsNullOrEmpty(xmlMetadata.OuterXml()))
            {
                primaryStandards = ItemStandardExtractor.Extract(xmlMetadata.Root).ToList();
                secondaryStandards =
                    ItemStandardExtractor.Extract(xmlMetadata.Root, "SecondaryStandard").ToList();
            }
            if (primaryStandards.Any(x => string.IsNullOrEmpty(x.Standard)))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded,
                    "Common Core Standard not included in PrimaryStandard metadata.");
            }

            // Validate claim
            if (primaryStandards.Any(x => !sValidClaims.Contains(x.Claim)))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded,
                    "Unexpected claim value (Should be 1, 2, 3, or 4 with possible suffix).", "Claim='{0}'",
                    primaryStandards.First(x => !sValidClaims.Contains(x.Claim)).Claim);
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


            // Validate content segments
            // Get the wordlist ID
            var xp = $"itemrelease/{(it.IsPassage ? "passage" : "item")}/resourceslist/resource[@type='wordList']/@id";
            var wordlistId = xml.XpEval(xp);

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

            var standardClaimTarget = new ReportingStandard(primaryStandards, secondaryStandards);

            // Check for silencing tags
            if (Program.gValidationOptions.IsEnabled("tss"))
            {
                var primaryStandard = primaryStandards.FirstOrDefault();
                if (primaryStandard != null && HasTtsSilencingTags(xml)
                    && !primaryStandard.Claim.Split('-').FirstOrDefault().Equals("2")
                    && !primaryStandard.Target.Split('-').FirstOrDefault().Equals("9"))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable,
                        "Item has improper TTS Silencing Tag", "subject='{0}' claim='{1}' target='{2}'", subject,
                        primaryStandard.Claim, primaryStandard.Target);
                }
            }

            if (!it.IsPassage && Program.gValidationOptions.IsEnabled("asl") &&
                CheckForAttachment(it, xml, "ASL", "MP4"))
            {
                AslVideoValidator.Validate(it, xml);
            }

            Console.WriteLine($"Tabulating {it.ItemId}");

            var scoringSeparation = scoringInformation.GroupBy(
                    x => !string.IsNullOrEmpty(x.Domain) && x.Domain.Equals("paper", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (Program.gValidationOptions.IsEnabled("dsk"))
            {
                // Folder,ItemId,ItemType,Version,Subject,Grade,AnswerKey,AsmtType,WordlistId,ASL,BrailleType,Translation,Media,Size,DepthOfKnowledge,AllowCalculator,
                // MathematicalPractice, MaxPoints, CommonCore, ClaimContentTarget, SecondaryCommonCore, SecondaryClaimContentTarget, measurementmodel, scorepoints,
                // dimension, weight, parameters
                mItemReport.WriteLine(string.Join(",", ReportingUtility.CsvEncode(it.Folder),
                    ReportingUtility.CsvEncode(it.ItemId), ReportingUtility.CsvEncode(it.ItemType),
                    ReportingUtility.CsvEncode(version), ReportingUtility.CsvEncode(subject),
                    ReportingUtility.CsvEncode(grade), ReportingUtility.CsvEncode(answerKey),
                    ReportingUtility.CsvEncode(assessmentType), ReportingUtility.CsvEncode(wordlistId),
                    ReportingUtility.CsvEncode(asl), ReportingUtility.CsvEncode(brailleType),
                    ReportingUtility.CsvEncode(translation), ReportingUtility.CsvEncode(media), size.ToString(),
                    ReportingUtility.CsvEncode(depthOfKnowledge), ReportingUtility.CsvEncode(allowCalculator),
                    ReportingUtility.CsvEncode(mathematicalPractice), ReportingUtility.CsvEncode(maximumNumberOfPoints),
                    ReportingUtility.CsvEncode(standardClaimTarget.PrimaryCommonCore),
                    ReportingUtility.CsvEncode(standardClaimTarget.PrimaryClaimsContentTargets),
                    ReportingUtility.CsvEncode(standardClaimTarget.SecondaryCommonCore),
                    ReportingUtility.CsvEncode(standardClaimTarget.SecondaryClaimsContentTargets),
                    ReportingUtility.CsvEncode(
                        scoringSeparation.FirstOrDefault(x => !x.Key)?
                            .Select(x => x.MeasurementModel)
                            .Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                    ReportingUtility.CsvEncode(
                        scoringSeparation.FirstOrDefault(x => !x.Key)?
                            .Select(x => x.ScorePoints)
                            .Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                    ReportingUtility.CsvEncode(
                        scoringSeparation.FirstOrDefault(x => !x.Key)?
                            .Select(x => x.Dimension)
                            .Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                    ReportingUtility.CsvEncode(
                        scoringSeparation.FirstOrDefault(x => !x.Key)?
                            .Select(x => x.Weight)
                            .Aggregate((x, y) => $"{x};{y}") ??
                        string.Empty),
                    ReportingUtility.CsvEncode(
                        scoringSeparation.FirstOrDefault(x => !x.Key)?
                            .Select(x => x.GetParameters())
                            .Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                    ReportingUtility.CsvEncode(
                        scoringSeparation.FirstOrDefault(x => x.Key)?
                            .Select(x => x.MeasurementModel)
                            .Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                    ReportingUtility.CsvEncode(
                        scoringSeparation.FirstOrDefault(x => x.Key)?
                            .Select(x => x.ScorePoints)
                            .Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                    ReportingUtility.CsvEncode(
                        scoringSeparation.FirstOrDefault(x => x.Key)?
                            .Select(x => x.Dimension)
                            .Aggregate((x, y) => $"{x};{y}") ?? string.Empty),
                    ReportingUtility.CsvEncode(
                        scoringSeparation.FirstOrDefault(x => x.Key)?
                            .Select(x => x.Weight)
                            .Aggregate((x, y) => $"{x};{y}") ??
                        string.Empty),
                    ReportingUtility.CsvEncode(
                        scoringSeparation.FirstOrDefault(x => x.Key)?
                            .Select(x => x.GetParameters())
                            .Aggregate((x, y) => $"{x};{y}") ?? string.Empty)));
            }

            // === Tabulation is complete, check for other errors

            // Points
            {
                var itemPoint = xml.XpEval("itemrelease/item/attriblist/attrib[@attid='itm_att_Item Point']/val");
                if (string.IsNullOrEmpty(itemPoint))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable,
                        "Item Point attribute (item_att_Item Point) not found.");
                }
                else
                {
                    // Item Point attribute may have a suffix such as "pt", "pt.", " pt", " pts" and other variations.
                    // TODO: In seeking consistency, we may make this more picky in the future.
                    itemPoint = itemPoint.Trim();
                    if (!char.IsDigit(itemPoint[0]))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable,
                            "Item Point attribute does not begin with an integer.", "itm_att_Item Point='{0}'",
                            itemPoint);
                    }
                    else
                    {
                        var points = itemPoint.ParseLeadingInteger();

                        // See if matches MaximumNumberOfPoints (defined as optional in metadata)
                        var metaPoint = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:MaximumNumberOfPoints",
                            sXmlNs);
                        if (metaPoint == null)
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                                "MaximumNumberOfPoints not found in metadata.");
                        }
                        else
                        {
                            int mpoints;
                            if (!int.TryParse(metaPoint, out mpoints))
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                                    "Metadata MaximumNumberOfPoints value is not integer.",
                                    "MaximumNumberOfPoints='{0}'", metaPoint);
                            }
                            else if (mpoints != points)
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                                    "Metadata MaximumNumberOfPoints does not match item point attribute.",
                                    "MaximumNumberOfPoints='{0}' itm_att_Item Point='{0}'", mpoints, points);
                            }
                        }

                        // See if matches ScorePoints (defined as optional in metadata)
                        var scorePoints = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:ScorePoints", sXmlNs);
                        if (scorePoints == null)
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign,
                                "ScorePoints not found in metadata.");
                        }
                        else
                        {
                            scorePoints = scorePoints.Trim();
                            if (scorePoints[0] == '"')
                            {
                                scorePoints = scorePoints.Substring(1);
                            }
                            else
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                                    "ScorePoints value missing leading quote.");
                            }
                            if (scorePoints[scorePoints.Length - 1] == '"')
                            {
                                scorePoints = scorePoints.Substring(0, scorePoints.Length - 1);
                            }
                            else
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                                    "ScorePoints value missing trailing quote.");
                            }

                            var maxspoints = -1;
                            var minspoints = 100000;
                            foreach (var sp in scorePoints.Split(','))
                            {
                                int spoints;
                                if (!int.TryParse(sp.Trim(), out spoints))
                                {
                                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                                        "Metadata ScorePoints value is not integer.", "ScorePoints='{0}' value='{1}'",
                                        scorePoints, sp);
                                }
                                else if (spoints < 0 || spoints > points)
                                {
                                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Severe,
                                        "Metadata ScorePoints value is out of range.",
                                        "ScorePoints='{0}' value='{1}' min='0' max='{2}'", scorePoints, spoints, points);
                                }
                                else
                                {
                                    if (maxspoints < spoints)
                                    {
                                        maxspoints = spoints;
                                    }
                                    else
                                    {
                                        ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign,
                                            "Metadata ScorePoints are not in ascending order.", "ScorePoints='{0}'",
                                            scorePoints);
                                    }
                                    if (minspoints > spoints)
                                    {
                                        minspoints = spoints;
                                    }
                                }
                            }
                            if (minspoints > 0)
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign,
                                    "Metadata ScorePoints doesn't include a zero score.", "ScorePoints='{0}'",
                                    scorePoints);
                            }
                            if (maxspoints < points)
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                                    "Metadata ScorePoints doesn't include a maximum score.",
                                    "ScorePoints='{0}' max='{1}'", scorePoints, points);
                            }
                        }
                    }
                }
            }

            // Performance Task Details
            if (string.Equals(assessmentType, "PT", StringComparison.OrdinalIgnoreCase))
            {
                // PtSequence
                int seq;
                var ptSequence = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PtSequence", sXmlNs);
                if (ptSequence == null)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded,
                        "Metadata for PT item is missing <PtSequence> element.");
                }
                else if (!int.TryParse(ptSequence.Trim(), out seq))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Degraded,
                        "Metadata <PtSequence> is not an integer.", "PtSequence='{0}'", ptSequence);
                }

                // PtWritingType Metadata (defined as optional in metadata but we'll still report a benign error if it's not on PT WER items)
                if (string.Equals(it.ItemType, "wer", StringComparison.OrdinalIgnoreCase))
                {
                    var ptWritingType = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:PtWritingType", sXmlNs);
                    if (ptWritingType == null)
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign,
                            "Metadata for PT item is missing <PtWritingType> element.");
                    }
                    else
                    {
                        ptWritingType = ptWritingType.Trim();
                        if (!sValidWritingTypes.Contains(ptWritingType))
                        {
                            // Fix capitalization
                            var normalized = string.Concat(ptWritingType.Substring(0, 1).ToUpperInvariant(),
                                ptWritingType.Substring(1).ToLowerInvariant());

                            // Report according to type of error
                            if (!sValidWritingTypes.Contains(normalized))
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign,
                                    "PtWritingType metadata has invalid value.", "PtWritingType='{0}'", ptWritingType);
                            }
                            else
                            {
                                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign,
                                    "Capitalization error in PtWritingType metadata.",
                                    "PtWritingType='{0}' expected='{1}'", ptWritingType, normalized);
                            }
                        }
                    }
                }

                // Stimulus (Passage) ID
                var stimId = xml.XpEval("itemrelease/item/attriblist/attrib[@attid='stm_pass_id']/val");
                if (stimId == null)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                        "PT Item missing associated passage ID (stm_pass_id).");
                }
                else
                {
                    var metaStimId = xmlMetadata.XpEval("metadata/sa:smarterAppMetadata/sa:AssociatedStimulus", sXmlNs);
                    if (metaStimId == null)
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                            "PT Item metatadata missing AssociatedStimulus.");
                    }
                    else if (!string.Equals(stimId, metaStimId, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                            "PT Item passage ID doesn't match metadata AssociatedStimulus.",
                            "Item stm_pass_id='{0}' Metadata AssociatedStimulus='{1}'", stimId, metaStimId);
                    }

                    // Get the bankKey
                    var bankKey = xml.XpEvalE("itemrelease/item/@bankkey");

                    // Look for the stimulus
                    var stimulusFilename = string.Format(@"Stimuli\stim-{1}-{0}\stim-{1}-{0}.xml", stimId, bankKey);
                    if (!mPackageFolder.FileExists(stimulusFilename))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                            "PT item stimulus not found.", "StimulusId='{0}'", stimId);
                    }

                    // Make sure dependency is recorded in manifest
                    CheckDependencyInManifest(it, stimulusFilename, "Stimulus");
                }
            } // if Performance Task

            // Check for tutorial
            {
                var tutorialId = xml.XpEval("itemrelease/item/tutorial/@id");
                if (tutorialId == null)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                        "Tutorial id missing from item.");
                }
                else if (Program.gValidationOptions.IsEnabled("trd"))
                {
                    var bankKey = xml.XpEval("itemrelease/item/tutorial/@bankkey");

                    // Look for the tutorial
                    var tutorialFilename = string.Format(@"Items\item-{1}-{0}\item-{1}-{0}.xml", tutorialId, bankKey);
                    if (!mPackageFolder.FileExists(tutorialFilename))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Tutorial not found.",
                            "TutorialId='{0}'", tutorialId);
                    }

                    // Make sure dependency is recorded in manifest
                    CheckDependencyInManifest(it, tutorialFilename, "Tutorial");
                }
            }
        } // TablulateInteraction

        private void TabulatePassage(ItemContext it)
        {
            // Read the item XML
            var xml = new XDocument();
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", out xml))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid item file.",
                    LoadXmlErrorDetail);
                return;
            }

            if (Program.gValidationOptions.IsEnabled("cdt"))
            {
                var isCDataValid = CDataExtractor.ExtractCData(xml.Root)
                    .Select(
                        x =>
                            CDataValidator.IsValid(x, it,
                                x.Parent.Name.LocalName.Equals("val", StringComparison.OrdinalIgnoreCase)
                                    ? ErrorSeverity.Benign
                                    : ErrorSeverity.Degraded)).ToList();
                // These are the legacy checks against the CData values
                if (!it.ItemType.Equals("wordlist", StringComparison.OrdinalIgnoreCase))
                {
                    var contentAndWordlist = CDataValidator.ValidateContentAndWordlist(it, xml.Root);
                }
            }

            // Load the metadata
            var xmlMetadata = new XDocument();
            if (!TryLoadXml(it.FfItem, "metadata.xml", out xmlMetadata))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid metadata.xml.",
                    LoadXmlErrorDetail);
            }

            // Check interaction type
            var metaItemType = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:InteractionType", sXmlNs);
            if (!string.Equals(metaItemType, cStimulusInteractionType, StringComparison.Ordinal))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                    "Incorrect metadata <InteractionType>.", "InteractionType='{0}' Expected='{1}'", metaItemType,
                    cStimulusInteractionType);
            }

            // Get the version
            var version = xml.XpEvalE("itemrelease/passage/@version");

            // Subject
            string subject = xml.XpEvalE("itemrelease/passage/attriblist/attrib[@attid='itm_item_subject']/val");
            string metaSubject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            if (string.IsNullOrEmpty(subject))
            {
                // For the present, we don't expect the subject in the item attributes on passages
                //ReportingUtility.ReportError(it, ErrorCategory.Attribute, ErrorSeverity.Tolerable, "Missing subject in item attributes (itm_item_subject).");
                subject = metaSubject;
                if (string.IsNullOrEmpty(subject))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                        "Missing subject in item metadata.");
                }
            }
            else
            {
                if (!string.Equals(subject, metaSubject, StringComparison.Ordinal))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                        "Subject mismatch between item and metadata.", "ItemSubject='{0}' MetadataSubject='{1}'",
                        subject, metaSubject);
                }
            }

            // Grade: Passages do not have a particular grade affiliation
            var grade = string.Empty;

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

            // Get the wordlist ID
            var xp = $"itemrelease/{(it.IsPassage ? "passage" : "item")}/resourceslist/resource[@type='wordList']/@id";
            var wordlistId = xml.XpEval(xp);

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

            // WordCount
            var wordCount = GetWordCount(it, xml);

            if (Program.gValidationOptions.IsEnabled("dsk"))
            {
                // Folder,StimulusId,Version,Subject,WordlistId,ASL,BrailleType,Translation,Media,Size,WordCount
                mStimulusReport.WriteLine(string.Join(",", ReportingUtility.CsvEncode(it.Folder),
                    ReportingUtility.CsvEncode(it.ItemId), ReportingUtility.CsvEncode(version),
                    ReportingUtility.CsvEncode(subject), ReportingUtility.CsvEncode(wordlistId),
                    ReportingUtility.CsvEncode(asl), ReportingUtility.CsvEncode(brailleType),
                    ReportingUtility.CsvEncode(translation), ReportingUtility.CsvEncode(media), size.ToString(),
                    wordCount.ToString()));
            }
        } // TabulatePassage


        private void TabulateTutorial(ItemContext it)
        {
            // Read the item XML
            var xml = new XDocument();
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", out xml))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid item file.",
                    LoadXmlErrorDetail);
                return;
            }

            // Read the metadata
            var xmlMetadata = new XDocument();
            if (!TryLoadXml(it.FfItem, "metadata.xml", out xmlMetadata))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid metadata.xml.",
                    LoadXmlErrorDetail);
            }

            // Get the version
            var version = xml.XpEvalE("itemrelease/item/@version");

            // Subject
            var subject = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_item_subject']/val");
            string metaSubject = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Subject", sXmlNs);
            if (string.IsNullOrEmpty(subject))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Attribute, ErrorSeverity.Tolerable,
                    "Missing subject in item attributes (itm_item_subject).");
                subject = metaSubject;
                if (string.IsNullOrEmpty(subject))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                        "Missing subject in item metadata.");
                }
            }
            else
            {
                if (!string.Equals(subject, metaSubject, StringComparison.Ordinal))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                        "Subject mismatch between item and metadata.", "ItemSubject='{0}' MetadataSubject='{1}'",
                        subject, metaSubject);
                }
            }

            // Grade
            var grade = xml.XpEvalE("itemrelease/item/attriblist/attrib[@attid='itm_att_Grade']/val");
            // will return "NA" or empty

            // Answer Key
            var answerKey = string.Empty; // Not applicable

            // AssessmentType (PT or CAT)
            var assessmentType = string.Empty; // Not applicable

            // Standard, Claim and Target (not applicable
            var standard = string.Empty;
            var claim = string.Empty;
            var target = string.Empty;

            // Get the wordlist ID
            var xp = $"itemrelease/{(it.IsPassage ? "passage" : "item")}/resourceslist/resource[@type='wordList']/@id";
            var wordlistId = xml.XpEval(xp);

            // ASL
            var asl = GetAslType(it, xml, xmlMetadata);

            // BrailleType
            var brailleType = GetBrailleType(it, xml, xmlMetadata);

            // Translation
            var translation = GetTranslation(it, xml, xmlMetadata);

            if (Program.gValidationOptions.IsEnabled("dsk"))
            {
                // Folder,ItemId,ItemType,Version,Subject,Grade,AnswerKey,AsmtType,WordlistId,ASL,BrailleType,Translation,Media,Size,DepthOfKnowledge,AllowCalculator,MathematicalPractice, MaxPoints, 
                // CommonCore, ClaimContentTarget, SecondaryCommonCore, SecondaryClaimContentTarget, CAT_MeasurementModel,
                // CAT_ScorePoints, CAT_Dimension, CAT_Weight,CAT_Parameters, PP_MeasurementModel
                // PP_ScorePoints,PP_Dimension,PP_Weight,PP_Parameters
                mItemReport.WriteLine(string.Join(",", ReportingUtility.CsvEncode(it.Folder),
                    ReportingUtility.CsvEncode(it.ItemId), ReportingUtility.CsvEncode(it.ItemType),
                    ReportingUtility.CsvEncode(version), ReportingUtility.CsvEncode(subject),
                    ReportingUtility.CsvEncode(grade), ReportingUtility.CsvEncode(answerKey),
                    ReportingUtility.CsvEncode(assessmentType), ReportingUtility.CsvEncode(wordlistId),
                    ReportingUtility.CsvEncode(asl), ReportingUtility.CsvEncode(brailleType),
                    ReportingUtility.CsvEncode(translation),
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty));
            }
        } // TabulateTutorial

        public static bool TryLoadXml(FileFolder ff, string filename, out XDocument xml)
        {
            FileFile ffXml;
            if (!ff.TryGetFile(filename, out ffXml))
            {
                LoadXmlErrorDetail = $"filename='{Path.GetFileName(filename)}' detail='File not found'";
                xml = null;
                return false;
            }
            using (var reader = new StreamReader(ffXml.Open(), Encoding.UTF8, true, 1024, false))
            {
                try
                {
                    xml = XDocument.Load(reader);
                }
                catch (Exception err)
                {
                    LoadXmlErrorDetail = $"filename='{Path.GetFileName(filename)}' detail='{err.Message}'";
                    xml = null;
                    return false;
                }
            }
            return true;
        }

        private static bool CheckForAttachment(ItemContext it, XDocument xml, string attachType,
            string expectedExtension)
        {
            var fileName = FileUtility.GetAttachmentFilename(it, xml, attachType);
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }
            if (!it.FfItem.FileExists(fileName))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                    "Dangling reference to attached file that does not exist.", "attachType='{0}' Filename='{1}'",
                    attachType, fileName);
                return false;
            }

            var extension = Path.GetExtension(fileName);
            if (extension.Length > 0)
            {
                extension = extension.Substring(1); // Strip leading "."
            }
            if (!string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                    "Unexpected extension for attached file.",
                    "attachType='{0}' extension='{1}' expected='{2}' filename='{3}'", attachType, extension,
                    expectedExtension, fileName);
            }
            return true;
        }

        private static void ReportUnexpectedFiles(ItemContext it, string fileType, string regexPattern,
            params object[] args)
        {
            var regex = new Regex(string.Format(regexPattern, args));
            foreach (var file in it.FfItem.Files)
            {
                var match = regex.Match(file.Name);
                if (match.Success)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Benign,
                        "Unreferenced file found.", "fileType='{0}', filename='{1}'", fileType, file.Name);
                }
            }
        }

        private void CheckDependencyInManifest(ItemContext it, string dependencyFilename, string dependencyType)
        {
            // Suppress manifest checks if the manifest is empty
            if (mFilenameToResourceId.Count == 0)
            {
                return;
            }

            // Look up item in manifest
            string itemResourceId = null;
            var itemFilename = string.Concat(it.FfItem.RootedName, "/", it.FfItem.Name, ".xml");
            if (!mFilenameToResourceId.TryGetValue(NormalizeFilenameInManifest(itemFilename), out itemResourceId))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                    "Item not found in manifest.");
            }

            // Look up dependency in the manifest
            string dependencyResourceId = null;
            if (
                !mFilenameToResourceId.TryGetValue(NormalizeFilenameInManifest(dependencyFilename),
                    out dependencyResourceId))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                    dependencyType + " not found in manifest.", "DependencyFilename='{0}'", dependencyFilename);
            }

            // Check for dependency in manifest
            if (!string.IsNullOrEmpty(itemResourceId) && !string.IsNullOrEmpty(dependencyResourceId))
            {
                if (!mResourceDependencies.Contains(ToDependsOnString(itemResourceId, dependencyResourceId)))
                {
                    ReportingUtility.ReportError("pmd", it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                        string.Format("Manifest does not record dependency between item and {0}.", dependencyType),
                        "ItemResourceId='{0}' {1}ResourceId='{2}'", itemResourceId, dependencyType, dependencyResourceId);
                }
            }
        }

        private string GetAslType(ItemContext it, XDocument xml, XDocument xmlMetadata)
        {
            var aslFound = CheckForAttachment(it, xml, "ASL", "MP4");
            if (!aslFound)
            {
                ReportUnexpectedFiles(it, "ASL video", "^item_{0}_ASL", it.ItemId);
            }

            var aslInMetadata =
                string.Equals(
                    xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AccessibilityTagsASLLanguage", sXmlNs), "Y",
                    StringComparison.OrdinalIgnoreCase);
            if (aslInMetadata && !aslFound)
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Severe,
                    "Item metadata specifies ASL but no ASL in item.");
            }
            if (!aslInMetadata && aslFound)
            {
                ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Tolerable,
                    "Item has ASL but not indicated in the metadata.");
            }

            return aslFound && aslInMetadata ? "MP4" : string.Empty;
        }

        public static string GetBrailleType(ItemContext it, XDocument xml, XDocument xmlMetadata)
        {
            // First, check metadata
            var brailleTypeMeta = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:BrailleType", sXmlNs);

            var brailleTypes = new SortedSet<string>(new BrailleTypeComparer());
            var brailleFiles = new List<BrailleFile>();

            var validTypes = Enum.GetNames(typeof(BrailleCode)).ToList();

            // Enumerate all of the braille attachments
            {
                var type = it.IsPassage ? "passage" : "item";
                var attachmentXPath = $"itemrelease/{type}/content/attachmentlist/attachment";
                var fileExtensionsXPath = $"itemrelease/{type}/content/attachmentlist/attachment/@file";
                var extensions = xml.SelectNodes(fileExtensionsXPath)?
                    .Cast<XAttribute>().Select(x => x.Value)
                    .GroupBy(x => x.Split('.').LastOrDefault());
                var fileTypes = extensions.Select(x => x.Key.ToLower()).ToList();
                if (fileTypes.Contains("brf") && fileTypes.Contains("prn"))
                    // We have more than one Braille file extension present
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                        "More than one braille embossing file type extension present in attachment list",
                        $"File Types: [{fileTypes.Aggregate((x, y) => $"{x}|{y}")}]");
                }

                var processedIds = new List<string>();

                foreach (XElement xmlEle in xml.SelectNodes(attachmentXPath))
                {
                    // All attachments must have an ID and those IDs must be unique within their item
                    var id = xmlEle.GetAttribute("id");
                    if (string.IsNullOrEmpty(id))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                            "Attachment missing id attribute.");
                    }
                    else if (processedIds.Contains(id))
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
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                            "Attachment missing type attribute.");
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
                        ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Severe,
                            "Braille metadata does not match attachment type.", "metadata='{0}', fileType='{1}'",
                            brailleTypeMeta, attachType);
                    }

                    // Check that the file exists
                    var filename = xmlEle.GetAttribute("file");
                    const string validFilePattern =
                        @"(stim|item|passage)_(\d+)_(enu)_(\D{3}|uncontracted|contracted|nemeth)(_transcript)*\.(brf|prn)";
                    if (string.IsNullOrEmpty(filename))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                            "Attachment missing file attribute.", "attachType='{0}'", attachType);
                        continue;
                    }
                    if (!it.FfItem.FileExists(filename))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Tolerable,
                            "Braille embossing file is missing.",
                            "attachType='{0}' Filename='{1}'", attachType, filename);
                    }

                    // Check the extension
                    var extension = Path.GetExtension(filename);
                    if (extension.Length > 0)
                    {
                        extension = extension.Substring(1); // Strip leading "."
                    }
                    if (!string.Equals(extension, attachType, StringComparison.OrdinalIgnoreCase))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                            "Braille embossing filename has unexpected extension.",
                            "extension='{0}' expected='{1}' filename='{2}'",
                            extension, attachType, filename);
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
                    if (Regex.IsMatch(filename, validFilePattern))
                        // We are not checking for these values if it's not a match.
                    {
                        var itemOrStimText = it.IsPassage ? "stim" : "item";
                        if (!match.Groups[1].Value.Equals(itemOrStimText, StringComparison.OrdinalIgnoreCase))
                            // item or stim
                        {
                            if (it.IsPassage &&
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
                        if (!match.Groups[2].Value.Equals(it.ItemId, StringComparison.OrdinalIgnoreCase))
                            // item id
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe,
                                $"Braille embossing filename indicates item ID mismatch.",
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
                        else if (!string.IsNullOrEmpty(subtype) &&
                                 !match.Groups[4].Value.Equals(subtype.Split('_').First(),
                                     StringComparison.OrdinalIgnoreCase))
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                                "Braille embossing filename doesn't match expected braille type.",
                                $"Embossing Braille Type: {match.Groups[4].Value} Expected Braille Type: " +
                                $"{subtype.Split('_').First()} Filename: {filename}");
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
                            $"Filename: {filename}");
                    }

                    // Report the result
                    var brailleFile = string.IsNullOrEmpty(subtype)
                        ? attachType.ToUpperInvariant()
                        : string.Concat(attachType.ToUpperInvariant(), "(", subtype.ToLowerInvariant(), ")");
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
                foreach (var xmlBraille in xml.SelectNodes("//brailleText").OfType<XElement>())
                {
                    foreach (var node in xmlBraille.DescendantNodes())
                    {
                        if (node.NodeType == XmlNodeType.Element &&
                            (string.Equals(((XElement) node).Name.LocalName, "brailleTextString",
                                 StringComparison.Ordinal)
                             || string.Equals(((XElement) node).Name.LocalName, "brailleCode", StringComparison.Ordinal)))
                        {
                            if (node.InnerText().Length != 0)
                            {
                                var brailleEmbedded = string.Equals(((XElement) node).Name.LocalName,
                                    "brailleTextString", StringComparison.Ordinal)
                                    ? "Embed"
                                    : "EmbedCode";
                                var brailleType = ((XElement) node).GetAttribute("type");
                                if (!string.IsNullOrEmpty(brailleType))
                                {
                                    brailleEmbedded = string.Concat(brailleEmbedded, "(", brailleType.ToLowerInvariant(),
                                        ")");
                                }
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
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Benign,
                        "brailleTextString and/or brailleCode element is empty.");
                }
            }

            var brailleList = BrailleUtility.GetSupportByCode(brailleFiles);
            // Check for match with metadata
            // Metadata MUST take precedence over contents.
            if (string.Equals(brailleTypeMeta, "Not Braillable", StringComparison.OrdinalIgnoreCase))
            {
                if (brailleTypes.Count != 0)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign,
                        "Metadata indicates not braillable but braille content included.", "brailleTypes='{0}'",
                        string.Join(";", brailleTypes));
                }
                brailleTypes.Clear();
                brailleTypes.Add("NotBraillable");
                brailleList.Clear();
                brailleList.Add(BrailleSupport.NOTBRAILLABLE);
            }
            else if (string.IsNullOrEmpty(brailleTypeMeta))
            {
                brailleTypes.Clear(); // Don't report embedded braille markup if there is no attachment
                brailleList.Clear();
            }

            return brailleFiles.FirstOrDefault()?.Type + (brailleList.Any()
                       ? "|" + brailleList
                             .Select(x => x.ToString())
                             .Distinct()
                             .Aggregate((y, z) => $"{y};{z}")
                       : string.Empty);
        }

        private static bool HasTtsSilencingTags(XNode xml)
        {
            return xml.SelectNodes("//readAloud/textToSpeechPronunciation").Cast<XElement>()
                .Any(node => node.InnerText().Length == 0);
        }

        // Returns the Wordlist ID
        private string CountWordlistReferences(ItemContext it, XDocument xml)
        {
            var wordlistId = string.Empty;
            var xp = it.IsPassage
                ? "itemrelease/passage/resourceslist/resource[@type='wordList']"
                : "itemrelease/item/resourceslist/resource[@type='wordList']";

            foreach (XElement xmlRes in xml.SelectNodes(xp))
            {
                var witId = xmlRes.GetAttribute("id");
                if (string.IsNullOrEmpty(witId))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                        "Item references blank wordList id.");
                }
                else
                {
                    if (!string.IsNullOrEmpty(wordlistId))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Degraded,
                            "Item references multiple wordlists.");
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

        private static string GetXmlContext(XNode node)
        {
            var context = string.Empty;
            while (node != null && node.NodeType != XmlNodeType.Document)
            {
                context = string.Concat("/", node.Cast()?.Name.LocalName ?? string.Empty, context);
                node = node.Parent;
            }
            return context;
        }

        private string GetTranslation(ItemContext it, XDocument xml, XDocument xmlMetadata)
        {
            // Find non-english content and the language value
            var languages = new HashSet<string>();
            foreach (
                XElement xmlEle in
                xml.SelectNodes(it.IsPassage ? "itemrelease/passage/content" : "itemrelease/item/content"))
            {
                var language = xmlEle.GetAttribute("language").ToLowerInvariant();

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
                var node =
                    xmlMetadata.SelectSingleNode(
                            string.Concat("metadata/sa:smarterAppMetadata/sa:Language[. = '", language, "']"), sXmlNs)
                        as
                        XNode;
                if (node == null)
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Benign,
                        "Item content includes language but metadata does not have a corresponding <Language> entry.",
                        "Language='{0}'", language);
                }
            }

            var translation = string.Empty;

            // Now, search the metadata for translations and make sure all exist in the content
            foreach (XElement xmlEle in xmlMetadata.SelectNodes("metadata/sa:smarterAppMetadata/sa:Language", sXmlNs))
            {
                var language = xmlEle.InnerText();
                if (!languages.Contains(language))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Metadata, ErrorSeverity.Severe,
                        "Item metadata indicates language but item content does not include that language.",
                        "Language='{0}'", language);
                }

                // If not english, add to result
                if (!string.Equals(language, "eng", StringComparison.Ordinal))
                {
                    translation = translation.Length > 0 ? string.Concat(translation, " ", language) : language;
                }
            }

            return translation;
        }

        private string GetMedia(ItemContext it, XDocument xml)
        {
            //if (it.ItemId.Equals("1117", StringComparison.Ordinal)) Debugger.Break();

            // First get the list of attachments so that they are not included in the media list
            var attachments = new HashSet<string>();
            foreach (
                XElement xmlEle in
                xml.SelectNodes(it.IsPassage
                    ? "itemrelease/passage/content/attachmentlist/attachment"
                    : "itemrelease/item/content/attachmentlist/attachment"))
            {
                var filename = xmlEle.GetAttribute("file").ToLowerInvariant();
                if (!string.IsNullOrEmpty(filename))
                {
                    attachments.Add(filename);
                }
            }

            // Get the content string so we can verify that media files are referenced.
            var content = string.Empty;
            foreach (
                XElement xmlEle in
                xml.SelectNodes(it.IsPassage ? "itemrelease/passage/content/stem" : "itemrelease/item/content/stem"))
            {
                content += xmlEle.InnerText();
            }

            // Enumerate all files and select the media
            var mediaList = new SortedSet<string>();
            foreach (var file in it.FfItem.Files)
            {
                var filename = file.Name;
                if (attachments.Contains(filename.ToLowerInvariant()))
                {
                    continue;
                }

                var ext = Path.GetExtension(filename);
                if (ext.Length > 0)
                {
                    ext = ext.Substring(1).ToUpperInvariant(); // Drop the leading period
                }
                if (sMediaFileTypes.Contains(ext))
                {
                    // Make sure media file is referenced
                    if (Program.gValidationOptions.IsEnabled("umf") &&
                        content.IndexOf(filename, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Benign,
                            "Media file not referenced in item.", "Filename='{0}'", filename);
                    }
                    else
                    {
                        mediaList.Add(ext);
                    }
                }
            }

            if (mediaList.Count == 0)
            {
                return string.Empty;
            }
            return string.Join(";", mediaList);
        }

        private long GetItemSize(ItemContext it)
        {
            long size = 0;
            foreach (var f in it.FfItem.Files)
            {
                size += f.Length;
            }
            return size;
        }

        private long GetWordCount(ItemContext it, XDocument xml)
        {
            var content = string.Empty;
            int index = 0, wordCount = 0;
            foreach (
                XElement xmlEle in
                xml.SelectNodes(it.IsPassage ? "itemrelease/passage/content/stem" : "itemrelease/item/content/stem"))
            {
                content = xmlEle.InnerText();

                // strip HTML
                content = Regex.Replace(content, @"<[^>]+>|&nbsp;", "").Trim();
                // replace the non-breaking HTML character &#xA0; with a blank
                content = content.Replace("&#xA0;", "");

                // calculate word count
                while (index < content.Length)
                {
                    // check if current char is part of a word.  whitespace, hypen and slash are word terminators
                    while (index < content.Length && char.IsWhiteSpace(content[index]) == false &&
                           !content[index].Equals("-") && !content[index].Equals("/"))
                    {
                        index++;
                    }

                    wordCount++;

                    // skip whitespace, hypen, slash and stand alone punctuation marks until next word
                    while (index < content.Length &&
                           (char.IsWhiteSpace(content[index]) ||
                            content[index].Equals("-") ||
                            content[index].Equals("/") ||
                            Regex.IsMatch(content[index].ToString(), @"[\p{P}]")))
                    {
                        index++;
                    }
                }
            }
            return wordCount;
        }

        private static string DepthOfKnowledgeFromMetadata(XNode xmlMetadata, XmlNamespaceManager xmlNamespaceManager)
        {
            return xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:DepthOfKnowledge", xmlNamespaceManager);
        }

        private static string MathematicalPracticeFromMetadata(XNode xmlMetadata,
            XmlNamespaceManager xmlNamespaceManager)
        {
            return xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:MathematicalPractice", xmlNamespaceManager);
        }

        private static string AllowCalculatorFromMetadata(XNode xmlMetadata, XmlNamespaceManager xmlNamespaceManager)
        {
            return xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:AllowCalculator", xmlNamespaceManager);
        }

        private static string MaximumNumberOfPointsFromMetadata(XNode xmlMetadata,
            XmlNamespaceManager xmlNamespaceManager)
        {
            return xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:MaximumNumberOfPoints", xmlNamespaceManager);
        }

        private void TabulateWordList(ItemContext it)
        {
            // Read the item XML
            var xml = new XDocument();
            if (!TryLoadXml(it.FfItem, it.FfItem.Name + ".xml", out xml))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Item, ErrorSeverity.Severe, "Invalid wordlist file.",
                    LoadXmlErrorDetail);
                return;
            }

            if (Program.gValidationOptions.IsEnabled("cdt"))
            {
                var isCDataValid = CDataExtractor.ExtractCData(xml.Root)
                    .Select(
                        x =>
                            CDataValidator.IsValid(x, it,
                                x.Parent.Name.LocalName.Equals("val", StringComparison.OrdinalIgnoreCase)
                                    ? ErrorSeverity.Benign
                                    : ErrorSeverity.Degraded)).ToList();
                // These are the legacy checks against the CData values
                if (!it.ItemType.Equals("wordlist", StringComparison.OrdinalIgnoreCase))
                {
                    var contentAndWordlist = CDataValidator.ValidateContentAndWordlist(it, xml.Root);
                }
            }

            // Count this wordlist
            ++mWordlistCount;

            // See if the wordlist has been referenced
            var refCount = mWordlistRefCounts.Count(it.ItemId);
            if (refCount == 0)
            {
                ReportingUtility.ReportError(it, ErrorCategory.Wordlist, ErrorSeverity.Benign,
                    "Wordlist is not referenced by any item.");
            }

            // Zero the counts
            var termcount = 0;
            var maxgloss = 0;
            var mingloss = int.MaxValue;
            var totalgloss = 0;

            // Enumerate all terms and count glossary entries
            foreach (XNode kwNode in xml.SelectNodes("itemrelease/item/keywordList/keyword"))
            {
                ++mGlossaryTermCount;
                ++termcount;

                // Count this instance of the term
                var term = kwNode.XpEval("@text");
                mTermCounts.Increment(term);

                var glosscount = 0;
                foreach (XNode htmlNode in kwNode.SelectNodes("html"))
                {
                    ++glosscount;
                }

                if (maxgloss < glosscount)
                {
                    maxgloss = glosscount;
                }
                if (mingloss > glosscount)
                {
                    mingloss = glosscount;
                }
                totalgloss += glosscount;
            }

            if (mingloss == int.MaxValue)
            {
                mingloss = 0;
            }

            if (Program.gValidationOptions.IsEnabled("dsk"))
            {
                //Folder,WIT_ID,RefCount,TermCount,MaxGloss,MinGloss,AvgGloss
                mWordlistReport.WriteLine(string.Join(",", it.Folder, ReportingUtility.CsvEncode(it.ItemId),
                    refCount.ToString(), termcount.ToString(), maxgloss.ToString(), mingloss.ToString(),
                    termcount > 0 ? (totalgloss / (double) termcount).ToString("f2") : "0"));
            }
        }

        // This is kind of ugly with so many parameters but it's the cleanest way to handle this task that's repeated multiple times


        private void ValidateManifest()
        {
            // Prep an itemcontext for reporting errors
            var it = new ItemContext(this, mPackageFolder, null, null);

            // Load the manifest
            var xmlManifest = new XDocument();
            if (!TryLoadXml(mPackageFolder, cImsManifest, out xmlManifest))
            {
                ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign, "Invalid manifest.",
                    LoadXmlErrorDetail);
                return;
            }

            // Keep track of every resource id mentioned in the manifest
            var ids = new HashSet<string>();

            // Enumerate all resources in the manifest
            foreach (XElement xmlRes in xmlManifest.SelectNodes("ims:manifest/ims:resources/ims:resource", sXmlNs))
            {
                var id = xmlRes.GetAttribute("identifier");
                if (string.IsNullOrEmpty(id))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                        "Resource in manifest is missing id.", "Filename='{0}'",
                        xmlRes.XpEvalE("ims:file/@href", sXmlNs));
                }
                var filename = xmlRes.XpEval("ims:file/@href", sXmlNs);
                if (string.IsNullOrEmpty(filename))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                        "Resource specified in manifest has no filename.", "ResourceId='{0}'", id);
                }
                else if (!mPackageFolder.FileExists(filename))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                        "Resource specified in manifest does not exist.", "ResourceId='{0}' Filename='{1}'", id,
                        filename);
                }

                if (ids.Contains(id))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                        "Resource listed multiple times in manifest.", "ResourceId='{0}'", id);
                }
                else
                {
                    ids.Add(id);
                }

                // Normalize the filename
                filename = NormalizeFilenameInManifest(filename);
                if (mFilenameToResourceId.ContainsKey(filename))
                {
                    ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                        "File listed multiple times in manifest.", "ResourceId='{0}' Filename='{1}'", id, filename);
                }
                else
                {
                    mFilenameToResourceId.Add(filename, id);
                }

                // Index any dependencies
                foreach (XElement xmlDep in xmlRes.SelectNodes("ims:dependency", sXmlNs))
                {
                    var dependsOnId = xmlDep.GetAttribute("identifierref");
                    if (string.IsNullOrEmpty(dependsOnId))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                            "Dependency in manifest is missing identifierref attribute.", "ResourceId='{0}'", id);
                    }
                    else
                    {
                        var dependency = ToDependsOnString(id, dependsOnId);
                        if (mResourceDependencies.Contains(dependency))
                        {
                            ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                                "Dependency in manifest repeated multiple times.", "ResourceId='{0}' DependsOnId='{1}'",
                                id, dependsOnId);
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
                ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign, "Manifest is empty.");
                return;
            }

            // Enumerate all files and check for them in the manifest
            {
                foreach (var ff in mPackageFolder.Folders)
                {
                    ValidateDirectoryInManifest(it, ff);
                }
            }
        }

        // Recursively check that files exist in the manifest
        private void ValidateDirectoryInManifest(ItemContext it, FileFolder ff)
        {
            // See if this is an item or stimulus directory
            string itemId = null;
            if (ff.Name.StartsWith("item-", StringComparison.OrdinalIgnoreCase) ||
                ff.Name.StartsWith("stim-", StringComparison.OrdinalIgnoreCase))
            {
                FileFile fi;
                if (ff.TryGetFile(string.Concat(ff.Name, ".xml"), out fi))
                {
                    var itemFileName = NormalizeFilenameInManifest(fi.RootedName);

                    if (!mFilenameToResourceId.TryGetValue(itemFileName, out itemId))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                            "Item does not appear in the manifest.", "ItemFilename='{0}'", itemFileName);
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
                    ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                        "Resource does not appear in the manifest.", "Filename='{0}'", filename);
                }

                // If in an item, see if dependency is expressed
                else if (itemId != null && !string.Equals(itemId, resourceId, StringComparison.Ordinal))
                {
                    // Check for dependency
                    if (!mResourceDependencies.Contains(ToDependsOnString(itemId, resourceId)))
                    {
                        ReportingUtility.ReportError(it, ErrorCategory.Manifest, ErrorSeverity.Benign,
                            "Manifest does not express resource dependency.", "ResourceId='{0}' DependesOnId='{1}'",
                            itemId, resourceId);
                    }
                }
            }

            // Recurse
            foreach (var ffSub in ff.Folders)
            {
                ValidateDirectoryInManifest(it, ffSub);
            }
        }

        private static string NormalizeFilenameInManifest(string filename)
        {
            filename = filename.ToLowerInvariant().Replace('\\', '/');
            return filename[0] == '/' ? filename.Substring(1) : filename;
        }

        private static string ToDependsOnString(string itemId, string dependsOnId)
        {
            return string.Concat(itemId, "~", dependsOnId);
        }

        private void SummaryReport(TextWriter writer)
        {
            if (Program.gValidationOptions.IsEnabled("dsk"))
            {
                writer.WriteLine("Errors: {0}", ReportingUtility.ErrorCount);
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
                writer.WriteLine("Answer Key Counts:");
                mAnswerKeyCounts.Dump(writer);
                writer.WriteLine();
                writer.WriteLine("Glossary Terms Used in Wordlists:");
                mTermCounts.Dump(writer);
                writer.WriteLine();
            }
        }

        private class BrailleTypeComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                // Make "PRN" sort between "BRF" and "Embed"
                if (x.StartsWith("PRN", StringComparison.Ordinal))
                {
                    x = "C" + x.Substring(3);
                }
                if (y.StartsWith("PRN", StringComparison.Ordinal))
                {
                    y = "C" + y.Substring(3);
                }
                return string.CompareOrdinal(x, y);
            }
        }
    }

    internal static class TabulatorHelp
    {
        private static readonly char[] cWhitespace = {' ', '\t', '\r', '\n'};

        public static string XpEval(this XNode doc, string xpath, XmlNamespaceManager xmlns = null)
        {
            var node = xmlns != null ? doc.SelectSingleNode(xpath, xmlns) : doc.SelectSingleNode(xpath);
            if (node is XElement)
            {
                return ((XElement) node)?.InnerText();
            }
            return ((XAttribute) node)?.Value;
        }

        public static string XpEvalE(this XNode doc, string xpath, XmlNamespaceManager xmlns = null)
        {
            var result = (IEnumerable) doc?.XPathEvaluate(xpath, xmlns);
            var element = result?.OfType<XElement>()?.ToList();
            var attribute = result?.OfType<XAttribute>()?.ToList();
            if (element?.FirstOrDefault() != null)
            {
                return element.FirstOrDefault()?.Value;
            }
            return attribute?.FirstOrDefault() != null ? attribute.FirstOrDefault()?.Value : null;
        }

        public static XNode NextNode(this XNode node, XNode withinSubtree = null)
        {
            if (node == null)
            {
                throw new NullReferenceException("Null passed to NextNode.");
            }

            return node.FirstChild() ?? (node.NextNode ?? node.Parent?.NextNode);
        }

        public static void Increment(this Dictionary<string, int> dict, string key)
        {
            int count;
            if (!dict.TryGetValue(key, out count))
            {
                count = 0;
            }
            dict[key] = count + 1;
        }

        public static int Count(this Dictionary<string, int> dict, string key)
        {
            int count;
            if (!dict.TryGetValue(key, out count))
            {
                count = 0;
            }
            return count;
        }

        public static void Dump(this Dictionary<string, int> dict, TextWriter writer)
        {
            var list = new List<KeyValuePair<string, int>>(dict);
            list.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
            {
                var diff = b.Value - a.Value;
                return diff != 0 ? diff : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
            });
            foreach (var pair in list)
            {
                writer.WriteLine("{0,6}: {1}", pair.Value, pair.Key);
            }
        }

        public static string FirstWord(this string str)
        {
            str = str?.Trim();
            var space = str?.IndexOfAny(cWhitespace) ?? 0;
            return space > 0 ? str?.Substring(0, space) : str;
        }

        public static int ParseLeadingInteger(this string str)
        {
            str = str.Trim();
            var i = 0;
            foreach (var c in str)
            {
                if (!char.IsDigit(c))
                {
                    return i;
                }
                i = i * 10 + (c - '0');
            }
            return i;
        }
    }

    internal class XmlSubtreeEnumerable : IEnumerable<XNode>
    {
        private readonly XNode m_root;

        public XmlSubtreeEnumerable(XNode root)
        {
            m_root = root;
        }

        public IEnumerator<XNode> GetEnumerator()
        {
            return new XmlSubtreeEnumerator(m_root);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new XmlSubtreeEnumerator(m_root);
        }
    }

    internal class XmlSubtreeEnumerator : IEnumerator<XNode>
    {
        private readonly XNode m_root;
        private bool m_atEnd;
        private XNode m_current;

        public XmlSubtreeEnumerator(XNode root)
        {
            m_root = root;
            Reset();
        }

        public void Reset()
        {
            m_current = null;
            m_atEnd = false;
        }

        public XNode Current
        {
            get
            {
                if (m_current == null)
                {
                    throw new InvalidOperationException("");
                }
                return m_current;
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (m_atEnd)
            {
                return false;
            }
            if (m_current == null)
            {
                m_current = m_root;
                if (m_current == null)
                {
                    m_atEnd = true;
                }
            }
            else
            {
                var next = m_current.FirstChild();
                if (next == null)
                {
                    next = m_current.NextNode;
                }
                if (next == null)
                {
                    next = m_current;
                    for (;;)
                    {
                        next = next.Parent;
                        if (ReferenceEquals(m_root, next))
                        {
                            next = null;
                            m_atEnd = true;
                            break;
                        }
                        if (next.NextNode != null)
                        {
                            next = next.NextNode;
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