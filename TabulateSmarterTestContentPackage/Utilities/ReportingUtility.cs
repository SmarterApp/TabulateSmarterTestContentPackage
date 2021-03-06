using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using TabulateSmarterTestContentPackage.Models;
using System.Security.Cryptography;

namespace TabulateSmarterTestContentPackage.Utilities
{
    public static class ReportingUtility
    {
        const int c_maxDetailLength = 180; // Per the CDS v16 specification
        const int c_witPrefix = 600000000;

        public static int ErrorCount { get; set; }
        public static string ErrorReportPath { get; set; }
        public static string CurrentPackageName { get; set; }
        public static bool DeDuplicate { get; set; }
        public static bool UseCdsFormat { get; set; }
        public static string AdminYear { get; set; }
        public static string Asmt { get; set; }

        const string c_toolId = "TAB";
        const string c_errType = "content_pkg";

        static string s_runDate;
        static TextWriter s_ErrorReport;
        static HashSet<HashValue> s_ErrorsReported = new HashSet<HashValue>();
        static string s_version;

        private static void InitErrorReport()
        {
            if (s_ErrorReport != null) return;
            s_ErrorReport = new StreamWriter(ErrorReportPath, false, Encoding.UTF8);
            s_runDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK");

            var application = System.Reflection.Assembly.GetExecutingAssembly();
            s_version = application.GetName().Version.ToString();

            if (AdminYear == null) AdminYear = string.Empty;
            if (Asmt == null) Asmt = string.Empty;

            if (!UseCdsFormat)
            {
                s_ErrorReport.WriteLine("Folder,BankKey,ItemId,ItemType,Category,Severity,ErrorMessage,Detail");
            }
            else
            {
                s_ErrorReport.WriteLine("admin_year,asmt,severity,item_id,item_version,error_message_id,error_message,detail,notes,review_area,error_category,error_key,tool_id,tool_version,run_date,error_type,tdf_key");
            }

            string detail = $"version='{s_version}' options='{Program.Options}' date='{s_runDate}'";

            // This goes recursive but that's OK.
            ReportError(null, ErrorId.TabulatorStart, detail);
        }

        // All other ReportError overloads concentrate down to here.
        public static void ReportError(ItemIdentifier ii, ErrorId errorId, string detail)
        {
            if (!Errors.IsEnabled(errorId))
            {
                return; // Suppress reporting this error.
            }

            if (s_ErrorReport == null)
            {
                InitErrorReport();
            }

            string folderName;
            string itemType;
            string bankKey;
            string itemId;
            string version;
            if (ii == null)
            {
                folderName = string.Empty;
                itemType = null;
                bankKey = null;
                itemId = null;
                version = null;
            }
            else if (object.ReferenceEquals(ii, Errors.ManifestItemId))
            {
                folderName = "imsmanifest.xml";
                itemType = null;
                bankKey = null;
                itemId = null;
                version = null;
            }
            else if (ii.ItemId > c_witPrefix)
            {
                folderName = ii.FolderName;
                itemType = ii.ItemType;
                bankKey = ii.BankKey.ToString();
                itemId = (ii.ItemId - c_witPrefix).ToString();
                version = ii.Version;
                detail = string.Concat($"wordlistId='{ii.ItemId}' ", detail);
            }
            else
            {
                folderName = ii.FolderName;
                itemType = ii.ItemType;
                bankKey = ii.BankKey.ToString();
                itemId = ii.ItemId.ToString();
                version = ii.Version;
            }

            if (CurrentPackageName != null)
            {
                folderName = string.Concat(CurrentPackageName, "/", folderName);
            }

            // If deduplicate, find out if this error has already been reported for this particular item.
            if (DeDuplicate)
            {
                // Create a hash of the itemId and message
                var errHash = HashValue.ComputeHash<SHA1CryptoServiceProvider>(string.Concat(itemType, bankKey, itemId, errorId));

                // If it's aready in the set then exit
                if (!s_ErrorsReported.Add(errHash))
                {
                    return; // Already reported an error of this type on this item
                }
            }

            var errorInfo = Errors.ErrorTable[(int)errorId];

            if (errorInfo.Severity > ErrorSeverity.Message)
            {
                ++ErrorCount;
            }

            if (!UseCdsFormat)
            {
                // "Folder,ItemType,BankKey,ItemId,Category,Severity,ErrorMessage,Detail"
                s_ErrorReport.WriteLine(string.Join(",", Tabulator.CsvEncode(folderName),
                    Tabulator.CsvEncode(bankKey), Tabulator.CsvEncode(itemId), Tabulator.CsvEncode(itemType),
                    errorInfo.Category, errorInfo.Severity, Tabulator.CsvEncode(errorInfo.Message), Tabulator.CsvEncode(detail)));
            }
            else
            {
                // Limit detail length
                if (detail.Length > c_maxDetailLength)
                {
                    detail = detail.Substring(0, c_maxDetailLength - 3) + "...";
                }

                string msgId = Errors.ErrorIdToString(errorInfo.Id);
                string errKey = GenerateErrorKey(itemId, msgId, detail);
                // "admin_year,asmt,severity,item_id,item_version,error_message_id,error_message,
                // detail,notes,review_area,error_category,error_key,tool_id,tool_version,run_date,
                // error_type,tdf_key"
                s_ErrorReport.WriteLine(Tabulator.CsvEncode(AdminYear, Asmt,
                    errorInfo.Severity.ToString().ToLowerInvariant(),
                    itemId, version, msgId, errorInfo.Message, detail, string.Empty,
                    errorInfo.ReviewArea.ToString().ToLowerInvariant(),
                    errorInfo.Category.ToString().ToLowerInvariant(),
                    errKey, c_toolId, s_version, s_runDate, c_errType, string.Empty));
            }
        }

        public static void ReportError(ItemIdentifier ii, ErrorId errorId)
        {
            ReportError(ii, errorId, string.Empty);
        }

        public static void ReportError(ItemIdentifier ii, ErrorId errorId, string detail, params object[] args)
        {
            ReportError(ii, errorId, string.Format(detail, args));
        }

        public static void ReportError(ItemIdentifier ii, Exception err)
        {
            ReportError(ii, ErrorId.Exception, $"{err.GetType().FullName}: {err.Message}");
        }

        public static void ReportWitError(ItemIdentifier ii, ItemIdentifier witIt, ErrorId errorId, string detail)
        {
            detail = string.Concat($"wordlistId='{witIt.ItemId}' ", detail);
            ReportError(ii, errorId, detail);
        }

        public static void ReportWitError(ItemIdentifier ii, ItemIdentifier witIt, ErrorId errorId)
        {
            ReportWitError(ii, witIt, errorId, string.Empty);
        }

        public static void ReportWitError(ItemIdentifier ii, ItemIdentifier witIt, ErrorId errorId, string detail, params object[] args)
        {
            ReportWitError(ii, witIt, errorId, string.Format(detail, args));
        }

        public static void CloseReport()
        {
            if (s_ErrorReport != null)
            {
                s_ErrorReport.Dispose();
                s_ErrorReport = null;
            }
            s_ErrorsReported.Clear();
        }

        private static string GenerateErrorKey(string itemId, string errorMessageId, string detail)
        {
            var hash = HashValue.ComputeHash<MD5CryptoServiceProvider>(string.Concat(itemId, errorMessageId, detail));
            return hash.ToString();
        }

        public static void ExportErrorTable(string filepath)
        {
            string path = Path.GetFullPath(filepath);
            Console.WriteLine($"Exporting error table to: {path}");
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.WriteLine("error_message_id,error_message,error_category,severity,review_area");
                foreach (var errInfo in Errors.ErrorTable)
                {
                    if (errInfo.Id != ErrorId.None)
                    {
                        writer.WriteLine(Tabulator.CsvEncode(Errors.ErrorIdToString(errInfo.Id), errInfo.Message,
                            errInfo.Category.ToString().ToLowerInvariant(),
                            errInfo.Severity.ToString().ToLowerInvariant(),
                            errInfo.ReviewArea.ToString().ToLowerInvariant()));
                    }
                }
            }
        }
    }
}