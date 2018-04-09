using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace TabulateSmarterTestContentPackage
{
    internal class Program
    {
// 78 character margin                                                       |
        private const string cSyntax =
@"Syntax: TabulateSmarterTestContentPackage [options] [-ids <filename>] <packageMoniker> ...

Command Line Examples:
    ""D:\Packages\MyPackage.zip""
        Tabulates the test package contained in ""MyPackage.zip"" reports are
        ""MyPackage_SummaryReport.txt"", ""MyPackage_ItemReport.csv"" and so
        forth.

    ""D:\Packages\*.zip""
        Tabulates all "".zip"" test packages in the ""D:\Packages"" folder.

    -a ""D:\Packages\*.zip""
        Tabulates all "".zip"" test packages in teh ""D:\Packages"" folder
        and aggregates the results into one set of reports. The names being
        ""Aggregate_SummaryReport.txt"", ""Aggregate_ItemReport.csv"" and so
        forth.

    ""D:\Packages\MyElaPackage.zip"" ""D:\Packages\MyMathPackage.zip""
        Tabulates both ""MyElaPackage.zip"" and ""MyMathPackage.zip"" with
        separate results for each.

    ""D:\Packages\MyElaPackage.zip"" ""D:\Packages\MyMathPackage.zip"" -ids ""mathids.txt""
        Tabulates both ""MyElaPackage.zip"" and ""MyMathPackage.zip"" with
        separate results for each. In ""MyMathPackage.zip"" only the items
        with the ids listed in ""mathids.txt"" are included.

    -a ""D:\Packages\MyElaPackage.zip"" ""D:\Packages\MyMathPackage.zip""
        Tabulates both packages and aggregates the results into one set
        of reports.

    ""D:\Packages\MyElaPackage""
        Tabulates the package represented by the ""MyElaPackage"" file
        folder.

    -bank https://itembank.org -at hs3kwBlt8sorK9qwp8Wr -o ""D:\Tabulations\ItemBank""
        Tabulates the entire contents of the specified item bank using
        the corresponding access token and stores the results in a set
        of reports at ""D:\Tabulations\ItemBank"".

    -bank https://itembank.org -at hs3kwBlt8sorK9qwp8Wr -ids ""myPullList.csv"" -o ""ItemBank""
        Tabulates the items identifed by ids in ""myPullList.csv"" and stored
        in the item bank. Includes stimuli, wordlists, and tutorials
        referenced by the items in the tabulation. Reports are placed
        in the current directory with ""ItemBank"" for the prefix.

Arguments:
    -a               Aggregate the results of all tabulations into one set of
                     reports.
    -v-<opt>         Disable a particular validation option (see below)
    -v+<opt>         Enable a particular validation option
    -h               Display this help text
    -bk <id>         The bankkey to use when one is not specfied in an id
                     file (see the -ids argument). If not included, the
                     default bankkey is 200.
    -ids <filename>  Optional name of a file containing item IDs to be
                     tabulated in the preceding package. If this file is not
                     specfied then all items in the package will be tabulated.
                     This argument may be repeated once for each package. (See
                     below for details.)
    -o <path>        The path prefix for the output (report) files of the
                     preceding package. If not specified, defaults to the
                     package file or directory name. Required when tabulating
                     an item bank.
    -lid             Indicates that a list of IDs should be reported during
                     the item selection pass.
    -lidx            Indicates that a list of IDs should be reported and that
                     the tabulation should exit after the selection pass - not
                     generating the other reports.
    -rbrk            Export rubrics into HTML files so that they can be
                     examined. The rubrics will be in a folder located in the
                     same place as the reports.
    -dedup           Only report the first instance of an error on a
                     particular item (De-duplicate).
    -w               Wait for a keypress for exiting - helpful when not
                     running from a command-line window.
    <packageMoniker> The filename of a local package or the identifier of an
                     online item bank. (See below for details.)
    
Package Moniker
    Local Packages may be in .zip format, or unpacked into a folder tree.
    Tabulating an online item bank using GitLab is also supported.

    .zip package: A .zip package is a content package collected into a .zip
    file. The moniker is the filename (or complete path) of the .zip file.
    Wildcards (* and ?) are acceptable in which case all matching packages
    will be tabulated. The .zip package must have an 'imsmanifext.xml' file
    in the root.

    Folder Package: A content package may be unpacked into a file system
    folder tree and tabulated that way. The moniker is the name (or complete
    path) of the root folder of the package. Wildcards (* and ?) are
    acceptable in which case all matching packages will be tabulated. The
    package must have an 'imsmanifest.xml' file in the root.

    Item Bank: An item bank moniker consists of three parts of which two are
    optional.
    -bank <url>           (Optional) The URL of the GitLab item bank from which
                          the items will be drawn. If not specified, defaults
                          to ""https://itembank.smarterbalanced.org"".
    -ns <namespace>       (Optional) The GitLab namespace to which the items
                          belong. This should be a username or a group name.
                          If not specified, defaults to ""itemreviewapp"".
    -at <token>           (Required) A GitLab access token valid on the item
                          bank. See below on how to generate the token.

    You may specify multiple packages of any type and in any mix of types.
    Each package may have an an associated Item IDs file (-ids argument) and
    each package may specify an output file prefix (-o argument). When
    included, the IDs and output arguments must follow the moniker of the
    package.

Access Token
    To generate an item bank access token, do the following:
    1. Log into the GitLab item bank.
    2. Access your user profile (by clicking on your account icon in the upper-
       right).
    3. Edit your profile (by clicking on the pencil icon in the upper-right.
    4. Select ""access tokens"" from the menu.
    5. Give the token a name and expiration date. We recommend expiration no
       no longer than 3 months. Select ""API"" for the scope. Then click
       ""Create personal access token.""

Aggregate Tabulation
    When multiple packages are specified, by default they are tabulated
    individually - each with its own set of reports. The aggregate option
    (-a) tabulates all of the packages and aggregates the results into one set
    of reports.

Item ID File:
    The optional Item ID file, specified by the '-ids' argument, is a list of
    IDs for items that should be included in the tabulation. It may be a flat
    list or in CSV format.

    Flat List Format:
    In this format there is one item ID per line. IDs may be the bare number
    (e.g. ""12345"") or they may be the full item name including
    the ""Item-"" prefix(e.g. ""item-200-12345""). If the ID is a bare number
    than the bank key specified by the ""-bk"" parameter or default of ""200""
    will be used.

    CSV Format:
    A CSV file should comply with RFC 4180. The first line should be a list of
    field names. One column MUST be named ""ItemId"" and it will be the source
    of the item ids. Another column MAY be named ""BankKey"". If so, it will be
    the source of bank keys. If not included, the default bank key (either 200
    or the value specified in the ""-bk"") will be used. As with flat list
    format, the item ID may be a bare integer or a full name including prefix.

    Specifying a Stimulus:
    Normally, stimuli are automatically included when an item is specified
    that depends on that stimulus. However, a stimulus may be explicitly
    included by using the full name (e.g. ""stim-200-6789"". This applies to
    both Flat List and CSV formats.

Report Output
    The tabulator generates six reports:
        <prefix>_SummaryReport.txt    Summary information including
                                      counts of items, errors, etc.
        <prefix>_ItemReport.csv       Items including key metadata.
        <prefix>_StimulusReport.csv   Stimuli including key metadata.
        <prefix>_ErrorReport.csv      Item and stimulus validation errors.
        <prefix>_WordlistReport.csv   All wordLists including glossary term
                                      counts.
        <prefix>_GlossaryReport.csv   A comprehensive list of every glossary
                                      term in the package.
    
    For .zip and Folder packages, the default prefix is the name of the .zip
    file or the folder. For example ""MyPackage_ItemReport.csv"". This may be
    overridden using the -o argument.

    For an item bank, the prefix is always specified by the -o argument.
    
    For aggregate reports, the default prefix is the folder of the first
    package in the list plus the word ""Aggregate"". For example,
    ""Aggregate_ItemReport.csv"". This may be overridden using the -o
    argument.

Validation Options:
    Validation options disable or enable the reporting of certain errors. Only
    a subset of validations can be controlled this way.

    -v-pmd  Passage Manifest Dependency: An item that depends on a passage
            should have that dependency represented in the manifest. This
            option disables checking for that dependency.
    -v-trd  Tutorial References and Dependencies: Disables checking for
            tutorial references and dependencies. This is valuable when
            tutorials are packaged separately from the balance of the
            content.
    -v-asl  ASL video: Disables checking for ASL video whose ratio of video
            length to item stim length falls outside of two standard
            deviations from mean (adjustible through app.config).
    -v-tss  Text-to-Speech Silencing: Disable check for TTS silencing tags
            that do not belong.
    -v-ugt  Untagged Glossary Terms: Disable check for glossary terms that
            are tagged in one case but not in another.
    -v-tgs  Target Grade Suffix: Disables checking whether the grade suffix in
            a target matches the grade alignment in the item attributes.

    -v+all  Enable all optional validation and tabulation features.
    -v+umf  Unreferenced Media File: Enables checking whether media files are
            referenced in the corresponding item, passage, or wordlist.
    -v+gtr  Glossary Text Report: Include glossary text in the Glossary
            Report output file.
    -v+uwt  Unreferenced Wordlist Terms: Reports an error when a wordlist
            term is not referenced by the corresponding item.
    -v+mwa  Missing Wordlist Attachments: Reports errors for missing wordlist
            attachments (audio and images) even when the corresponding term
            is not referenced by any item.
    -v+ats  Image Alternate Text for Spanish: Enables checking for alternate
            text on images in the stacked Spanish version of an item.
    -v+akv  Answer Key Value: Reports the actual answer key (e.g. 'C') in the
            ItemReport for selected response items. Without this option simply
            reports 'SR'.

Error severity definitions:
    Severe     The error will prevent the test item from functioning properly
               or from being scored properly.
    Degraded   The item will display and accept a student response but
               certain features will not work properly. For example, an
               accessibility feature may not function properly.
    Tolerable  The test delivery system can tolerate the problem and function
               without the student noticing anything. However, certain
               features may not be available to the student. For example, a
               glossary may be valuable for a term but it doesn’t appear (and
               the associated text is not highlighted).
    Benign     A problem that doesn’t impact test delivery in any way but may
               affect future item development. For example, a wordlist term
               may be missing data but if that term isn’t referenced in an
               item then it is benign.
";
// 78 character margin                                                       |

        class Operation
        {
            public string PackagePath;
            public string BankUrl;
            public string BankNamespace;
            public string BankAccessToken;
            public string IdFilename;
            public string ReportPrefix;
        }

        const string c_DefaultBankUrl = "https://itembank.smarterbalanced.org";
        const string c_DefaultBankNamespace = "itemreviewapp";
        const int c_DefaultBankKey = 200;
        const string c_AggregatePrefix = "Aggregate";

        public static ValidationOptions gValidationOptions = new ValidationOptions();

        // Parsed command line
        static List<Operation> s_operations = new List<Operation>();
        static string s_aggregateReportPrefix = null;
        static bool s_aggregate = false;
        static bool s_showHelp = false;
        static int s_bankKey = c_DefaultBankKey;
        static bool s_reportIds;
        static bool s_exitAfterIds;
        static bool s_exportRubrics;
        static bool s_deDuplicate;
        static bool s_waitBeforeExit;

        private static void Main(string[] args)
        {
            long startTicks = Environment.TickCount;

            // Default options
            gValidationOptions.Disable("umf"); // Disable checking for Unreferenced Media Files
            gValidationOptions.Disable("gtr"); // Disable Glossary Text Report
            gValidationOptions.Disable("uwt"); // Disable Unreferenced Wordlist Terms
            gValidationOptions.Disable("mwa"); // Disable checking for attachments on unreferenced wordlist terms
            gValidationOptions.Disable("css"); // Disable reporting css color-contrast interference (temporary fix)
            gValidationOptions.Disable("ats"); // Disable checking for image alt text in Spanish content.
            gValidationOptions.Disable("akv"); // Disable reporting answer key values.

            try
            {
                Models.TabulatorSettings.Load();
                ParseCommandLine(args);

                Console.WriteLine("Tabulator Flags");
                Console.WriteLine(Enumerable.Repeat("-",20).Aggregate((x,y) => $"{x}{y}"));

                gValidationOptions.Keys.ToList().ForEach(x =>
                {
                    Console.WriteLine($"[{x}: {gValidationOptions[x].ToString()}]");
                });

                Console.WriteLine(Enumerable.Repeat("-", 20).Aggregate((x, y) => $"{x}{y}"));
                Console.WriteLine();

                if (s_showHelp || s_operations.Count == 0)
                {
                    Console.WriteLine(cSyntax);
                }
                else if (s_aggregate)
                {
                    TabulateAggregate();
                }
                else
                {
                    TabulateEach();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            var elapsedTicks = unchecked((uint)Environment.TickCount - (uint)startTicks);
            Console.WriteLine("Elapsed time: {0}.{1:d3} seconds", elapsedTicks / 1000, elapsedTicks % 1000);

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                s_waitBeforeExit = true;
            }
#endif

            if (s_waitBeforeExit)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Press any key to exit.");
                Console.ReadKey(true);
            }
        }

        static void ParseCommandLine(string[] args)
        {
            Operation bankOperation = null;

            for (int i = 0; i < args.Length; ++i)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "-a":
                        s_aggregate = true;
                        break;

                    case "-h":
                        s_showHelp = true;
                        break;

                    case "-bk":
                        ++i;
                        if (i > args.Length) throw new ArgumentException("No value specified for '-bk' command-line argument.");
                        if (!int.TryParse(args[i], out s_bankKey)) throw new ArgumentException($"Value specified for '-bk' argument is not integer. ({args[i]})");
                        break;

                    case "-ids":
                        ++i;
                        if (i > args.Length) throw new ArgumentException("No value specified for '-ids' command-line argument.");
                        if (!File.Exists(args[i])) throw new ArgumentException($"'-ids' file not found. ({args[i]})");
                        EnqueueOperation(ref bankOperation); // Does nothing if there is no bank operation;
                        if (s_operations.Count == 0) throw new ArgumentException("'-ids' argument must follow a package or bank identification.");
                        if (s_operations.Last().IdFilename != null) throw new ArgumentException("Only one '-ids' argument may be specified per package.");
                        s_operations.Last().IdFilename = args[i];
                        break;

                    case "-lid":
                        s_reportIds = true;
                        break;

                    case "-lidx":
                        s_reportIds = true;
                        s_exitAfterIds = true;
                        break;

                    case "-rbrk":
                        s_exportRubrics = true;
                        break;

                    case "-o":
                        ++i;
                        if (i > args.Length) throw new ArgumentException("No value specified for '-o' command-line argument.");
                        {
                            string path = args[i];
                            if (!ValidateOutputPrefix(ref path)) throw new ArgumentException($"'-o' invalid filename or path not found. ({args[i]})");
                            EnqueueOperation(ref bankOperation); // Does nothing if there is no bank operation;
                            if (s_operations.Count == 0) throw new ArgumentException("'-o' argument must follow a package or bank identification.");
                            if (s_operations.Last().ReportPrefix != null) throw new ArgumentException("Only one '-o' argument may be specified per package.");
                            s_operations.Last().ReportPrefix = path;
                            if (s_aggregateReportPrefix == null) s_aggregateReportPrefix = path;
                        }
                        break;

                    case "-bank":
                        ++i;
                        if (i > args.Length) throw new ArgumentException("No value specified for '-bank' command-line argument.");
                        {
                            Uri uri;
                            if (!Uri.TryCreate(args[i], UriKind.Absolute, out uri)) throw new ArgumentException($"Invalid bank URL '{args[i]}");
                            if (bankOperation != null && bankOperation.BankUrl != null)
                            {
                                EnqueueOperation(ref bankOperation);
                            }
                            if (bankOperation == null) bankOperation = new Operation();
                            bankOperation.BankUrl = uri.ToString();
                        }
                        break;

                    case "-ns":
                        ++i;
                        if (i > args.Length) throw new ArgumentException("No value specified for '-ns' command-line argument.");
                        if (bankOperation != null && bankOperation.BankNamespace != null)
                        {
                            EnqueueOperation(ref bankOperation);
                        }
                        if (bankOperation == null) bankOperation = new Operation();
                        bankOperation.BankNamespace = args[i];
                        break;

                    case "-at":
                        ++i;
                        if (i > args.Length) throw new ArgumentException("No value specified for '-ns' command-line argument.");
                        if (bankOperation != null && bankOperation.BankAccessToken != null)
                        {
                            EnqueueOperation(ref bankOperation);
                        }
                        if (bankOperation == null) bankOperation = new Operation();
                        bankOperation.BankAccessToken = args[i];
                        break;

                    case "-dedup":
                        s_deDuplicate = true;
                        break;

                    case "-w":
                        s_waitBeforeExit = true;
                        break;

                    default:
                        if (arg.StartsWith("-v", StringComparison.OrdinalIgnoreCase) && (arg[2] == '-' || arg[2] == '+'))
                        {
                            var key = arg.Substring(3).ToLowerInvariant();
                            var value = arg[2] == '+';
                            if (key.Equals("all", StringComparison.Ordinal))
                            {
                                if (!value)
                                {
                                    throw new ArgumentException(
                                        "Invalid command-line option '-v-all'. Options must be disabled one at a time.");
                                }
                                gValidationOptions.EnableAll();
                            }
                            else
                            {
                                gValidationOptions[key] = value;
                            }
                        }
                        else
                        {
                            EnqueueOperation(ref bankOperation); // Does nothing if there is no bank operation;
                            string path = arg;
                            if (!ValidatePackagePath(ref path))
                            {
                                throw new ArgumentException("No match for path: " + arg);
                            }
                            Operation operation = new Operation();
                            operation.PackagePath = arg;
                            EnqueueOperation(ref operation);
                        }
                        break;
                }
            } // for each argument

            // If there's a pending bank operation enqueue it.
            EnqueueOperation(ref bankOperation);
        }

        static void EnqueueOperation(ref Operation operation)
        {
            if (operation == null) return;
            if (operation.PackagePath != null)
            {
                System.Diagnostics.Debug.Assert(operation.BankUrl == null);
                System.Diagnostics.Debug.Assert(operation.BankNamespace == null);
                System.Diagnostics.Debug.Assert(operation.BankAccessToken == null);
            }
            else
            {
                System.Diagnostics.Debug.Assert(operation.PackagePath == null);
                if (operation.BankAccessToken == null) throw new ArgumentException("Item Bank requires '-at' AccessToken argument.");
                if (operation.BankUrl == null) operation.BankUrl = c_DefaultBankUrl;
                if (operation.BankNamespace == null) operation.BankNamespace = c_DefaultBankNamespace;
            }
            s_operations.Add(operation);
            operation = null;
        }

        static void TabulateEach()
        {
            foreach (var operation in s_operations)
            {
                // Local package
                if (operation.PackagePath != null)
                {
                    bool zip = operation.PackagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                    string directory = Path.GetDirectoryName(operation.PackagePath);
                    string pattern = Path.GetFileName(operation.PackagePath);
                    string[] packages;
                    if (zip)
                    {
                        packages = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
                    }
                    else
                    {
                        packages = Directory.GetDirectories(directory, pattern, SearchOption.TopDirectoryOnly);
                    }
                    foreach (var packagePath in packages)
                    {
                        using (TestPackage package = zip ? (TestPackage)new ZipPackage(packagePath) : (TestPackage)new FsPackage(packagePath))
                        {

                            // Figure out the reporting prefix
                            string reportPrefix;
                            if (operation.ReportPrefix != null)
                            {
                                if (packages.Length > 1) // Wildcard
                                {
                                    reportPrefix = string.Concat(operation.ReportPrefix, "_", package.Name);
                                }
                                else
                                {
                                    reportPrefix = operation.ReportPrefix;
                                }
                            }
                            else
                            {
                                reportPrefix = zip ? packagePath.Substring(0, packagePath.Length-4) : packagePath;
                            }

                            // Tabulate the package
                            using (var tab = new Tabulator(reportPrefix))
                            {
                                tab.ReportIds = s_reportIds;
                                tab.ExitAfterSelect = s_exitAfterIds;
                                tab.ExportRubrics = s_exportRubrics;
                                tab.DeDuplicate = s_deDuplicate;
                                if (operation.IdFilename != null)
                                {
                                    tab.SelectItems(new IdReadable(operation.IdFilename, c_DefaultBankKey));
                                }
                                tab.Tabulate(package);
                            }
                        }
                    }
                }

                // Item bank
                else
                {
                    using (TestPackage package = new ItemBankPackage(operation.BankUrl, operation.BankAccessToken, operation.BankNamespace))
                    {
                        if (operation.ReportPrefix == null)
                        {
                            throw new ArgumentException("Item bank tabulation must specify '-o' report prefix.");
                        }
                        using (var tab = new Tabulator(operation.ReportPrefix))
                        {
                            tab.ReportIds = s_reportIds;
                            tab.ExitAfterSelect = s_exitAfterIds;
                            tab.ExportRubrics = s_exportRubrics;
                            tab.DeDuplicate = s_deDuplicate;
                            if (operation.IdFilename != null)
                            {
                                tab.SelectItems(new IdReadable(operation.IdFilename, c_DefaultBankKey));
                            }
                            tab.Tabulate(package);
                        }
                    }
                }
            }
        }

        static void TabulateAggregate()
        {
            // Figure out the reporting prefix
            string reportPrefix;
            if (s_aggregateReportPrefix  != null)
            {
                reportPrefix = s_aggregateReportPrefix;
            }
            else
            {
                string packagePath = s_operations[0].PackagePath;
                if (packagePath == null)
                {
                    throw new ArgumentException("Item bank tabulation must specify '-o' report prefix.");
                }
                reportPrefix = Path.Combine(Path.GetDirectoryName(packagePath), c_AggregatePrefix);
            }

            using (var tab = new Tabulator(reportPrefix))
            {
                tab.ReportIds = s_reportIds;
                tab.ExitAfterSelect = s_exitAfterIds;
                tab.ExportRubrics = s_exportRubrics;
                tab.DeDuplicate = s_deDuplicate;

                foreach (var operation in s_operations)
                {
                    // Local package
                    if (operation.PackagePath != null)
                    {
                        bool zip = operation.PackagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                        string directory = Path.GetDirectoryName(operation.PackagePath);
                        string pattern = Path.GetFileName(operation.PackagePath);
                        string[] packages;
                        if (zip)
                        {
                            packages = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
                        }
                        else
                        {
                            packages = Directory.GetDirectories(directory, pattern, SearchOption.TopDirectoryOnly);
                        }
                        foreach (var packageName in packages)
                        {
                            using (TestPackage package = zip ? (TestPackage)new ZipPackage(packageName) : (TestPackage)new FsPackage(packageName))
                            {
                                if (operation.IdFilename != null)
                                {
                                    tab.SelectItems(new IdReadable(operation.IdFilename, c_DefaultBankKey));
                                }
                                tab.Tabulate(package);
                            }
                        }
                    }

                    // Item bank package
                    else
                    {
                        using (TestPackage package = new ItemBankPackage(operation.BankUrl, operation.BankAccessToken, operation.BankNamespace))
                        {
                            if (operation.IdFilename != null)
                            {
                                tab.SelectItems(new IdReadable(operation.IdFilename, c_DefaultBankKey));
                            }
                            tab.Tabulate(package);
                        }
                    }
                }
            }
        }

        // Returns true if at least one file or directory matches the path
        // Updates the path to a full path
        static bool ValidatePackagePath(ref string path)
        {
            string directory = Path.GetFullPath(Path.GetDirectoryName(path));
            string filename = Path.GetFileName(path);
            if (!Directory.Exists(directory))
            {
                return false;
            }
            if (filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.GetFiles(directory, filename, SearchOption.TopDirectoryOnly).Length <= 0)
                {
                    return false;
                }
            }
            else
            {
                if (Directory.GetDirectories(directory, filename, SearchOption.TopDirectoryOnly).Length <= 0)
                {
                    return false;
                }
            }

            path = Path.Combine(directory, filename);
            return true;
        }

        // Returns true if the folder is valid and the file could be created
        static bool ValidateOutputPrefix(ref string path)
        {
            string directory = Path.GetFullPath(Path.GetDirectoryName(path));
            string fileprefix = Path.GetFileName(path);
            if (!Directory.Exists(directory))
            {
                return false;
            }
            path = Path.Combine(directory, fileprefix);
            return true;
        }

    }

    internal class ValidationOptions : Dictionary<string, bool>
    {
        public void Enable(string option)
        {
            this[option] = true;
        }

        public void Disable(string option)
        {
            this[option] = false;
        }

        public void EnableAll()
        {
            Clear(); // Since options default to enabled, clearing enables all.
        }

        public bool IsEnabled(string option)
        {
            bool value;
            return !TryGetValue(option, out value) || value;
        }
    }
}