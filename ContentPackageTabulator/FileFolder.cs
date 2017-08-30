using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace ContentPackageTabulator
{
    /// <summary>
    ///     An abstract class that manages a subtree of files
    ///     Implementations are on the convetional file system and on a .zip file.
    ///     Nested .zip files are NOT supported.
    /// </summary>
    public abstract class FileFolder
    {
        public FileFolder(string rootedName, string name)
        {
            RootedName = rootedName;
            Name = name;
        }

        // --- Properties ---

        /// <summary>
        ///     The name of the file
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     The path to the file from the root of the collection. Starts with a backslash but no drive letter.
        /// </summary>
        public string RootedName { get; }

        public abstract ICollection<FileFolder> Folders { get; }
        public abstract ICollection<FileFile> Files { get; }

        // Methods
        public abstract bool TryGetFolder(string path, out FileFolder value);
        public abstract bool TryGetFile(string path, out FileFile value);

        public bool FileExists(string path)
        {
            FileFile file;
            return TryGetFile(path, out file);
        }

        public FileFolder GetFolder(string path)
        {
            FileFolder ff;
            if (!TryGetFolder(path, out ff))
            {
                throw new ArgumentException("Folder not found.");
            }
            return ff;
        }

        public FileFile GetFile(string path)
        {
            FileFile file;
            if (!TryGetFile(path, out file))
            {
                throw new ArgumentException("File not found.");
            }
            return file;
        }
    }

    public abstract class FileFile
    {
        private string mExtension;

        public FileFile(string rootedName, string name)
        {
            RootedName = rootedName;
            Name = name;
        }

        /// <summary>
        ///     The name of the file
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     The path to the file from the root of the collection. Starts with a backslash but no drive letter.
        /// </summary>
        public string RootedName { get; }

        /// <summary>
        ///     The filename extension (e.g. ".xml")
        /// </summary>
        public string Extension
        {
            get
            {
                if (mExtension == null)
                {
                    var n = Name.LastIndexOf('.');
                    mExtension = n >= 0 ? Name.Substring(n) : string.Empty;
                }
                return mExtension;
            }
        }

        /// <summary>
        ///     Size of the file
        /// </summary>
        public abstract long Length { get; }

        /// <summary>
        ///     Opens a read-only stream on the file
        /// </summary>
        /// <returns>A read only stream</returns>
        public abstract Stream Open();
    }

    public class FsFolder : FileFolder
    {
        private static readonly char[] sSlashes = {'/', '\\'};

        private List<FileFile> mFiles;

        private List<FileFolder> mFolders;

        public FsFolder(string physicalRoot)
            : base("/", string.Empty)
        {
            mPhysicalPath = physicalRoot;
        }

        private FsFolder(string physicalPath, string rootedName, string name)
            : base(rootedName, name)
        {
            mPhysicalPath = physicalPath;
        }

        public string mPhysicalPath { get; set; }

        public override ICollection<FileFolder> Folders
        {
            get
            {
                if (mFolders == null)
                {
                    mFolders = new List<FileFolder>();
                    var rootedNamePrefix = RootedName.Length <= 1 ? RootedName : string.Concat(RootedName, "/");
                    var diThis = new DirectoryInfo(mPhysicalPath);
                    foreach (var di in diThis.EnumerateDirectories())
                    {
                        mFolders.Add(new FsFolder(di.FullName, string.Concat(rootedNamePrefix, di.Name), di.Name));
                    }
                }
                return mFolders;
            }
        }

        public override ICollection<FileFile> Files
        {
            get
            {
                if (mFiles == null)
                {
                    mFiles = new List<FileFile>();
                    var rootedNamePrefix = RootedName.Length <= 1 ? RootedName : string.Concat(RootedName, "/");
                    var diThis = new DirectoryInfo(mPhysicalPath);
                    foreach (var fi in diThis.EnumerateFiles())
                    {
                        mFiles.Add(new FsFile(fi.FullName, string.Concat(rootedNamePrefix, fi.Name), fi.Name));
                    }
                }
                return mFiles;
            }
        }

        public override bool TryGetFolder(string path, out FileFolder value)
        {
            value = null;
            var physicalPath = ToPhysicalPath(path);
            if (!Directory.Exists(physicalPath))
            {
                return false;
            }
            var rootedName = ToRootedName(path);
            var name = ToName(path);
            value = new FsFolder(physicalPath, rootedName, name);
            return true;
        }

        public override bool TryGetFile(string path, out FileFile value)
        {
            value = null;
            var physicalPath = ToPhysicalPath(path);
            if (!File.Exists(physicalPath))
            {
                return false;
            }
            var rootedName = ToRootedName(path);
            var name = ToName(path);
            value = new FsFile(physicalPath, rootedName, name);
            return true;
        }

        private string ToPhysicalPath(string relativePath)
        {
            return Path.Combine(mPhysicalPath, relativePath.Replace('/', '\\'));
        }

        private string ToRootedName(string path)
        {
            path = path.Replace('\\', '/');
            if (path[0] == '/')
            {
                throw new ArgumentException("Absolute paths not supported!");
            }
            if (RootedName.Length == 1)
            {
                return string.Concat("/", path);
            }
            return string.Concat(RootedName, "/", path);
        }

        private string ToName(string path)
        {
            var n = path.LastIndexOfAny(sSlashes);
            return n >= 0 ? path.Substring(n + 1) : path;
        }

        private class FsFile : FileFile
        {
            private readonly string mPhysicalPath;
            private long mLength = -1;

            public FsFile(string physicalPath, string rootedName, string name)
                : base(rootedName, name)
            {
                mPhysicalPath = physicalPath;
            }

            public override long Length
            {
                get
                {
                    if (mLength < 0)
                    {
                        var fi = new FileInfo(mPhysicalPath);
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

    internal class ZipFileTree : FileFolder, IDisposable
    {
        private static readonly char[] sSlashes = {'/', '\\'};
        private readonly ZipFileFolder mRoot;

        private ZipArchive mZip;

        public ZipFileTree(string zipFileName)
            : base("/", string.Empty)
        {
            mRoot = new ZipFileFolder(this, "/", string.Empty);

            try
            {
                mZip = ZipFile.OpenRead(zipFileName);

                // Enumerate all entries in the archive and fill in the tree
                foreach (var entry in mZip.Entries)
                {
                    var parts = entry.FullName.Split(sSlashes);
                    if (parts[parts.Length - 1].Length == 0)
                    {
                        continue; // Some archives contain folder names which have trailing slashes
                    }
                    var folder = mRoot;
                    for (var i = 0; i < parts.Length - 1; ++i)
                    {
                        folder = folder.GetOrCreateSubFolder(parts[i]);
                    }
                    folder.AddFile(parts[parts.Length - 1], entry);
                }
            }
            catch (Exception err)
            {
                Dispose(true);
                throw new InvalidDataException($"Corrupted zip file '{zipFileName}': {err.Message}", err);
            }
        }

        public override ICollection<FileFolder> Folders
        {
            get { return mRoot.Folders; }
        }

        public override ICollection<FileFile> Files
        {
            get { return mRoot.Files; }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
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

        ~ZipFileTree()
        {
            Dispose(false);
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
            private readonly Dictionary<string, FileFile> mFiles = new Dictionary<string, FileFile>();
            private readonly Dictionary<string, FileFolder> mFolders = new Dictionary<string, FileFolder>();
            private readonly ZipFileTree mTree;

            public ZipFileFolder(ZipFileTree tree, string rootedName, string name)
                : base(rootedName, name)
            {
                mTree = tree;
            }

            public override ICollection<FileFolder> Folders
            {
                get { return mFolders.Values; }
            }

            public override ICollection<FileFile> Files
            {
                get { return mFiles.Values; }
            }

            public ZipFileFolder GetOrCreateSubFolder(string name)
            {
                var lcName = name.ToLowerInvariant();
                FileFolder folder;
                if (!mFolders.TryGetValue(lcName, out folder))
                {
                    var subRootedName = RootedName.Length <= 1
                        ? string.Concat("/", name)
                        : string.Concat(RootedName, "/", name);
                    folder = new ZipFileFolder(mTree, subRootedName, name);
                    mFolders[lcName] = folder;
                }
                return (ZipFileFolder) folder;
            }

            public ZipFileFile AddFile(string name, ZipArchiveEntry entry)
            {
                ZipFileFile file;
                var subRootedName = RootedName.Length <= 1
                    ? string.Concat("/", name)
                    : string.Concat(RootedName, "/", name);
                file = new ZipFileFile(subRootedName, name, entry);
                mFiles[name.ToLowerInvariant()] = file;
                return file;
            }

            private bool FollowPath(string path, out ZipFileFolder folder, out string name)
            {
                var parts = path.ToLowerInvariant().Split(sSlashes);
                if (parts.Length < 1)
                {
                    throw new FileNotFoundException("Empty path: " + path);
                }
                if (parts[0].Length == 0)
                {
                    throw new FileNotFoundException("Absolute paths not supported: " + path);
                }

                var f = this;
                for (var i = 0; i < parts.Length - 1; ++i)
                {
                    FileFolder next;
                    if (!f.mFolders.TryGetValue(parts[i], out next))
                    {
                        folder = null;
                        name = null;
                        return false;
                    }
                    f = (ZipFileFolder) next;
                }

                folder = f;
                name = parts[parts.Length - 1];
                return true;
            }

            public override bool TryGetFolder(string path, out FileFolder value)
            {
                ZipFileFolder folder;
                string name;
                if (!FollowPath(path, out folder, out name)
                    || !mFolders.TryGetValue(name, out value))
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
            private readonly ZipArchiveEntry mEntry;

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