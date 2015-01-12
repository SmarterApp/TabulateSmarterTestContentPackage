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
        static readonly string cSyntax =
@"Syntax: TabulateSmarterTestContentPackage [options] <path to package directory>

Options:
    -s Individually tabulate each package in Subdirectories of the specified directory.
    -a Tabulate all packages in subdirectories and aggregate the results.

Packages are typically delivered in .zip format. The .zip file must be unpacked
into a directory tree. The path of the root of the directory tree should be
specified. This may be identified by the presence of the imsmanifest.zip file
in the root directory.

Contents of the package are tabulated into a set of .csv files which are placed
in the root directory alongside the imsmanifest.zip file.";

        static void Main(string[] args)
        {
            try
            {
                string rootPath = null;
                char operation = 'o'; // o=one, s=packages in Subdirectories, a=aggregate packages in subdirectories
                foreach (string arg in args)
                {
                    if (arg[0] == '-') // Option
                    {
                        switch (arg.ToLower())
                        {
                            case "-s":
                                operation = 's';
                                break;
                            case "-a":
                                operation = 'a';
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
                }

            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
                Console.WriteLine();
                Console.WriteLine(err.ToString());
            }

            if (ConsoleHelper.IsSoleConsoleOwner)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Press any key to exit.");
                Console.ReadKey(true);
            }
        }
    }
}
