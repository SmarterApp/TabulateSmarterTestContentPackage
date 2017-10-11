using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using TabulateSmarterTestContentPackage.Utilities;
using Win32Interop;
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

    ""D:\Packages\MyElaPackage.zip"" -ids ""mathids.txt"" ""D:\Packages\MyMathPackage.zip""
        Tabulates both ""MyElaPackage.zip"" and ""MyMathPackage.zip"" with
        separate results for each. In ""MyMathPackage.zip"" only the items
        with the ids listed in ""mathids.txt"" are included.

    -a ""D:\Packages\MyElaPackage.zip"" ""D:\Packages\MyMathPackage.zip""
        Tabulates both packages and aggregates the results into one set
        of reports.

    ""D:\Packages\MyElaPackage""
        Tabulates the package represented by the ""MyElaPackage"" file
        folder.

    -bank https://itembank.org -at hs3kwBlt8sorK9qwp8Wr
        Tabulates the entire contents of the specified item bank using
        the corresponding access token.

    -ids ""myPullList.csv"" -bank https://itembank.org -at hs3kwBlt8sorK9qwp8Wr
        Tabulates the items identifed by ids in ""myPullList.csv"" and stored
        in the item bank. Includes stimuli, wordlists, and tutorials
        referenced by the items in the tabulation.

Arguments:
    -a               Aggregate the results of all tabulations into one set of
                     reports.
    -v-<opt>         Disable a particular validation option (see below)
    -v+<opt>         Enable a particular validation option
    -h               Display this help text
    -bk <id>         The bankkey to use when one is not specfied in an id
                     file (see the -ids argument). If not included, the
                     default bankkey is 200.
    -ids <filename>  Optional name of a file containing item IDs to be tabulated
                     in the package that follows. If this file is not specfied
                     then all items in the package will be tabulated. This
                     argument may be repeated. (See below for details.)
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

    Item Bank: An item bank moniker consists of four parts of which two are
    optional.
    -bank <url>           (Optional) The URL of the GitLab item bank from which
                          the items will be drawn. If not specified, defaults
                          to ""https://itembank.smarterbalanced.org"".
    -ns <namespace>       (Optional) The GitLab namespace to which the items
                          belong. This should be a username or a group name.
                          If not specified, defaults to ""itemreviewapp"".
    -at <token>           (Required) A GitLab access token valid on the item
                          bank. See below on how to generate the token.
    -o <path>             (Required) The prefix to the output (report) files.
                          Unlike .zip and Folder packages, the output
                          path cannot be derived from the package moniker.

    You may specify multiple packages of any type and in any mix of types.
    Each package may have an an associated Item IDs file (see the -ids
    argument). If included, the IDs file must precede the moniker of the
    package.

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
    
    For .zip and Folder packages, the prefix is the name of the .zip file or
    the folder. For example ""MyPackage_ItemReport.csv"".

    For an item bank, the prefix is specified by the -o argument. For example,
    ""ItemBank_ItemReport.csv"".
    
    For aggregate reports, the prefix is the folder of the first package
    in the list plus the word ""Aggregate"". For example,
    ""Aggregate_ItemReport.csv"".

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
            length to item stim length falls outside of two standard deviations
            from mean (adjustible through app.config).
    -v+cdt  Disables CData validations including glossary tags and restricted
            css
    -v-tss  Check for text-to-speech silencing tags.

    -v+all  Enable all optional validation and tabulation features.
    -v+ebt  Embedded Braille Text: Enables checking embedded <brailleText>
            elements. Without this option, only braille embossing attachments
            are checked.
    -v+tgs  Target Grade Suffix: Enables checking whether the grade suffix in
            a target matches the grade alignment in the item attributes.
    -v+umf  Unreferenced Media File: Enables checking whether media files are
            referenced in the corresponding item, passage, or wordlist.
    -v+gtr  Glossary Text Report: Include glossary text in the Glossary
            Report output file.
    -v+uwt  Unreferenced Wordlist Terms: Reports an error when a wordlist
            term is not referenced by the corresponding item.
    -v+mwa  Missing Wordlist Attachments: Reports errors for missing wordlist
            attachments (audio and images) even when the corresponding term
            is not referenced by any item.
    -v+iat  Image Alternate Text: Reports errors when images are present
            without alternate text.

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

        const string cAggregatePrefix = "Aggregate";

        public static ValidationOptions gValidationOptions = new ValidationOptions();
        public static Logger Logger = LogManager.GetCurrentClassLogger();

        private static void Main(string[] args)
        {
            long startTicks = Environment.TickCount;

            // Default options
            gValidationOptions.Disable("ebt"); // Disable EmptyBrailleText test.
            gValidationOptions.Disable("tgs"); // Disable Target Grade Suffix
            gValidationOptions.Disable("umf"); // Disable checking for Unreferenced Media Files
            gValidationOptions.Disable("gtr"); // Disable Glossary Text Report
            gValidationOptions.Disable("uwt"); // Disable Unreferenced Wordlist Terms
            gValidationOptions.Disable("mwa"); // Disable checking for attachments on unreferenced wordlist terms
            gValidationOptions.Disable("iat"); // Disable checking for images without alternate text
            gValidationOptions.Disable("css"); // Disable reporting css color-contrast interference (temporary fix)

            LogManager.DisableLogging();

            try
            {
                var paths = new List<string>();
                bool aggregate = false;
                bool showHelp = false;
                foreach (var arg in args)
                {
                    if (arg[0] == '-') // Option
                    {
                        switch (char.ToLower(arg[1]))
                        {
                            case 'a':
                                aggregate = true;
                                break;
                            case 'h':
                                showHelp = true;
                                break;
                            case 'v':
                                if (arg[2] != '+' && arg[2] != '-')
                                {
                                    throw new ArgumentException("Invalid command-line option: " + arg);
                                }
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
                                break;
                            default:
                                throw new ArgumentException("Unexpected command-line option: " + arg);
                        }
                    }
                    else
                    {
                        string path = arg;
                        if (!ValidatePath(ref path))
                        {
                            throw new ArgumentException("No match for path: " + arg);
                        }
                        paths.Add(arg);
                    }
                }

                if (gValidationOptions.IsEnabled("asl"))
                {
                    SettingsUtility.RetrieveAslValues();
                }

                Console.WriteLine("Tabulator Flags");
                Console.WriteLine(Enumerable.Repeat("-",20).Aggregate((x,y) => $"{x}{y}"));

                gValidationOptions.Keys.ToList().ForEach(x =>
                {
                    Console.WriteLine($"[{x}: {gValidationOptions[x].ToString()}]");
                });

                Console.WriteLine(Enumerable.Repeat("-", 20).Aggregate((x, y) => $"{x}{y}"));
                Console.WriteLine();

                if (showHelp || paths.Count == 0)
                {
                    Console.WriteLine(cSyntax);
                }
                else if (aggregate)
                {
                    TabulateAggregate(paths);
                }
                else
                {
                    TabulateEach(paths);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Logger.Error(ex.Message);
            }

            var elapsedTicks = unchecked((uint)Environment.TickCount - (uint)startTicks);
            Console.WriteLine("Elapsed time: {0}.{1:d3} seconds", elapsedTicks / 1000, elapsedTicks % 1000);
            Logger.Info("Elapsed time: {0}.{1:d3} seconds", elapsedTicks / 1000, elapsedTicks % 1000);

            if (ConsoleHelper.IsSoleConsoleOwner)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Press any key to exit.");
                Console.ReadKey(true);
            }
        }

        static void TabulateEach(IReadOnlyList<string> paths)
        {
            foreach (var path in paths)
            {
                string directory = Path.GetDirectoryName(path);
                string pattern = Path.GetFileName(path);
                bool zip = path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                string[] packages;
                if (zip)
                {
                    packages = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
                }
                else
                {
                    packages = Directory.GetDirectories(directory, pattern, SearchOption.TopDirectoryOnly);
                }
                foreach (var package in packages)
                {
                    string reportPrefix = zip ? package.Substring(0, package.Length - 4) : package;

                    using (var tab = new Tabulator(reportPrefix))
                    {
                        tab.Tabulate(package);
                    }
                }
            }
        }

        static void TabulateAggregate(IReadOnlyList<string> paths)
        {
            string reportPrefix = Path.Combine(Path.GetDirectoryName(paths[0]), cAggregatePrefix);

            using (var tab = new Tabulator(reportPrefix))
            {
                foreach (var path in paths)
                {
                    string directory = Path.GetDirectoryName(path);
                    string pattern = Path.GetFileName(path);
                    string[] packages;
                    if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        packages = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
                    }
                    else
                    {
                        packages = Directory.GetDirectories(directory, pattern, SearchOption.TopDirectoryOnly);
                    }
                    foreach (var package in packages)
                    {
                        tab.Tabulate(package);
                    }
                }
            }
        }

        // Returns true if at least one file or directory matches the path
        // Updates the path to a full path
        static bool ValidatePath(ref string path)
        {
            string directory = Path.GetFullPath(Path.GetDirectoryName(path));
            string filename = Path.GetFileName(path);
            if (!Directory.Exists(directory))
            {
                return false;
            }
            if (Directory.GetFileSystemEntries(directory, filename, SearchOption.TopDirectoryOnly).Length <= 0)
            {
                return false;
            }

            path = Path.Combine(directory, filename);
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