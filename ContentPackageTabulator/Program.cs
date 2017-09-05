using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContentPackageTabulator.Models;
using ContentPackageTabulator.Utilities;
using Newtonsoft.Json;

namespace ContentPackageTabulator
{
    internal class Program
    {
// 78 character margin                                                       |
        private const string cSyntax =
            @"Syntax: TabulateSmarterTestContentPackage [options] <path>

Options:
    -s       Individually tabulate each package in Subdirectories of the
             specified directory.
    -a       Tabulate all packages in subdirectories and aggregate the
             results into one set of reports.
    -v-<opt> Disable a particular validation option (see below)
    -v+<opt> Enable a particular validation option
    -j       Output a validation.json file
    -h       Display this help text

Packages are typically delivered in .zip format. The tabulator can operate on
the package in its .zip form or unpacked into a directory tree. In either
case, the package is recognized by the presence of 'imsmanifest.xml' in the
root folder of the package.

When tabulating a single package, the path should be to the .zip file or to
the root folder of an unpacked package. When tabulating multiple packages
(using the -s or -a option) the path should be to the folder containing
the packages (in .zip or unpacked form). When tabulating multiple packages
the packages are recognized by the presence of imsmanifest.xml.

Reports are a set of .csv files and one .txt file that are placed in the same
directory as the .zip or package folder. Names are prefixed with the name of
the package. For example, 'MyContentPackage_ItemReport.csv'.

Validation options disable or enable the reporting of certain errors. Only a
subset of errors can be controlled this way.

Validation Options:
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
    -v-cdt  Disables CData validations including glossary tags and restricted css
    -v-tss  Check for text-to-speech silencing tags.
    -v-dsk  Do not write files to disk

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

        public static ValidationOptions gValidationOptions = new ValidationOptions();

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

            try
            {
                string rootPath = null;
                var operation = 'o'; // o=one, s=packages in Subdirectories, a=aggregate packages in subdirectories
                foreach (var arg in args)
                {
                    if (arg[0] == '-') // Option
                    {
                        switch (char.ToLower(arg[1]))
                        {
                            case 's':
                                operation = 's'; // All subdirectories
                                break;
                            case 'a':
                                operation = 'a'; // Aggregate
                                break;
                            case 'j':
                                operation = 'j'; // Output a validation.json file
                                break;
                            case 'h':
                                operation = 'h'; // Help
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
                    else if (rootPath == null)
                    {
                        rootPath = arg;
                    }
                    else
                    {
                        throw new ArgumentException("Unexpected command-line parameter: " + arg);
                    }
                }

                if (rootPath == null)
                {
                    operation = 'h';
                }

                Console.WriteLine("Tabulator Flags");
                Console.WriteLine(Enumerable.Repeat("-", 20).Aggregate((x, y) => $"{x}{y}"));

                gValidationOptions.Keys.ToList()
                    .ForEach(x => { Console.WriteLine($"[{x}: {gValidationOptions[x].ToString()}]"); });

                Console.WriteLine(Enumerable.Repeat("-", 20).Aggregate((x, y) => $"{x}{y}"));
                Console.WriteLine();

                var tab = new Tabulator();
                switch (operation)
                {
                    default:
                        tab.TabulateOne(rootPath);
                        break;

                    case 's':
                        tab.TabulateEach(rootPath);
                        break;

                    case 'a':
                        tab.TabulateAggregate(rootPath);
                        break;

                    case 'j':
						var result = new Tabulator().TabulateErrors(rootPath);
						var json = result.Select(x => new TabulationErrorDto
						{
							Category = x.Category.ToString(),
							Detail = x.Detail,
							Message = x.Message,
							Severity = x.Severity.ToString()
						}).OrderBy(x => x.Severity, new CustomStringComparer(StringComparer.OrdinalIgnoreCase))
									 .ThenBy(x => x.Category)
									 .ThenBy(x => x.Message)
									 .ThenBy(x => x.Detail);
						using (StreamWriter file = File.CreateText(@"validation.json"))
						{
							JsonSerializer serializer = new JsonSerializer();
							serializer.Serialize(file, json);
						}
                        break;

                    case 'h':
                        Console.WriteLine(cSyntax);
                        break;
                }
                if (gValidationOptions.IsEnabled("dsk"))
                {
                    ReportingUtility.ReportErrors();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            var elapsedTicks = Environment.TickCount - startTicks;
            Console.WriteLine("Info: Elapsed time: {0}.{1:d3} seconds", elapsedTicks / 1000, elapsedTicks % 1000);
        }
    }

	class CustomStringComparer : IComparer<string>
	{
		private readonly IComparer<string> _baseComparer;
		public CustomStringComparer(IComparer<string> baseComparer)
		{
			_baseComparer = baseComparer;
		}

		public int Compare(string x, string y)
		{
			if (_baseComparer.Compare(x, y) == 0)
				return 0;

			// "severe" comes before everything else
			if (_baseComparer.Compare(x, "Severe") == 0)
				return -1;
			if (_baseComparer.Compare(y, "Severe") == 0)
				return 1;

			// "degraded" comes next
			if (_baseComparer.Compare(x, "Degraded") == 0)
				return -1;
			if (_baseComparer.Compare(y, "Degraded") == 0)
				return 1;

			// "tolerable" comes next
			if (_baseComparer.Compare(x, "Tolerable") == 0)
				return -1;
			if (_baseComparer.Compare(y, "Tolerable") == 0)
				return 1;

			// "benign" comes last
			if (_baseComparer.Compare(x, "Benign") == 0)
				return -1;
			if (_baseComparer.Compare(y, "Benign") == 0)
				return 1;

			return _baseComparer.Compare(x, y);
		}
	}
}