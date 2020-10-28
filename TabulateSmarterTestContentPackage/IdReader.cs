using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Diagnostics;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage
{
    class IdReadable : IEnumerable<ItemIdentifier>
    {
        string m_filename;
        int m_defaultBankKey;

        public IdReadable(string filename, int defaultBankKey)
        {
            m_filename = filename;
            m_defaultBankKey = defaultBankKey;
        }

        public IEnumerator<ItemIdentifier> GetEnumerator()
        {
            return new IdReader(m_filename, m_defaultBankKey);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class IdReader : IEnumerator<ItemIdentifier>
    {
        string m_filename;
        CsvReader m_csvReader;
        int m_defaultBankKey;
        int m_idColumn = -1;
        int m_bankKeyColumn = -1;
        int m_minColumns = -1;
        ItemIdentifier m_current = null;

        public IdReader(string filename, int defaultBankKey)
        {
            m_filename = filename;
            m_csvReader = new CsvReader(filename);
            m_defaultBankKey = defaultBankKey;
        }

        public ItemIdentifier Current
        {
            get { return m_current; }
        }

        object IEnumerator.Current
        {
            get { return m_current; }
        }

        public bool MoveNext()
        {
            if (m_idColumn == -1)
            {
                return FirstMoveNext();
            }

            for (; ; )
            {
                string[] row = m_csvReader.Read();
                if (row == null) return false;

                if (row.Length < m_minColumns)
                {
                    Console.Error.WriteLine($"   ({Path.GetFileName(m_filename)}) Too few columns in item ID input file row: minColumns ={m_minColumns}");
                    continue;
                }

                int bankKey = m_defaultBankKey;
                if (m_bankKeyColumn >= 0 && !string.IsNullOrEmpty(row[m_bankKeyColumn]))
                {
                    if (!int.TryParse(row[m_bankKeyColumn], out bankKey))
                    {
                        Console.Error.WriteLine($"   ({Path.GetFileName(m_filename)}) Invalid bankKey value in item ID input file row: bankKey='{row[m_bankKeyColumn]}'");
                        continue;
                    }
                }

                if (!ItemIdentifier.TryParse(row[m_idColumn], bankKey, out m_current))
                {
                    Console.Error.WriteLine($"   ({Path.GetFileName(m_filename)}) Invalid item ID in item ID input file row: itemId='{row[m_idColumn]}'");
                    continue;
                }

                return true;
            }
        }

        private bool FirstMoveNext()
        {
            string[] row = m_csvReader.Read();
            if (row == null)
            {
                throw new ArgumentException("Empty item ID input file.");
            }

            // First line is either column headings or an item ID. Determine by looking for expected headings.
            for (int i=0; i<row.Length; ++i)
            {
                if (row[i].Equals("ItemId", StringComparison.OrdinalIgnoreCase) || row[i].Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    m_idColumn = i;
                }
                else if (row[i].Equals("BankKey", StringComparison.OrdinalIgnoreCase))
                {
                    m_bankKeyColumn = i;
                }
            }

            if (m_idColumn >= 0)
            {
                m_minColumns = Math.Max(m_idColumn + 1, m_bankKeyColumn + 1);
                return MoveNext();
            }

            if (row.Length != 0 && ItemIdentifier.TryParse(row[0], m_defaultBankKey, out m_current))
            {
                m_idColumn = 0;
                m_minColumns = 1;
                return true;
            }

            throw new ArgumentException("Item ID input file in unexpected format.");
        }

        public void Reset()
        {
            throw new NotSupportedException("Cannot reset IdReader.");
        }

        public void Dispose()
        {
            if (m_csvReader != null)
            {
                m_csvReader.Dispose();
            }
            m_csvReader = null;
        }
    }

}
