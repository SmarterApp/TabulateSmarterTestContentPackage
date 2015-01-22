using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Win32Interop;

namespace TabulateSmarterTestContentPackage
{
    class Program
    {
// 78 character margin                                                       |
        static readonly string cSyntax =
@"Syntax: TabulateSmarterTestContentPackage [options] <path>

Options:
    -s       Individually tabulate each package in Subdirectories of the
             specified directory.
    -a       Tabulate all packages in subdirectories and aggregate the
             results into one set of reports.
    -v-<opt> Disable a particular validation option (see below)
    -v+<opt> Enable a partcular validation option
    -h       Display this help text

Packages are typically delivered in .zip format. The .zip file must be
unpacked into a directory tree. The path of the root of the directory tree
should be specified. This may be identified by the presence of the
imsmanifest.zip file in the root directory.

Reports are a set of .csv files and one .txt file that are placed in the same
directory as the imsmanifest.xml file. When aggregating the results (with the
-a option) then the reports are placed in the parent directory of all of the
packages (the one specified on the command line).

Validation options disable or enable the reportiong of certain errors. Only a
subset of errors can be controlled this way.

Validation Options:
    -v-pmd  Passage Manifest Dependency: An item that depends on a passage
            should have that dependency represented in the manifest. This
            option disables checking for that dependency. 
    -v+ebt  Empty Braille Text: Enables checking for empty <brailleText>
            elements in items.
";
// 78 character margin                                                       |

        public static ValidationOptions gValidationOptions = new ValidationOptions();

        static void Main(string[] args)
        {
            try
            {
                // Default options
                gValidationOptions.Disable("ebt");  // Disable EmptyBrailleText test.

                string rootPath = null;
                char operation = 'o'; // o=one, s=packages in Subdirectories, a=aggregate packages in subdirectories
                foreach (string arg in args)
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
                            case 'h':
                                operation = 'h'; // Help
                                break;
                            case 'v':
                                if (arg[2] != '+' && arg[2] != '-') throw new ArgumentException("Invalid command-line option: " + arg);
                                gValidationOptions[arg.Substring(3).ToLower()] = (arg[2] == '+');
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
                    Console.Error.WriteLine(cSyntax);
                    return;
                }

                Tabulator tab = new Tabulator(rootPath);
                switch (operation)
                {
                    default:
                        tab.TabulateOne();
                        break;

                    case 's':
                        tab.TabulateEach();
                        break;

                    case 'a':
                        tab.TabulateAggregate();
                        break;

                    case 'h':
                        Console.WriteLine(cSyntax);
                        break;
                }

            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
            }

            if (ConsoleHelper.IsSoleConsoleOwner)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Press any key to exit.");
                Console.ReadKey(true);
            }
        }
    }

    class ValidationOptions : Dictionary<string, bool>
    {
        public void Enable(string option)
        {
            this[option] = true;
        }

        public void Disable(string option)
        {
            this[option] = false;
        }

        public bool IsEnabled(string option)
        {
            bool value;
            if (!TryGetValue(option, out value)) return true;   // Options default to enabled
            return value;
        }

    }
}
