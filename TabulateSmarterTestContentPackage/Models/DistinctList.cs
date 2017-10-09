using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TabulateSmarterTestContentPackage.Models
{
    /// <summary>
    /// A list of objects that will only contain one instance of any particular value.
    /// </summary>
    /// <typeparam name="T">Type of objects in the list.</typeparam>
    class DistinctList<T> : IList<T>
    {
        HashSet<T> m_set = new HashSet<T>();
        List<T> m_list = new List<T>();

        public void Sort()
        {
            m_list.Sort();
        }

#region IList members

        public T this[int index]
        {
            get { return m_list[index]; }
            set
            {
                if (m_list[index].Equals(value))
                {
                    // Do nothing
                }
                else if (m_set.Add(value))
                {
                    m_set.Remove(m_list[index]);
                    m_list[index] = value;
                }
                else
                {
                    throw new ArgumentException("Value already exists in DistinctList.");
                }
            }
        }

        public int Count => m_list.Count;

        public bool IsReadOnly => false;

        public bool Add(T item)
        {
            if (m_set.Add(item))
            {
                m_list.Add(item);
                return true;
            }
            else
            {
                return false;
            }
        }

        public int AddRange(IEnumerable<T> collection)
        {
            int count = 0;

            foreach (var value in collection)
            {
                if (Add(value))
                {
                    ++count;
                }
            }

            return count;
        }

        public void Clear()
        {
            m_set.Clear();
            m_list.Clear();
        }

        public bool Contains(T item)
        {
            return m_set.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            m_list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_list.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return m_list.IndexOf(item);
        }

        public bool Insert(int index, T item)
        {
            if (m_set.Add(item))
            {
                m_list.Insert(index, item);
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool Remove(T item)
        {
            if (m_set.Remove(item))
            {
                m_list.Remove(item);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void RemoveAt(int index)
        {
            m_set.Remove(m_list[index]);
            m_list.RemoveAt(index);
        }

        void ICollection<T>.Add(T item)
        {
            if (!Add(item))
            {
                throw new ArgumentException("Value already exists in DistinctList.");
            }
        }

        void IList<T>.Insert(int index, T item)
        {
            if (!Insert(index, item))
            {
                throw new AggregateException("Value already exists in DistinctList.");
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

#endregion //IList members

    }
}
