using System;
using System.Text;
using System.Security.Cryptography;

namespace TabulateSmarterTestContentPackage.Models
{
    class MD5Hash
    {
        static Encoding sUtf8NoBom = new UTF8Encoding(false, false);

        byte[] m_hash;

        public MD5Hash(string text)
        {
            var hasher = new MD5CryptoServiceProvider();
            m_hash = hasher.ComputeHash(sUtf8NoBom.GetBytes(text));
        }

        public override int GetHashCode()
        {
            int[] value = new int[1];
            Buffer.BlockCopy(m_hash, 0, value, 0, sizeof(int));
            return value[0];
        }

        public override bool Equals(object obj)
        {
            MD5Hash other = obj as MD5Hash;
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
            StringBuilder sb = new StringBuilder();
            foreach (byte b in m_hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
