using System;
using System.IO;
using System.Text;

/*
MP4 File Format Reference: http://standards.iso.org/ittf/PubliclyAvailableStandards/c068960_ISO_IEC_14496-12_2015.zip
*/

namespace TabulateSmarterTestContentPackage.Utilities
{
    public class Mp4VideoUtility : IDisposable
    {
        private readonly Box m_root;
        private BinaryReader m_reader;
        private string m_tempFileName;

        /// <summary>
        ///     Create the MP4 object from a filename.
        /// </summary>
        /// <param name="filename">Name of an MP4 file.</param>
        public Mp4VideoUtility(string filename)
        {
            m_reader = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read),
                Encoding.UTF8, false);
            m_root = new Box(m_reader);

            var b = m_root.FirstChild;
            if (!b.BoxType.Equals("ftyp", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("File is not MP4 format.");
            }
        }

        /// <summary>
        /// Create the MP4 object from a stream.
        /// </summary>
        /// <param name="stream">The stream containing an MP4 file.</param>
        public Mp4VideoUtility(Stream stream)
        {
            Stream tempStream = null;
            try
            {
                // If cannot seek in the stream (probably from a .zip file) then copy to a temporary stream.
                if (!stream.CanSeek)
                {
                    m_tempFileName = Path.GetTempFileName();
                    tempStream = File.Open(m_tempFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    stream.CopyTo(tempStream);
                    tempStream.Position = 0;
                    m_reader = new BinaryReader(tempStream, Encoding.UTF8, false);
                    tempStream = null; // BinaryReader now owns the stream
                }
                else
                {
                    m_reader = new BinaryReader(stream, Encoding.UTF8, true);
                }
                m_root = new Box(m_reader);

                var b = m_root.FirstChild;
                if (!b.BoxType.Equals("ftyp", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("File is not MP4 format.");
                }
            }
            finally
            {
                if (tempStream != null)
                {
                    tempStream.Dispose();
                    tempStream = null;
                    if (m_tempFileName != null)
                    {
                        try
                        {
                            File.Delete(m_tempFileName);
                        }
                        catch
                        {
                            // Suppress any further exception.
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Dispose the MP4 file.
        /// </summary>
        public void Dispose()
        {
            m_reader?.Dispose();
            m_reader = null;
            if (m_tempFileName != null)
            {
                try
                {
                    File.Delete(m_tempFileName);
                }
                catch
                {
                    // Suppress any exception.
                }
            }
        }

        /// <summary>
        ///     Get duration of an MP4 file in milliseconds.
        /// </summary>
        /// <param name="filename">Name of the MP4 file.</param>
        /// <returns>Duration in milliseconds or -1 if duration is unknown.</returns>
        /// <remarks>
        ///     Throws an exception if the file is not .mp4 format.
        /// </remarks>
        public static long GetDuration(string filename)
        {
            using (var mp4 = new Mp4VideoUtility(filename))
            {
                return mp4.GetDuration();
            }
        }

        /// <summary>
        ///     Get duration of an MP4 in milliseconds
        /// </summary>
        /// <param name="stream">Stream containing the MP4 file.</param>
        /// <returns>Duration in milliseconds or -1 if duration is unknown.</returns>
        /// <remarks>
        ///     Throws an exception if the file is not .mp4 format.
        /// </remarks>
        public static long GetDuration(Stream stream)
        {
            using (var mp4 = new Mp4VideoUtility(stream))
            {
                return mp4.GetDuration();
            }
        }

        /// <summary>
        ///     Return the playing time in milliseconds
        /// </summary>
        /// <returns>Duration in milliseconds or -1 if duration is unknown.</returns>
        /// <remarks>
        ///     Throws an exception if the file is not .mp4 format.
        /// </remarks>
        public long GetDuration()
        {
            for (var b = m_root.FirstChild; b != null; b = b.NextSibling)
            {
                if (b.BoxType.Equals("moov", StringComparison.Ordinal))
                {
                    for (var c = b.FirstChild; c != null; c = c.NextSibling)
                    {
                        if (c.BoxType.Equals("mvhd", StringComparison.Ordinal))
                        {
                            m_reader.BaseStream.Position = (long) c.Body;
                            var version = m_reader.ReadByte();
                            m_reader.ReadBytes(3); // Flags are unused

                            ulong timescale;
                            ulong duration;
                            if (version == 1)
                            {
                                m_reader.ReadUInt64BE(); // CreationTime
                                m_reader.ReadUInt64BE(); // ModificationTime
                                timescale = m_reader.ReadUInt64BE();
                                duration = m_reader.ReadUInt64BE();
                            }
                            else // version == 0
                            {
                                m_reader.ReadUInt32BE(); // CreationTime
                                m_reader.ReadUInt32BE(); // ModificationTime
                                timescale = m_reader.ReadUInt32BE();
                                duration = m_reader.ReadUInt32BE();
                            }

                            if (duration == ulong.MaxValue)
                            {
                                return -1L; // Duration is unknown
                            }
                            return (long) (duration * 1000 / timescale); // Convert to milliseconds
                        }
                    }
                }
            }
            return 0;
        }

        /// <summary>
        ///     MP4 files are composed of boxes. Represents one box.
        /// </summary>
        private class Box
        {
            private readonly BinaryReader m_reader;

            /// <summary>
            ///     Creates the root box
            /// </summary>
            /// <param name="reader">The BinaryReader for the file.</param>
            public Box(BinaryReader reader)
            {
                m_reader = reader;
                Parent = null;
                Start = 0;
                Size = (ulong) reader.BaseStream.Length;
                Body = 0;
                BoxType = string.Empty;
            }

            private Box(Box parent, ulong start)
            {
                m_reader = parent.m_reader;
                Parent = parent;
                Start = start;
                m_reader.BaseStream.Position = (long) start;
                var size = m_reader.ReadUInt32BE();
                BoxType = Encoding.ASCII.GetString(m_reader.ReadBytes(4));
                Body = Start + 8;
                if (size == 0)
                {
                    Size = (ulong) m_reader.BaseStream.Length - Start;
                }
                else if (size == 1)
                {
                    Size = m_reader.ReadUInt64BE();
                    Body += 8;
                }
                else
                {
                    Size = size;
                }
                if (BoxType.Equals("uuid", StringComparison.Ordinal))
                {
                    BoxType = Encoding.ASCII.GetString(m_reader.ReadBytes(16));
                    Body += 16;
                }
            }

            private ulong Start { get; }

            private ulong Size { get; }

            public string BoxType { get; }

            public ulong Body { get; }

            private Box Parent { get; }

            public Box FirstChild => new Box(this, Body);

            public Box NextSibling
            {
                get
                {
                    var start = Start + Size;
                    if (start >= Parent.Start + Parent.Size)
                    {
                        return null;
                    }
                    return new Box(Parent, Start + Size);
                }
            }
        }
    }

    /// <summary>
    ///     Extend BinaryReader with Big-Endian Versions
    /// </summary>
    internal static class BinaryReaderHelper
    {
        public static uint ReadUInt32BE(this BinaryReader reader)
        {
            var a = reader.ReadBytes(4);
            Array.Reverse(a);
            return BitConverter.ToUInt32(a, 0);
        }

        public static ulong ReadUInt64BE(this BinaryReader reader)
        {
            var a = reader.ReadBytes(8);
            Array.Reverse(a);
            return BitConverter.ToUInt64(a, 0);
        }
    }
}