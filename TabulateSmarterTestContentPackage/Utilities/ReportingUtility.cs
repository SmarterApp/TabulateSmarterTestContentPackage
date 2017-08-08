using System.IO;
using System.Text;
using ContentPackageTabulator.Models;

namespace ContentPackageTabulator.Utilities
{
    public static class ReportingUtility
    {
        private static readonly char[] cCsvEscapeChars = {',', '"', '\'', '\r', '\n'};
        public static int ErrorCount { get; set; }
        public static string ErrorReportPath { get; set; }
        public static TextWriter ErrorReport { get; set; }

        public static void ReportError(ItemContext it, ErrorCategory category, ErrorSeverity severity, string msg,
            string detail, params object[] args)
        {
            if (ErrorReport == null)
            {
                ErrorReport = new StreamWriter(File.Open(ErrorReportPath, FileMode.OpenOrCreate), Encoding.UTF8);
                ErrorReport.WriteLine("Folder,ItemId,ItemType,Category,Severity,ErrorMessage,Detail");
            }

            msg = string.IsNullOrEmpty(msg) ? string.Empty : CsvEncode(msg);

            detail = string.IsNullOrEmpty(detail) ? string.Empty : CsvEncode(string.Format(detail, args));

            // "Folder,ItemId,ItemType,Category,ErrorMessage"
            ErrorReport.WriteLine(string.Join(",", CsvEncode(it.Folder), CsvEncode(it.ItemId),
                CsvEncode(it.ItemType), category.ToString(), severity.ToString(), msg, detail));

            ++ErrorCount;
        }

        public static void ReportError(ItemContext it, ErrorCategory category, ErrorSeverity severity, string msg)
        {
            ReportError(it, category, severity, msg, null);
        }

        public static void ReportError(string validationOption, ItemContext it, ErrorCategory category,
            ErrorSeverity severity, string msg, string detail, params object[] args)
        {
            if (Program.gValidationOptions.IsEnabled(validationOption))
            {
                ReportError(it, category, severity, msg, detail, args);
            }
        }

        public static void ReportWitError(ItemContext it, ItemContext witIt, ErrorSeverity severity, string msg,
            string detail, params object[] args)
        {
            detail = string.Concat($"wordlistId='{witIt.ItemId}' ", detail);
            ReportError(it, ErrorCategory.Wordlist, severity, msg, detail, args);
        }

        public static string CsvEncode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            if (text.IndexOfAny(cCsvEscapeChars) < 0)
            {
                return text;
            }
            return string.Concat("\"", text.Replace("\"", "\"\""), "\"");
        }

        public static string CsvEncodeExcel(string text)
        {
            return string.Concat("\"", text.Replace("\"", "\"\""), "\t\"");
        }
    }
}