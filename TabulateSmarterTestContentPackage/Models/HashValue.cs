using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace TabulateSmarterTestContentPackage.Models
{
    class HashValue
    {
        byte[] m_hash;

        public HashValue(string inHexadecimal)
        {
            m_hash = FromHex(inHexadecimal);
        }

        public HashValue(byte[] hash)
        {
            m_hash = hash;
        }

        public override int GetHashCode()
        {
            if (m_hash == null || m_hash.Length == 0) return 0;
            if (m_hash.Length < sizeof(int)) return m_hash[0];
            int[] value = new int[1];
            Buffer.BlockCopy(m_hash, 0, value, 0, sizeof(int));
            return value[0];
        }

        public override bool Equals(object obj)
        {
            HashValue other = obj as HashValue;
            if (other == null) return false;
            if (m_hash.Length != other.m_hash.Length) return false;
            for (int i = 0; i < m_hash.Length; ++i)
            {
                if (m_hash[i] != other.m_hash[i]) return false;
            }
            return true;
        }

        public override string ToString()
        {
            return ToHex(m_hash);
        }

        public static string ToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length*2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        public static byte[] FromHex(string hex)
        {
            int byteLen = hex.Length / 2;
            byte[] bytes = new byte[byteLen];
            for (int i=0; i<bytes.Length; ++i)
            {
                bytes[i] = FromHex(hex[i * 2], hex[i * 2 + 1]);
            }
            return bytes;
        }

        private static byte FromHex(char u, char l)
        {
            return
                (byte)(
                    ( (l < 'A') ? ((uint)l) & 0x0F : ((((uint)l) & 0x0F) + 9) )
                    | ( ((u < 'A') ? ((uint)u) & 0x0F : ((((uint)u) & 0x0F) + 9)) << 4)
                );
        }

        static Encoding sUtf8NoBom = new UTF8Encoding(false, false);

        public static HashValue ComputeHash<THashAlgorithm>(string text)
            where THashAlgorithm : HashAlgorithm, new()
        {
            var hasher = new THashAlgorithm();
            return new HashValue(hasher.ComputeHash(sUtf8NoBom.GetBytes(text)));

        }

        public static HashValue ComputeHash<THashAlgorithm>(System.IO.Stream stream)
            where THashAlgorithm : HashAlgorithm, new()
        {
            var hasher = new THashAlgorithm();
            return new HashValue(hasher.ComputeHash(stream));
        }

    }
}
