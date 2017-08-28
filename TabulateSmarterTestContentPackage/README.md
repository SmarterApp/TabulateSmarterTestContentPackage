# TabulateSmarterTestContentPackage
Tabulates the contents of a test content package in SmarterApp format and checks for certain errors.

This is a command-line utility for extracting data about test packages in SmaterApp format. The current version generates three files and a possible error report and places them in the root directory of the content package.

The program is written in C# and can be built with the free edition of Microsoft Visual Studio Express 2013 for Windows Desktop. Professional versions of Visual Studio should also build the application.

An executable file is included in each "release" package. There is no installer, the program is executed from the command line.

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;**Syntax:** TabulateSmarterTestContentPackage [options] <path>

For detailed syntax, including command-line options run the program with the -h (help) option as follows:

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;TabulateSmarterTestContentPackage -h

### For versions 2.x.x:

Run with the following command:

dotnet ContentPackageTabulator.dll "<path/to/content/root>" -v+all
