using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;

namespace TabulateSmarterTestContentPackage
{

    /// <summary>
    /// An abstract class that manages a subtree of files
    /// Implementations are on the convetional file system and on a .zip file.
    /// Nested .zip files are NOT supported.
    /// </summary>
    abstract class FileFolder
    {
        // --- Properties ---

        public abstract string Name { get; }

        /// <summary>
        /// The path to the file from the root of the collection. Starts with a backslash but no drive letter.
        /// </summary>
        public abstract string RootedName { get; }
        public abstract IReadOnlyList<FileFolder> Folders { get; }
        public abstract IReadOnlyList<Yada> Files { get; }

        // Methods
        public abstract bool FileExists(string path);
        public abstract FileFolder GetFolder(string path);
        public abstract Yada GetFile(string path);
        public abstract bool TryGetFolder(string path, out FileFolder value);
        public abstract bool TryGetFile(string path, out Yada value);
    }

    abstract class Yada
    {
        /// <summary>
        /// The neame of the file
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// The path to the file from the root of the collection. Starts with a backslash but no drive letter.
        /// </summary>
        public abstract string RootedName { get; }

        /// <summary>
        /// The extension of the filename (e.g. ".xml")
        /// </summary>
        public abstract string Extension { get; }

        /// <summary>
        /// Size of the file
        /// </summary>
        public abstract long Length { get; }

        /// <summary>
        /// Opens a read-only stream on the file
        /// </summary>
        /// <returns>A read only stream</returns>
        public abstract Stream Open();

    }

    class FsFolder : FileFolder
    {
        string mPhysicalPath;
        string mRootedName;
        string mName;

        public FsFolder(string physicalRoot)
        {
            mPhysicalPath = physicalRoot;
            mRootedName = "/";
            mName = string.Empty;
        }

        private FsFolder(string physicalPath, string rootedName, string name)
        {
            mPhysicalPath = physicalPath;
            mRootedName = rootedName;
            mName = name;
        }

        public override string Name
        {
            get { return mName; }
        }

        public override string RootedName
        {
            get { return mRootedName; }
        }

        List<FileFolder> mFolders;
        public override IReadOnlyList<FileFolder> Folders
        {
            get
            {
                if (mFolders == null)
                {
                    mFolders = new List<FileFolder>();
                    string rootedNamePrefix = (mRootedName.Length <= 1) ? mRootedName : string.Concat(mRootedName, "/");
                    DirectoryInfo diThis = new DirectoryInfo(mPhysicalPath);
                    foreach (DirectoryInfo di in diThis.EnumerateDirectories())
                    {
                        mFolders.Add(new FsFolder(di.FullName, string.Concat(rootedNamePrefix, di.Name), di.Name));
                    }
                }
                return mFolders;
            }
        }

        List<Yada> mFiles;
        public override IReadOnlyList<Yada> Files
        {
            get
            {
                if (mFiles == null)
                {
                    mFiles = new List<Yada>();
                    string rootedNamePrefix = (mRootedName.Length <= 1) ? mRootedName : string.Concat(mRootedName, "/");
                    DirectoryInfo diThis = new DirectoryInfo(mPhysicalPath);
                    foreach (FileInfo fi in diThis.EnumerateFiles())
                    {
                        mFiles.Add(new FsFile(fi.FullName, string.Concat(rootedNamePrefix, fi.Name), fi.Name));
                    }
                }
                return mFiles;
            }
        }

        public override bool FileExists(string path)
        {
            return File.Exists(ToPhysicalPath(path));
        }

        public override FileFolder GetFolder(string path)
        {
            FileFolder ff;
            if (!TryGetFolder(path, out ff))
                throw new ArgumentException("Folder not found.");
            return ff;
        }

        public override Yada GetFile(string path)
        {
            Yada file;
            if (!TryGetFile(path, out file))
                throw new ArgumentException("File not found.");
            return file;
        }

        public override bool TryGetFolder(string path, out FileFolder value)
        {
            value = null;
            string physicalPath = ToPhysicalPath(path);
            if (!Directory.Exists(physicalPath)) return false;
            string rootedName = ToRootedName(path);
            string name = ToName(path);
            value = new FsFolder(physicalPath, rootedName, name);
            return true;
        }

        public override bool TryGetFile(string path, out Yada value)
        {
            value = null;
            string physicalPath = ToPhysicalPath(path);
            if (!File.Exists(physicalPath)) return false;
            string rootedName = ToRootedName(path);
            string name = ToName(path);
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
            if (path[0] == '/') throw new ArgumentException("Absolute paths not supported!");
            if (mRootedName.Length == 1) return string.Concat("/", path);
            return string.Concat(mRootedName, "/", path);
        }

        static readonly char[] sSlashes = new char[] { '/', '\\' };
        private string ToName(string path)
        {
            int n = path.LastIndexOfAny(sSlashes);
            return (n >= 0) ? path.Substring(n + 1) : path;
        }

        private class FsFile : Yada
        {

            string mPhysicalPath;
            string mRootedName;
            string mName;

            public FsFile(string physicalPath, string rootedName, string name)
            {
                mPhysicalPath = physicalPath;
                mRootedName = rootedName;
                mName = name;
            }

            public override string Name
            {
                get { return mName; }
            }

            public override string RootedName
            {
                get { return mRootedName; }
            }

            string mExtension = null;
            public override string Extension
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



    /*
                    using (ZipArchive archive2 = ZipFile.OpenRead(@"C:\Users\Brandt\Data\SummativePackages\2015.01.30.MathCatTestPackages.3of3\MATH CAT.Gr11.ContentPackage2.zip"))
            {
                Console.WriteLine("Archive2 has {0} entries.", archive2.Entries.Count);
                ZipArchiveEntry entry2 = archive2.GetEntry("imsmanifest.xml");
                Console.WriteLine("Found entry '{0}'.", entry2.FullName);

                using (StreamReader reader = new StreamReader(entry2.Open()))
                {
                    Console.WriteLine(reader.ReadLine());
                }
            }
*/


    /*
    using (ZipArchive archive1 = ZipFile.OpenRead(@"C:\Users\Brandt\Data\SummativePackages\2015.01.30.MathCatTestPackages.3of3.zip"))
    {
        Console.WriteLine("Archive1 has {0} entries.", archive1.Entries.Count);
        ZipArchiveEntry entry = archive1.GetEntry("MATH CAT.Gr08.ContentPackage.zip");
        Console.WriteLine("Found entry '{0}'.", entry.FullName);

        using (Stream entryStream = entry.Open())
        {
            Console.WriteLine("Opened stream.");
            using (ZipArchive archive2 = new ZipArchive(entryStream, ZipArchiveMode.Read, false))
            {
                Console.WriteLine("Archive2 has {0} entries.", archive1.Entries.Count);
                ZipArchiveEntry entry2 = archive2.GetEntry("imsmanifest.xml");
                Console.WriteLine("Found entry '{0}'.", entry2.FullName);

                using (StreamReader reader = new StreamReader(entry2.Open()))
                {
                    Console.WriteLine(reader.ReadLine());
                }
            }
        }
    }
     */



}
