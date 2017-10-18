using System.IO;
using System.Text;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Utilities
{
    public static class ReportingUtility
    {
        public static int ErrorCount { get; set; }
        public static string ErrorReportPath { get; set; }
        public static string CurrentPackageName { get; set; }

        static TextWriter m_ErrorReport { get; set; }

        private static void InternalReportError(string folder, string itemType, string bankKey, string itemId, ErrorCategory category, ErrorSeverity severity, string msg, string detail)
        {
            if (m_ErrorReport == null)
            {
                m_ErrorReport = new StreamWriter(ErrorReportPath, false, Encoding.UTF8);
                m_ErrorReport.WriteLine("Folder,BankKey,ItemId,ItemType,Category,Severity,ErrorMessage,Detail");
            }

            if (CurrentPackageName != null)
            {
                folder = string.Concat(CurrentPackageName, "/", folder);
            }

            // "Folder,ItemType,BankKey,ItemId,Category,Severity,ErrorMessage,Detail"
            m_ErrorReport.WriteLine(string.Join(",", Tabulator.CsvEncode(folder),
                Tabulator.CsvEncode(bankKey), Tabulator.CsvEncode(itemId), Tabulator.CsvEncode(itemType),
                category.ToString(), severity.ToString(), Tabulator.CsvEncode(msg), Tabulator.CsvEncode(detail)));

            ++ErrorCount;
        }

        public static void ReportError(ItemIdentifier ii, ErrorCategory category, ErrorSeverity severity, string msg, string detail = null)
        {
            InternalReportError(ii.FolderName, ii.ItemType, ii.BankKey.ToString(), ii.ItemId.ToString(), category, severity, msg, detail);
        }

        public static void ReportError(string folder, ErrorCategory category, ErrorSeverity severity, string msg, string detail = null)
        {
            InternalReportError(folder, null, null, null, category, severity, msg, detail);
        }

        public static void ReportError(FileFolder folder, ErrorCategory category, ErrorSeverity severity, string msg, string detail = null)
        {
            string folderName = folder.RootedName;
            if (!string.IsNullOrEmpty(folderName) && folderName[0] == '/')
            {
                folderName = folderName.Substring(1);
            }
            InternalReportError(folderName, null, null, null, category, severity, msg, detail);
        }

        public static void ReportError(ItemIdentifier ii, ErrorCategory category, ErrorSeverity severity, string msg,
            string detail, params object[] args)
        {
            ReportError(ii, category, severity, msg, string.Format(System.Globalization.CultureInfo.InvariantCulture, detail, args));
        }

        public static void ReportError(string validationOption, ItemIdentifier ii, ErrorCategory category,
            ErrorSeverity severity, string msg, string detail, params object[] args)
        {
            if (Program.gValidationOptions.IsEnabled(validationOption))
            {
                ReportError(ii, category, severity, msg, detail, args);
            }
        }

        public static void ReportError(string validationOption, ItemIdentifier ii, ErrorCategory category,
            ErrorSeverity severity, string msg, string detail = null)
        {
            if (Program.gValidationOptions.IsEnabled(validationOption))
            {
                ReportError(ii, category, severity, msg, detail);
            }
        }

        public static void ReportWitError(ItemIdentifier ii, ItemIdentifier witIt, ErrorSeverity severity, string msg,
            string detail = null)
        {
            detail = string.Concat($"wordlistId='{witIt.ItemId}' ", detail);
            ReportError(ii, ErrorCategory.Wordlist, severity, msg, detail);
        }

        public static void ReportWitError(ItemIdentifier ii, ItemIdentifier witIt, ErrorSeverity severity, string msg,
            string detail, params object[] args)
        {
            ReportWitError(ii, witIt, severity, msg, string.Format(System.Globalization.CultureInfo.InvariantCulture, detail, args));
        }

        public static void CloseReport()
        {
            if (m_ErrorReport != null)
            {
                m_ErrorReport.Dispose();
                m_ErrorReport = null;
            }
        }
    }
}