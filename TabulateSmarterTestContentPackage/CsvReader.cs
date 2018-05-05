using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace TabulateSmarterTestContentPackage
{
    class CsvReader : IDisposable
    {
        TextReader m_reader;

        public CsvReader(string filename)
        {
            m_reader = new StreamReader(filename, true);
        }

        public CsvReader(TextReader reader, bool autoCloseReader = true)
        {
            m_reader = reader;
        }

        /// <summary>
        /// Read one line from a CSV file
        /// </summary>
        /// <returns>An array of strings parsed from the line or null if at end-of-file.</returns>
        public string[] Read()
        {
            List<string> line = new List<string>();
            StringBuilder builder = new StringBuilder();

            if (m_reader.Peek() < 0) return null;

            for (; ; )
            {
                int c = m_reader.Read();
                char ch = (c >= 0) ? (char)c : '\n'; // Treat EOF like newline.

                // Reduce CRLF to LF
                if (ch == '\r')
                {
                    if (m_reader.Peek() == '\n') continue;
                    ch = '\n';
                }

                if (ch == '\n')
                {
                    line.Add(builder.ToString());
                    break;
                }
                else if (ch == ',')
                {
                    line.Add(builder.ToString());
                    builder.Clear();
                }
                else if (ch == '"')
                {
                    for (; ; )
                    {
                        c = m_reader.Read();
                        if (c < 0) break;
                        ch = (char)c;

                        if (ch == '"')
                        {
                            if (m_reader.Peek() == (int)'"')
                            {
                                // Double quote means embedded quote
                                m_reader.Read(); // read the second quote
                            }
                            else
                            {
                                break;
                            }
                        }
                        builder.Append(ch);
                    }
                } // if quote
                else
                {
                    builder.Append(ch);
                }
            } // forever loop

            return line.ToArray();
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (m_reader != null)
            {
                m_reader.Dispose();
                m_reader = null;
#if DEBUG
                if (!disposing)
                {
                    System.Diagnostics.Debug.Fail("Failed to dispose CsvReader.");
                }
#endif
            }
        }

        ~CsvReader()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

}
