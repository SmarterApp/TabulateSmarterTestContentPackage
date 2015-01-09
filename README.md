# TabulateSmarterTestContentPackage
Tabulates the contents of a test content package in SmarterApp format and checks for certain errors.

This is a command-line utility for extracting data about test packages in SmaterApp format. The current version generates three files and a possible error report and places them in the root directory of the content package.

The program is written in C# and can be built with the free edition of Microsoft Visual Studio Express 2013 for Windows Desktop. Professional versions of Visual Studio should also build the application.

To use:

1. Unpack the content package from the .zip file distribution into a directory tree. This should be the content package in IMS Content Packaging format. You can tell that's the right file and format because it will have an imsmanifest.xml file in the root directory. Note that the content package .zip file is often nested in another zip file that contains test package xml's, a readme, and a summary spreadsheet.
2. From a DOS command line, type: TabulateSmarterTestContentPackage <path to content directory><br/>Using the actual path to the root directory of the content package. This is the directory that contains imsmanifest.xml.
3. The program will run and then print summary information. Three files will be placed in the same directory as imsmanifest.xml.
  ** SummaryReport.txt - Contains the same counts as were displayed output to the console.
  ** TextGlossaryReport.csv - Contains a tabular extract of all text glossary terms.
  ** AudioGlossaryReport.csv - Contains a tabular extract of all audio glossary terms.
  ** ErrorReport.txt - Only exists if errors were detected. Contains details of the errors.
