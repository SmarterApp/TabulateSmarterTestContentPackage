using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using TabulateSmarterTestContentPackage.Models;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace TabulateSmarterTestContentPackage
{
    /// <summary>
    /// The Smarter Balanced Item Authoring Tool calls the tabulator internally to validate
    /// items when they are updated and also on-demand. It requires the errors to be output
    /// in sorted order (by serverity) and in JSON format. This class performs that conversion.
    /// It is enabled by the -j command-line option.
    /// </summary>
    static class CsvToJson
    {
        const string c_JsonErrorReportFn = "ErrorReport.json";
        const string c_JsonErrorReportFn_SingleItem = "validation.json";

        // Reads all errors from CSV into a list, sorts the list, and writes out in JSON format.
        public static void ConvertErrorReport(string srcFilename, string dstFilename)
        {
            using (var reader = new CsvReader(srcFilename))
            {
                var tabulationErrors = new List<TabulationErrorDto>();

                if (File.Exists(srcFilename))
                {
                    int ixFolder = 0;
                    int ixBankKey = 0;
                    int ixItemId = 0;
                    int ixItemType = 0;
                    int ixCategory = 0;
                    int ixSeverity = 0;
                    int ixErrorMessage = 0;
                    int ixDetail = 0;

                    // Load the header row and set indexes
                    {
                        var headers = reader.Read();
                        int i = 0;
                        foreach (var header in headers)
                        {
                            switch (header)
                            {
                                case "Folder":
                                    ixFolder = i;
                                    break;
                                case "BankKey":
                                    ixBankKey = i;
                                    break;
                                case "ItemId":
                                    ixItemId = i;
                                    break;
                                case "ItemType":
                                    ixItemType = i;
                                    break;
                                case "Category":
                                    ixCategory = i;
                                    break;
                                case "Severity":
                                    ixSeverity = i;
                                    break;
                                case "ErrorMessage":
                                    ixErrorMessage = i;
                                    break;
                                case "Detail":
                                    ixDetail = i;
                                    break;
                            }
                            ++i;
                        }
                    }

                    // Read each error from the CSV file
                    for (; ; )
                    {
                        var row = reader.Read();
                        if (row == null) break;

                        tabulationErrors.Add(new TabulationErrorDto()
                        {
                            Folder = row[ixFolder],
                            BankKey = row[ixBankKey],
                            ItemId = row[ixItemId],
                            ItemType = row[ixItemType],
                            Category = row[ixCategory],
                            Severity = row[ixSeverity],
                            Message = row[ixErrorMessage],
                            Detail = row[ixDetail]
                        });
                    }
                }

                tabulationErrors.Sort();

                var serializer = new DataContractJsonSerializer(tabulationErrors.GetType(),
                    new Type[] { typeof(TabulationErrorDto) });

                using (var outStream = new FileStream(dstFilename, FileMode.Create, FileAccess.Write))
                {
                    serializer.WriteObject(outStream, tabulationErrors);
                }
            }
        }

        public static string GenerateErrorReportJsonFilename(string reportPrefix, TestPackage package)
        {
            // If single item package, put the json report in that folder
            var singleItemPackage = package as SingleItemPackage;
            if (singleItemPackage != null)
            {
                return Path.Combine(singleItemPackage.PhysicalPath, c_JsonErrorReportFn_SingleItem);
            }
            else
            {
                return string.Concat(reportPrefix, "_", c_JsonErrorReportFn);
            }
        }

        [DataContract]
        private class TabulationErrorDto : IComparable<TabulationErrorDto>
        {
            //[JsonIgnore]
            public string Folder { get; set; }
            //[JsonIgnore]
            public string BankKey { get; set; }
            //[JsonIgnore]
            public string ItemId { get; set; }
            //[JsonIgnore]
            public string ItemType { get; set; }
            [DataMember(Name = "category", Order = 0)]
            public string Category { get; set; }
            [DataMember(Name = "severity", Order = 1)]
            public string Severity { get; set; }
            [DataMember(Name = "message", Order = 2)]
            public string Message { get; set; }
            [DataMember(Name = "detail", Order = 3)]
            public string Detail { get; set; }

            public int CompareTo(TabulationErrorDto other)
            {
                int cmp = CompareSeverity(Severity, other.Severity);
                if (cmp != 0)
                    return cmp;

                cmp = string.Compare(Category, other.Category, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                    return cmp;

                cmp = string.Compare(Message, other.Message, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                    return cmp;

                return string.Compare(Detail, other.Detail, StringComparison.OrdinalIgnoreCase);
            }

            static int CompareSeverity(string x, string y)
            {
                ErrorSeverity xs;
                ErrorSeverity ys;

                if (Enum.TryParse(x, out xs)
                    && Enum.TryParse(y, out ys))
                {
                    return (int)ys - (int)xs;
                }

                return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            }
        }

    }
}
