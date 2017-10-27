using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace TabulateSmarterTestContentPackage.Models
{
    class ShaHash
    {
        static Encoding sUtf8NoBom = new UTF8Encoding(false, false);

        byte[] m_hash;

        public ShaHash(string text)
        {
            var hasher = new SHA1CryptoServiceProvider();
            m_hash = hasher.ComputeHash(sUtf8NoBom.GetBytes(text));
        }

        public ShaHash(System.IO.Stream stream)
        {
            var hasher = new SHA1CryptoServiceProvider();
            m_hash = hasher.ComputeHash(stream);
        }

        public override int GetHashCode()
        {
            int[] value = new int[1];
            Buffer.BlockCopy(m_hash, 0, value, 0, sizeof(int));
            return value[0];
        }

        public override bool Equals(object obj)
        {
            ShaHash other = obj as ShaHash;
            if (other == null) return false;
            if (m_hash.Length != other.m_hash.Length) return false;
            for (int i=0; i<m_hash.Length; ++i)
            {
                if (m_hash[i] != other.m_hash[i]) return false;
            }
            return true;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach(byte b in m_hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
