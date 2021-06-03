# GI_Types Branch
This is a special build that distinguishes between the three GI item type variants:

* GI-DD - Drag and Drop
* GI-HS - Hotspot
* GI-PL - Graphing points and lines

Certain items include more than one of these variants in the same item.

# TabulateSmarterTestContentPackage
Tabulates the contents of a test content package in SmarterApp format and checks for certain errors.

This is a command-line utility for extracting data about test packages in SmarterApp format. The current version generates four .csv reports, a summary .txt report and a  a possible error report.

The program is written in C# and can be built with the free edition of Microsoft Visual Studio Express 2013 for Windows Desktop. Professional versions of Visual Studio should also build the application.

An executable file is included in each "release" package. There is no installer, the program is executed from the command line:
    
    TabulateSmarterTestContentPackage [options] <path>

If running a "Universal Platform" distribution then you must install the appropriate .Net Core Runtime (see below). Then use the following command line:

    dotnet TabulateSmarterTestContentPackage [options] <path>

For detailed syntax, including command-line options run the program with the -h (help) option as follows:

    TabulateSmarterTestContentPackage -h

## Installing the .NET Core Runtime
If running the "Universal Platform, you must download and install the .NET Core Runtime (unless it is already installed on your computer).
* [.NET Core Runtime for Windows](https://www.microsoft.com/net/download/Windows/run)
* [.NET Core Runtime for Linux](https://www.microsoft.com/net/download/linux/run) (Be sure to select the right Linux distribution.)
* [.NET Core Runtime for MacOS](https://www.microsoft.com/net/download/macos/run)
