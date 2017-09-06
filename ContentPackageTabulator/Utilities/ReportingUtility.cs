using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ContentPackageTabulator.Models;
using Newtonsoft.Json;

namespace ContentPackageTabulator.Utilities
{
    public static class ReportingUtility
    {
        private static readonly char[] cCsvEscapeChars = {',', '"', '\'', '\r', '\n'};
        public static int ErrorCount { get; set; }
        public static string ErrorReportPath { get; set; }
        public static string ValidationReportPath { get; set; }
        public static TextWriter ErrorReport { get; set; }
        public static IList<TabulationError> Errors { get; set; } = new List<TabulationError>();

        public static void ReportErrors()
        {
            Errors.ToList().ForEach(ReportError);
        }

        public static void ReportError(TabulationError tabulationError)
        {
            if (ErrorReport == null)
            {
                ErrorReport = new StreamWriter(File.Open(ErrorReportPath, FileMode.OpenOrCreate), Encoding.UTF8);
                ErrorReport.WriteLine("Folder,ItemId,ItemType,Category,Severity,ErrorMessage,Detail");
            }

            var msg = string.IsNullOrEmpty(tabulationError.Message) ? string.Empty : CsvEncode(tabulationError.Message);

            var detail = string.IsNullOrEmpty(tabulationError.Detail) ? string.Empty : CsvEncode(tabulationError.Detail);

            // "Folder,ItemId,ItemType,Category,ErrorMessage"
            ErrorReport.WriteLine(string.Join(",", CsvEncode(tabulationError.Context.Folder), CsvEncode(tabulationError.Context.ItemId),
                CsvEncode(tabulationError.Context.ItemType), tabulationError.Category.ToString(), tabulationError.Severity.ToString(), msg, detail));

            ++ErrorCount;
        }

        public static void WriteValidationJson() {
			var json = Errors.Select(x => new TabulationErrorDto
			{
				Category = x.Category.ToString(),
				Detail = x.Detail,
				Message = x.Message,
				Severity = x.Severity.ToString()
			}).OrderBy(x => x.Severity, new ErrorSeverityComparer(StringComparer.OrdinalIgnoreCase))
									 .ThenBy(x => x.Category)
									 .ThenBy(x => x.Message)
									 .ThenBy(x => x.Detail);
            if(File.Exists(ValidationReportPath)) {
                File.Delete(ValidationReportPath);
            }
			using (StreamWriter file = File.CreateText(ValidationReportPath))
			{
				JsonSerializer serializer = new JsonSerializer();
				serializer.Serialize(file, json);
			}
        }

        public static void ReportError(ItemContext it, ErrorCategory category, ErrorSeverity severity, string msg,
            string detail, params object[] args)
        {
            if (!string.IsNullOrEmpty(detail))
            {
                detail = string.Format(detail, args);
            }
            Errors.Add(new TabulationError
            {
                Category = category,
                Context = it,
                Detail = detail,
                Message = msg,
                Severity = severity
            });
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