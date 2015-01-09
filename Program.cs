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
@"Syntax: TabulateSmarterTestContentPackage <path to package directory>

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
                if (args.Length == 0)
                {
                    Console.WriteLine(cSyntax);                
                }
                else
                {
                    new Tabulator(args[0]).Tabulate();
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
