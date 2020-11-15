using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using TabulateSmarterTestContentPackage.Models;
using System.Collections;

namespace TabulateSmarterTestContentPackage
{

    /// <summary>
    /// An abstract class that manages access to a test package.
    /// Implementations are on the conventional file system (unpacked package),
    /// a .zip file (typical package), and GitLab item bank.
    /// </summary>
    public abstract class TestPackage : IDisposable
    {
        ItemsEnumerable m_itemsEnumerable;

        public TestPackage()
        {
            m_itemsEnumerable = new ItemsEnumerable(this);
        }

        public abstract string Name { get; }

        /// <summary>
        /// An enumerable collection of the IDs of all items and stimuli in the package.
        /// </summary>
        public IEnumerable<ItemIdentifier> ItemsAndStimuli
        {
            get { return m_itemsEnumerable; }
        }

        /// <summary>
        /// Returns an enumerator for Items and Stimuli. Must be implemented be a derived class.
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerator<ItemIdentifier> GetItemEnumerator();

        /// <summary>
        /// Indicates whether the package contains a particular item.
        /// </summary>
        /// <param name="">The item ID to look for.</param>
        /// <returns>True if the item exists, false if it doesn't.</returns>
        /// <remarks>The default implementation calls <see cref="TryGetItem"/>. Override if a more efficient method exists.</remarks>
        public virtual bool ItemExists(ItemIdentifier ii)
        {
            FileFolder ff;
            return TryGetItem(ii, out ff);
        }

        /// <summary>
        /// Gets an item or stimulus in the form of a <see cref="FileFolder`"/>
        /// </summary>
        /// <param name="ii">The item ID or null if seeking the root folder.</param>
        /// <param name="value">Returns the folder if it is found.</param>
        /// <returns>True if the folder is found. Otherwise, false.</returns>
        public abstract bool TryGetItem(ItemIdentifier ii, out FileFolder ff);

        /// <summary>
        /// Gets an item or stimulus in the form of a <see cref="FileFolder"/>
        /// </summary>
        /// <param name="ii">The item ID or null if seeking the root folder.</param>
        /// <returns>Returns the folder.</returns>
        public FileFolder GetItem(ItemIdentifier ii)
        {
            FileFolder ff;
            if (!TryGetItem(ii, out ff))
                throw new ArgumentException($"Item not found ({ii}).");
            return ff;
        }

        public abstract void Dispose();

        private class ItemsEnumerable : IEnumerable<ItemIdentifier>
        {
            TestPackage m_package;

            public ItemsEnumerable(TestPackage package)
            {
                m_package = package;
            }

            public IEnumerator<ItemIdentifier> GetEnumerator()
            {
                return m_package.GetItemEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

    }

    /// <summary>
    /// An abstract class that manages a subtree of files
    /// Implementations are on the convetional file system and on a .zip file.
    /// Nested .zip files are NOT supported.
    /// </summary>
    public abstract class FileFolder
    {
        string mRootedName;
        string mName;

        public FileFolder(string rootedName, string name)
        {
            mRootedName = rootedName;
            mName = name;
        }

        // --- Properties ---

        /// <summary>
        /// The name of the file
        /// </summary>
        public string Name { get { return mName; } }

        /// <summary>
        /// The path to the file from the root of the collection. Starts with a backslash but no drive letter.
        /// </summary>
        public string RootedName { get { return mRootedName; } }

        public abstract ICollection<FileFolder> Folders { get; }
        public abstract ICollection<FileFile> Files { get; }

        // Methods
        public abstract bool TryGetFolder(string path, out FileFolder value);
        public abstract bool TryGetFile(string path, out FileFile value);

        /// <summary>
        /// Indicates whether a subfolder exists in the folder
        /// </summary>
        /// <param name="path">Path to the file to check for existence</param>
        /// <returns>True if the folder exists, false if it doesn't.</returns>
        /// <remarks>The default implementation calls <see cref="TryGetFolder"/>. Override if a more efficient approach exists.
        /// </remarks>
        public virtual bool FolderExists(string path)
        {
            FileFolder folder;
            return TryGetFolder(path, out folder);
        }

        /// <summary>
        /// Indicates whether a file exists in the folder
        /// </summary>
        /// <param name="path">Path to the file to check for existence</param>
        /// <returns>True if the file exists, false if it doesn't.</returns>
        /// <remarks>The default implementation calls <see cref="TryGetFile"/>. Override if a more efficient approach exists.
        /// </remarks>
        public virtual bool FileExists(string path)
        {
            FileFile file;
            return TryGetFile(path, out file);
        }

        public FileFolder GetFolder(string path)
        {
            FileFolder ff;
            if (!TryGetFolder(path, out ff))
                throw new ArgumentException("Folder not found.");
            return ff;
        }

        public FileFile GetFile(string path)
        {
            FileFile file;
            if (!TryGetFile(path, out file))
                throw new ArgumentException("File not found.");
            return file;
        }

        public override string ToString()
        {
            return RootedName;
        }
    }

    public abstract class FileFile
    {
        string mRootedName;
        string mName;

        public FileFile(string rootedName, string name)
        {
            mRootedName = rootedName;
            mName = name;
        }

        /// <summary>
        /// The name of the file
        /// </summary>
        public string Name { get { return mName; } }

        /// <summary>
        /// The path to the file from the root of the collection. Starts with a backslash but no drive letter.
        /// </summary>
        public string RootedName { get { return mRootedName; } }

        string mExtension = null;
        /// <summary>
        /// The filename extension (e.g. ".xml")
        /// </summary>
        public string Extension
        {
            get
            {
                if (mExtension == null)
                {
                    int n = mName.LastIndexOf('.');
                    mExtension = (n >= 0) ? mName.Substring(n) : string.Empty;
                }
                return mExtension;
            }
        }

        /// <summary>
        /// Size of the file
        /// </summary>
        public abstract long Length { get; }

        /// <summary>
        /// Opens a read-only stream on the file
        /// </summary>
        /// <returns>A read only stream</returns>
        public abstract Stream Open();

        public override string ToString()
        {
            return RootedName;
        }

    }

    /// <summary>
    /// Item enumerator for packages that implement a conventional folder hierarchy
    /// </summary>
    class FolderItemEnumerator : IEnumerator<ItemIdentifier>
    {
        const string c_itemsFolder = "Items";
        const string c_stimuliFolder = "Stimuli";

        FileFolder m_rootFolder;
        ItemIdentifier m_current;
        int m_status; // 0=pre-start, 1=Items, 2=Stimuli, 3=done
        IEnumerator<FileFolder> m_enumerator;

        public FolderItemEnumerator(FileFolder rootFolder)
        {
            m_rootFolder = rootFolder;
        }

        public ItemIdentifier Current => m_current;

        object IEnumerator.Current => m_current;

        public bool MoveNext()
        {
            if (m_status >= 3) return false;

            for (; ; )
            {
                if (m_enumerator != null)
                {
                    if (m_enumerator.MoveNext())
                    {
                        if (ItemIdentifier.TryParse(m_enumerator.Current.Name, out m_current))
                        {
                            return true;
                        }
                        else
                        {
                            continue; // Not an item folder, cycle again.
                        }
                    }
                    else
                    {
                        m_enumerator.Dispose();
                        m_enumerator = null;
                    }
                }

                // Advance to the next status level
                ++m_status;
                if (m_status >= 3) return false;

                // Load up the next enumerator and recycle
                FileFolder folder;
                if (m_rootFolder.TryGetFolder((m_status == 1) ? c_itemsFolder : c_stimuliFolder, out folder))
                {
                    m_enumerator = folder.Folders.GetEnumerator();
                }
            }
        }

        public void Reset()
        {
            if (m_enumerator != null)
            {
                m_enumerator.Dispose();
            }
            m_enumerator = null;
            m_status = 0;
            m_current = null;
        }

        public void Dispose()
        {
            if (m_enumerator != null)
            {
                m_enumerator.Dispose();
            }
            m_enumerator = null;
        }
    }


    public class FsPackage : TestPackage
    {
        string m_name;
        FsFolder m_rootFolder;

        public FsPackage(string physicalRoot)
        {
            m_name = Path.GetFileName(physicalRoot);
            m_rootFolder = new FsFolder(physicalRoot);
        }

        public override string Name => m_name;

        public override bool TryGetItem(ItemIdentifier ii, out FileFolder ff)
        {
            if (ii == null)
            {
                ff = m_rootFolder;
                return true;
            }
            return m_rootFolder.TryGetFolder(ii.FolderName, out ff);
        }

        protected override IEnumerator<ItemIdentifier> GetItemEnumerator()
        {
            return new FolderItemEnumerator(m_rootFolder);
        }

        public override void Dispose()
        {
            // Nothing to do here.
        }
    }

    public class FsFolder : FileFolder
    {
        string m_PhysicalPath;

        public FsFolder(string physicalRoot)
            : base("/", string.Empty)
        {
            m_PhysicalPath = physicalRoot;
        }

        private FsFolder(string physicalPath, string rootedName, string name)
            : base(rootedName, name)
        {
            m_PhysicalPath = physicalPath;
        }

        List<FileFolder> mFolders;
        public override ICollection<FileFolder> Folders
        {
            get
            {
                if (mFolders == null)
                {
                    mFolders = new List<FileFolder>();
                    var rootedNamePrefix = (RootedName.Length <= 1) ? RootedName : string.Concat(RootedName, "/");
                    var diThis = new DirectoryInfo(m_PhysicalPath);
                    foreach (var di in diThis.EnumerateDirectories())
                    {
                        mFolders.Add(new FsFolder(di.FullName, string.Concat(rootedNamePrefix, di.Name), di.Name));
                    }
                }
                return mFolders;
            }
        }

        List<FileFile> mFiles;
        public override ICollection<FileFile> Files
        {
            get
            {
                if (mFiles == null)
                {
                    mFiles = new List<FileFile>();
                    string rootedNamePrefix = (RootedName.Length <= 1) ? RootedName : string.Concat(RootedName, "/");
                    DirectoryInfo diThis = new DirectoryInfo(m_PhysicalPath);
                    foreach (FileInfo fi in diThis.EnumerateFiles())
                    {
                        mFiles.Add(new FsFile(fi.FullName, string.Concat(rootedNamePrefix, fi.Name), fi.Name));
                    }
                }
                return mFiles;
            }
        }

        public override bool TryGetFolder(string path, out FileFolder value)
        {
            bool isFile;
            string name;
            string rootedName;
            string physicalPath;
            if (!ValidatePathAndGetNames(path, out isFile, out name, out rootedName, out physicalPath)
                || isFile)
            {
                value = null;
                return false;
            }
            value = new FsFolder(physicalPath, rootedName, name);
            return true;
        }

        public override bool TryGetFile(string path, out FileFile value)
        {
            bool isFile;
            string name;
            string rootedName;
            string physicalPath;
            if (!ValidatePathAndGetNames(path, out isFile, out name, out rootedName, out physicalPath)
                || !isFile)
            {
                value = null;
                return false;
            }
            value = new FsFile(physicalPath, rootedName, name);
            return true;
        }

        static readonly char[] sSlashes = new char[] { '/', '\\' };
        static readonly char[] sWildcards = new char[] { '*', '?' };

        /// <summary>
        /// Validate a path and return whether it leads to a file or a directory
        /// </summary>
        /// <param name="path">The path to validate, must be a subpath of this folder</param>
        /// <param name="isFile">True if it validates to a file, false if folder/directory</param>
        /// <param name="name">Actual name, with capitalization, of the target</param>
        /// <param name="rootedName">Name relative to the root of the test package</param>
        /// <param name="physicalPath">Physical path to the file or folder.</param>
        /// <returns>True if a file or directory exists at the specified path. Else false.</returns>
        private bool ValidatePathAndGetNames(string path, out bool isFile, out string name, out string rootedName, out string physicalPath)
        {
            if (path.IndexOfAny(sWildcards) >= 0) throw new ArgumentException("Wildcards not supported", "path");
            if (path[0] == '/' || path[0] == '\\') throw new ArgumentException("Absolute path not supported", "path");
            string[] parts = path.Split(sSlashes);
            if (parts.Length == 0) throw new ArgumentException("Empty path", "path");

            // Traverse the path and validate each part.
            rootedName = RootedName;
            DirectoryInfo di = new DirectoryInfo(m_PhysicalPath);

            for (int i = 0; i < parts.Length; ++i)
            {
                FileSystemInfo[] matches = di.GetFileSystemInfos(parts[i]);
                if (matches.Length <= 0) break; // Not found
                Debug.Assert(matches.Length == 1);

                rootedName = (rootedName.Length == 1)
                    ? string.Concat("/", matches[0].Name)
                    : string.Concat(rootedName, "/", matches[0].Name);

                // If last entry in the list
                if (i >= parts.Length - 1)
                {
                    isFile = matches[0] is FileInfo;
                    name = matches[0].Name;
                    physicalPath = matches[0].FullName;
                    return true;
                }

                // Move to next directory
                di = matches[0] as DirectoryInfo;
                if (di == null) break; // not a directory
            }

            // Fail to find file or directory by this name
            isFile = false;
            name = null;
            rootedName = null;
            physicalPath = null;
            return false;
        }

        private class FsFile : FileFile
        {
            string mPhysicalPath;

            public FsFile(string physicalPath, string rootedName, string name)
                : base(rootedName, name)
            {
                mPhysicalPath = physicalPath;
            }

            long mLength = -1;
            public override long Length
            {
                get
                {
                    if (mLength < 0)
                    {
                        FileInfo fi = new FileInfo(mPhysicalPath);
                        mLength = fi.Length;
                    }
                    return mLength;
                }
            }

            public override Stream Open()
            {
                return new FileStream(mPhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
        }
    }

    public class ZipPackage : TestPackage
    {
        string m_name;
        ZipFileTree m_tree;

        public ZipPackage(string zipFileName)
        {
            m_name = Path.GetFileNameWithoutExtension(zipFileName);
            m_tree = new ZipFileTree(zipFileName);
        }

        public override string Name => m_name;

        public override bool TryGetItem(ItemIdentifier ii, out FileFolder ff)
        {
            if (ii == null)
            {
                ff = m_tree;
                return true;
            }
            return m_tree.TryGetFolder(ii.FolderName, out ff);
        }

        protected override IEnumerator<ItemIdentifier> GetItemEnumerator()
        {
            return new FolderItemEnumerator(m_tree);
        }

        public override void Dispose()
        {
            if (m_tree != null)
            {
                m_tree.Dispose();
            }
            m_tree = null;
        }
    }

    class ZipFileTree : FileFolder, IDisposable
    {
        static readonly char[] sSlashes = new char[] { '/', '\\' };

        ZipArchive mZip;
        ZipFileFolder mRoot;

        public ZipFileTree(string zipFileName)
            : base("/", string.Empty)
        {
            mRoot = new ZipFileFolder(this, "/", string.Empty);

            try
            {
                mZip = ZipFile.OpenRead(zipFileName);

                // Enumerate all entries in the archive and fill in the tree
                foreach (ZipArchiveEntry entry in mZip.Entries)
                {
                    string[] parts = entry.FullName.Split(sSlashes);
                    if (parts[parts.Length - 1].Length == 0) continue; // Some archives contain folder names which have trailing slashes
                    ZipFileFolder folder = mRoot;
                    for (int i = 0; i < parts.Length - 1; ++i)
                    {
                        folder = folder.GetOrCreateSubFolder(parts[i]);
                    }
                    folder.AddFile(parts[parts.Length-1], entry);
                }
            }
            catch (Exception err)
            {
                Dispose(true);
                throw new InvalidDataException($"Corrupted zip file '{zipFileName}': {err.Message}", err);
            }
        }

        void Dispose(bool disposing)
        {
#if DEBUG
            if (mZip != null && !disposing)
            {
                Debug.Fail("ZipFileTree was not disposed.");
            }
#endif
            if (mZip != null)
            {
                mZip.Dispose();
                mZip = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~ZipFileTree()
        {
            Dispose(false);
        }

        public override ICollection<FileFolder> Folders
        {
            get { return mRoot.Folders; }
        }

        public override ICollection<FileFile> Files
        {
            get { return mRoot.Files; }
        }

        public override bool TryGetFolder(string path, out FileFolder value)
        {
            return mRoot.TryGetFolder(path, out value);
        }

        public override bool TryGetFile(string path, out FileFile value)
        {
            return mRoot.TryGetFile(path, out value);
        }


        private class ZipFileFolder : FileFolder
        {
            ZipFileTree mTree;
            Dictionary<string, FileFolder> mFolders = new Dictionary<string, FileFolder>();
            Dictionary<string, FileFile> mFiles = new Dictionary<string, FileFile>();

            public ZipFileFolder(ZipFileTree tree, string rootedName, string name)
                : base(rootedName, name)
            {
                mTree = tree;
            }

            public ZipFileFolder GetOrCreateSubFolder(string name)
            {
                string lcName = name.ToLowerInvariant();
                FileFolder folder;
                if (!mFolders.TryGetValue(lcName, out folder))
                {
                    string subRootedName = (RootedName.Length <= 1) ? string.Concat("/", name) : string.Concat(RootedName, "/", name);
                    folder = new ZipFileFolder(mTree, subRootedName, name);
                    mFolders[lcName] = folder;
                }
                return (ZipFileFolder)folder;
            }

            public ZipFileFile AddFile(string name, ZipArchiveEntry entry)
            {
                ZipFileFile file;
                string subRootedName = (RootedName.Length <= 1) ? string.Concat("/", name) : string.Concat(RootedName, "/", name);
                file = new ZipFileFile(subRootedName, name, entry);
                mFiles[name.ToLowerInvariant()] = file;
                return file;
            }

            private bool FollowPath(string path, out ZipFileFolder folder, out string name)
            {
                string[] parts = path.ToLowerInvariant().Split(sSlashes);
                if (parts.Length < 1) throw new FileNotFoundException("Empty path: " + path);
                if (parts[0].Length == 0) throw new FileNotFoundException("Absolute paths not supported: " + path);

                ZipFileFolder f = this;
                for (int i=0; i<parts.Length-1; ++i)
                {
                    FileFolder next;
                    if (!f.mFolders.TryGetValue(parts[i], out next))
                    {
                        folder = null;
                        name = null;
                        return false;
                    }
                    f = (ZipFileFolder)next;
                }

                folder = f;
                name = parts[parts.Length - 1];
                return true;
            }

            public override ICollection<FileFolder> Folders
            {
                get { return mFolders.Values; }
            }

            public override ICollection<FileFile> Files
            {
                get { return mFiles.Values; }
            }

            public override bool TryGetFolder(string path, out FileFolder value)
            {
                ZipFileFolder folder;
                string name;
                if (!FollowPath(path, out folder, out name)
                    || !folder.mFolders.TryGetValue(name, out value))
                {
                    value = null;
                    return false;
                }
                return true;
            }

            public override bool TryGetFile(string path, out FileFile value)
            {
                ZipFileFolder folder;
                string name;
                if (FollowPath(path, out folder, out name))
                {
                    if (folder.mFiles.TryGetValue(name, out value))
                    {
                        return true;
                    }
                }
                value = null;
                return false;
            }
        }

        private class ZipFileFile : FileFile
        {
            ZipArchiveEntry mEntry;

            public ZipFileFile(string rootedName, string name, ZipArchiveEntry entry)
                : base(rootedName, name)
            {
                mEntry = entry;
            }

            public override long Length
            {
                get { return mEntry.Length; }
            }

            public override Stream Open()
            {
                return mEntry.Open();
            }

        } // ZipFileFile
        
    } // ZipFileTree
}
