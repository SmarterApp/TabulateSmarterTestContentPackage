using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;

namespace TabulateSmarterTestContentPackage
{
    class Tabulator
    {
        static readonly UTF8Encoding sUtf8NoBomEncoding = new UTF8Encoding(false, true);

        string mRootPath;
        int mErrorCount = 0;
        int mItemCount = 0;
        int mWordlistCount = 0;
        int mGlossaryTermCount = 0;
        int mGlossaryM4aCount = 0;
        int mGlossaryOggCount = 0;
        Dictionary<string, int> mTypeCounts = new Dictionary<string,int>();
        Dictionary<string, int> mTermCounts = new Dictionary<string, int>();
        Dictionary<string, int> mTranslationCounts = new Dictionary<string, int>();
        Dictionary<string, int> mM4aTranslationCounts = new Dictionary<string, int>();
        Dictionary<string, int> mOggTranslationCounts = new Dictionary<string, int>();

        TextWriter mTextGlossaryReport;
        TextWriter mAudioGlossaryReport;
        TextWriter mErrorReport;

        public Tabulator(string rootPath)
        {
            mRootPath = rootPath;
        }

        public void Tabulate()
        {
            if (!File.Exists(Path.Combine(mRootPath, "imsmanifest.xml"))) throw new ArgumentException("Not a valid content package path. File imsmanifest.xml not found!");
            Console.WriteLine("Tabulating " + mRootPath);

            try
            {
                mTextGlossaryReport = new StreamWriter(Path.Combine(mRootPath, "TextGlossaryReport.csv"), false, sUtf8NoBomEncoding);
                mTextGlossaryReport.WriteLine("WIT_ID,Index,Term,Language,Length");
                mAudioGlossaryReport = new StreamWriter(Path.Combine(mRootPath, "AudioGlossaryReport.csv"));
                mAudioGlossaryReport.WriteLine("WIT_ID,Index,Term,Language,Encoding,Size");

                DirectoryInfo diItems = new DirectoryInfo(Path.Combine(mRootPath, "Items"));
                foreach (DirectoryInfo diItem in diItems.EnumerateDirectories())
                {
                    try
                    {
                        TabulateItem(diItem);
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine();
                        Console.WriteLine(err.ToString());
                        Console.WriteLine();
                        ++mErrorCount;
                    }
                }

                // Report aggregate results to the console
                Console.WriteLine();
                if (mErrorCount != 0) Console.WriteLine("Errors: {0}", mErrorCount);
                Console.WriteLine("Items: {0}", mItemCount);
                Console.WriteLine("Word Lists: {0}", mWordlistCount);
                Console.WriteLine("Glossary Terms: {0}", mGlossaryTermCount);
                Console.WriteLine("Unique Glossary Terms: {0}", mTermCounts.Count);
                Console.WriteLine("Glossary m4a Audio: {0}", mGlossaryM4aCount);
                Console.WriteLine("Glossary ogg Audio: {0}", mGlossaryOggCount);
                Console.WriteLine();
                Console.WriteLine("Item Type Counts:");
                mTypeCounts.Dump();
                Console.WriteLine();
                Console.WriteLine("Translation Counts:");
                mTranslationCounts.Dump();
                Console.WriteLine();
                Console.WriteLine("M4a Translation Counts:");
                mM4aTranslationCounts.Dump();
                Console.WriteLine();
                Console.WriteLine("Ogg Translation Counts:");
                mOggTranslationCounts.Dump();
                Console.WriteLine();

                /*
                Console.WriteLine("Glossary Term Counts:");
                mTermCounts.Dump();
                Console.WriteLine();
                */
            }
            finally
            {
                if (mAudioGlossaryReport != null)
                {
                    mAudioGlossaryReport.Dispose();
                    mAudioGlossaryReport = null;
                }
                if (mTextGlossaryReport != null)
                {
                    mTextGlossaryReport.Dispose();
                    mTextGlossaryReport = null;
                }
                if (mErrorReport != null)
                {
                    mErrorReport.Dispose();
                    mErrorReport = null;
                }
            }
        }

        private void TabulateItem(DirectoryInfo diItem)
        {
            // Read the item XML
            XmlDocument xml = new XmlDocument();
            xml.Load(Path.Combine(diItem.FullName, diItem.Name + ".xml"));
            string itemType = xml.XpEval("itemrelease/item/@format");
            if (itemType == null) itemType = xml.XpEval("itemrelease/item/@type");
            if (itemType == null) throw new InvalidDataException("Item type not found");
            string itemId = xml.XpEval("itemrelease/item/@id");
            if (itemId == null) throw new InvalidDataException("Item id not found");

            // Add to the item count and the type count
            ++mItemCount;
            mTypeCounts.Increment(itemType);

            if (string.Equals(itemType, "wordList", StringComparison.Ordinal))
            {
                TabulateWordList(diItem, xml, itemId);
            }
        }

        static readonly Regex sRxParseAudiofile = new Regex(@"Item_(\d+)_v(\d)_(\d+)_(\d+)([a-zA-Z]+)_glossary_", RegexOptions.Compiled|RegexOptions.CultureInvariant);

        private void TabulateWordList(DirectoryInfo diItem, XmlDocument xml, string itemId)
        {
            List<string> terms = new List<string>();
            ++mWordlistCount;
            foreach (XmlNode kwNode in xml.SelectNodes("itemrelease/item/keywordList/keyword"))
            {
                ++mGlossaryTermCount;
                string term = kwNode.XpEval("@text");
                int index = int.Parse(kwNode.XpEval("@index"));
                mTermCounts.Increment(term);

                while (terms.Count < index + 1) terms.Add(string.Empty);
                terms[index] = term;

                foreach(XmlNode htmlNode in kwNode.SelectNodes("html"))
                {
                    string language = htmlNode.XpEval("@listType");
                    mTranslationCounts.Increment(language);

                    // WIT_ID,Index,Term,Language,Length
                    mTextGlossaryReport.WriteLine("{0},{1},{2},{3},{4}", CsvEncode(itemId), index, CsvEncode(term), CsvEncode(language), htmlNode.InnerXml.Length);
                }
            }

            // Tablulate m4a audio translations
            foreach (FileInfo fi in diItem.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                // If Audio file
                string extension = fi.Extension.Substring(1).ToLower();
                if (string.Equals(extension, "m4a", StringComparison.Ordinal) || string.Equals(extension, "ogg", StringComparison.Ordinal))
                {
                    Match match = sRxParseAudiofile.Match(fi.Name);
                    if (match.Success)
                    {
                        string language = match.Groups[5].Value;
                        int index = int.Parse(match.Groups[4].Value);

                        if (index == 0 || index >= terms.Count)
                        {
                            ReportError(diItem, "Audio file {0} has index {1} with no matching term.", fi.Name, index);
                            continue;
                        }

                        string term = terms[index];

                        if (string.Equals(extension, "m4a", StringComparison.Ordinal))
                        {
                            mM4aTranslationCounts.Increment(language);
                        }
                        else
                        {
                            mOggTranslationCounts.Increment(language);
                        }

                        // WIT_ID,Index,Term,Language,Encoding,Size
                        mAudioGlossaryReport.WriteLine("{0},{1},{2},{3},{4},{5}", CsvEncode(itemId), index, CsvEncode(term), CsvEncode(language), CsvEncode(extension), fi.Length);
                    }
                }
            }
        }

        void ReportError(string msg)
        {
            if (mErrorReport == null)
            {
                mErrorReport = new StreamWriter(Path.Combine(mRootPath, "ErrorReport.txt"), false, sUtf8NoBomEncoding);
                mErrorReport.Write(msg);
                if (msg[msg.Length - 1] != '\n') mErrorReport.WriteLine();
                mErrorReport.WriteLine();
            }
        }

        void ReportError(DirectoryInfo diItem, string msg)
        {
            ReportError(string.Concat(diItem.Name, "\r\n", msg));

        }

        void ReportError(DirectoryInfo diItem, string msg, params object[] args)
        {
            ReportError(diItem, string.Format(msg, args));
        }

        private static readonly char[] cCsvEscapeChars = {',', '"', '\'', '\r', '\n'};

        static string CsvEncode(string text)
        {
            if (text.IndexOfAny(cCsvEscapeChars) < 0) return text;
            return string.Concat("\"", text.Replace("\"", "\"\""), "\"");
        }
    }

    static class TabulatorHelp
    {
        public static string XpEval(this XmlNode doc, string xpath)
        {
            XmlNode node = doc.SelectSingleNode(xpath);
            if (node == null) return null;
            return node.InnerText;
        }

        public static void Increment(this Dictionary<string, int> dict, string key)
        {
            int count;
            if (!dict.TryGetValue(key, out count)) count = 0;
            dict[key] = count + 1;
        }

        public static void Dump(this Dictionary<string, int> dict)
        {
            foreach (var pair in dict)
            {
                Console.WriteLine("  {0}: {1}", pair.Key, pair.Value);
            }
        }
    }
}
