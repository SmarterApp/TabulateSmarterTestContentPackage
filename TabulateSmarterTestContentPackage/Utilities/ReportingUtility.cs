using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using TabulateSmarterTestContentPackage.Models;
using Newtonsoft.Json;
using System.Linq;

namespace TabulateSmarterTestContentPackage.Utilities
{
    public static class ReportingUtility
    {
        public static int ErrorCount { get; set; }
        public static string ErrorReportPath { get; set; }
        public static bool JsonValidation { get; set; }
        public static string JsonErrorReportPath { get; set; }
        public static string CurrentPackageName { get; set; }
        public static bool DeDuplicate { get; set; }

        static TextWriter m_ErrorReport { get; set; }
        static TextWriter m_JsonErrorReport { get; set; }
        static HashSet<ShaHash> s_ErrorsReported = new HashSet<ShaHash>();

        private static void InternalReportError(string folder, string itemType, string bankKey, string itemId, ErrorCategory category, ErrorSeverity severity, string msg, string detail)
        {
            var tabulationError = new TabulationErrorDto()
            {
                Folder = folder,
                BankKey = bankKey,
                ItemId = itemId,
                ItemType = itemType,
                Category = category.ToString(),
                Severity = severity.ToString(),
                Message = msg,
                Detail = detail
            };

            // If deduplicate, find out if this error has already been reported for this particular item.
            if (DeDuplicate)
            {
                // Create a hash of the itemId and message
                var errHash = new ShaHash(string.Concat(itemType, bankKey, itemId, msg));

                // If it's aready in the set then exit
                if (!s_ErrorsReported.Add(errHash))
                {
                    return; // Already reported an error of this type on this item
                }
            }

            if (m_ErrorReport == null)
            {
                m_ErrorReport = new StreamWriter(ErrorReportPath, false, Encoding.UTF8);
                m_ErrorReport.WriteLine("Folder,BankKey,ItemId,ItemType,Category,Severity,ErrorMessage,Detail");

                if (JsonValidation && m_JsonErrorReport == null)
                {
                    m_JsonErrorReport = new StreamWriter(JsonErrorReportPath, false, Encoding.UTF8);
                }
            }

            if (CurrentPackageName != null)
            {
                folder = string.Concat(CurrentPackageName, "/", folder);
            }

            //generate & write csv output of a single tabulation error
            m_ErrorReport.WriteLine(tabulationError.ToCsv());

            //generate & write json output of a single tabulation error
            if (JsonValidation)
            {
                m_JsonErrorReport.Write(tabulationError.ToJson() + ",");
            }

            ++ErrorCount;
        }

        private static string ToCsv(this TabulationErrorDto t)
        {
            // "Folder,ItemType,BankKey,ItemId,Category,Severity,ErrorMessage,Detail"
            return string.Join(",", Tabulator.CsvEncode(t.Folder),
                Tabulator.CsvEncode(t.BankKey), Tabulator.CsvEncode(t.ItemId), Tabulator.CsvEncode(t.ItemType),
                t.Category.ToString(), t.Severity.ToString(), Tabulator.CsvEncode(t.Message), Tabulator.CsvEncode(t.Detail));
        }

        private static string ToJson(this TabulationErrorDto t)
        {
            return JsonConvert.SerializeObject(t);
        }
        
        public static void ReportError(ItemIdentifier ii, ErrorCategory category, ErrorSeverity severity, string msg, string detail = null)
        {
            string folderName;
            string itemType;
            string bankKey;
            string itemId;
            if (ii != null)
            {
                folderName = ii.FolderName;
                itemType = ii.ItemType;
                bankKey = ii.BankKey.ToString();
                itemId = ii.ItemId.ToString();
            }
            else
            {
                folderName = string.Empty;
                itemType = null;
                bankKey = null;
                itemId = null;
            }
            InternalReportError(folderName, itemType, bankKey, itemId, category, severity, msg, detail);
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

        public static void ReportError(ItemIdentifier ii, ErrorSeverity severity, Exception err)
        {
            ReportError(ii, ErrorCategory.Exception, severity, err.GetType().Name, err.ToString());
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

        private static void FinalizeJsonValidationReport()
        {
            if (m_JsonErrorReport != null)
                m_JsonErrorReport.Close();

            var reader = new StreamReader(JsonErrorReportPath);

            //read json file contents (minus last comma)
            var tabulationErrors = JsonConvert.DeserializeObject<IList<TabulationErrorDto>>("[" + reader.ReadToEnd().TrimEnd(',') + "]");
            reader.Close();

            //sort json and write it back to the same file with overwrite enabled
            m_JsonErrorReport = new StreamWriter(JsonErrorReportPath, false);
            m_JsonErrorReport.Write(JsonConvert.SerializeObject(tabulationErrors.OrderBy(x => x.Severity, new ErrorSeverityComparer(StringComparer.OrdinalIgnoreCase))
                                                                                .ThenBy(x => x.Category)
                                                                                .ThenBy(x => x.Message)
                                                                                .ThenBy(x => x.Detail)));
            m_JsonErrorReport.Close();
        }

        public static void CloseReport()
        {
            if (m_ErrorReport != null)
            {
                m_ErrorReport.Dispose();
                m_ErrorReport = null;
            }

            if (m_JsonErrorReport != null)
            {
                FinalizeJsonValidationReport();
                m_JsonErrorReport.Dispose();
                m_JsonErrorReport = null;
            }
            s_ErrorsReported.Clear();
        }

        private class TabulationErrorDto
        {
            [JsonIgnore]
            public string Folder { get; set; }
            [JsonIgnore]
            public string BankKey { get; set; }
            [JsonIgnore]
            public string ItemId { get; set; }
            [JsonIgnore]
            public string ItemType { get; set; }
            [JsonProperty(PropertyName = "category")]
            public string Category { get; set; }
            [JsonProperty(PropertyName = "severity")]
            public string Severity { get; set; }
            [JsonProperty(PropertyName = "message")]
            public string Message { get; set; }
            [JsonProperty(PropertyName = "detail")]
            public string Detail { get; set; }
        }

    }
}