﻿using MediaBrowser.Model.Extensions;
using MediaBrowser.Common.IO;
using MediaBrowser.Model.Logging;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Common.Implementations.IO
{
    /// <summary>
    /// Class CommonFileSystem
    /// </summary>
    public class CommonFileSystem : IFileSystem
    {
        protected ILogger Logger;

        private readonly bool _supportsAsyncFileStreams;
        private char[] _invalidFileNameChars;

        public CommonFileSystem(ILogger logger, bool supportsAsyncFileStreams, bool usePresetInvalidFileNameChars)
        {
            Logger = logger;
            _supportsAsyncFileStreams = supportsAsyncFileStreams;

            SetInvalidFileNameChars(usePresetInvalidFileNameChars);
        }

        private void SetInvalidFileNameChars(bool usePresetInvalidFileNameChars)
        {
            // GetInvalidFileNameChars is less restrictive in Linux/Mac than Windows, this mimic Windows behavior for mono under Linux/Mac.

            if (usePresetInvalidFileNameChars)
            {
                _invalidFileNameChars = new char[41] { '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07',
            '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F', '\x10', '\x11', '\x12',
            '\x13', '\x14', '\x15', '\x16', '\x17', '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D',
            '\x1E', '\x1F', '\x22', '\x3C', '\x3E', '\x7C', ':', '*', '?', '\\', '/' };
            }
            else
            {
                _invalidFileNameChars = Path.GetInvalidFileNameChars();
            }
        }

        /// <summary>
        /// Determines whether the specified filename is shortcut.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns><c>true</c> if the specified filename is shortcut; otherwise, <c>false</c>.</returns>
        /// <exception cref="System.ArgumentNullException">filename</exception>
        public virtual bool IsShortcut(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException("filename");
            }

            var extension = Path.GetExtension(filename);

            return string.Equals(extension, ".mblink", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the shortcut.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">filename</exception>
        public virtual string ResolveShortcut(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException("filename");
            }

            if (string.Equals(Path.GetExtension(filename), ".mblink", StringComparison.OrdinalIgnoreCase))
            {
                var path = ReadAllText(filename);

                return NormalizePath(path);
            }

            return null;
        }

        /// <summary>
        /// Creates the shortcut.
        /// </summary>
        /// <param name="shortcutPath">The shortcut path.</param>
        /// <param name="target">The target.</param>
        /// <exception cref="System.ArgumentNullException">
        /// shortcutPath
        /// or
        /// target
        /// </exception>
        public void CreateShortcut(string shortcutPath, string target)
        {
            if (string.IsNullOrEmpty(shortcutPath))
            {
                throw new ArgumentNullException("shortcutPath");
            }

            if (string.IsNullOrEmpty(target))
            {
                throw new ArgumentNullException("target");
            }

			File.WriteAllText(shortcutPath, target);
        }

        /// <summary>
        /// Gets the file system info.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>FileSystemInfo.</returns>
        public FileSystemInfo GetFileSystemInfo(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            // Take a guess to try and avoid two file system hits, but we'll double-check by calling Exists
            if (Path.HasExtension(path))
            {
                var fileInfo = new FileInfo(path);

                if (fileInfo.Exists)
                {
                    return fileInfo;
                }

                return new DirectoryInfo(path);
            }
            else
            {
                var fileInfo = new DirectoryInfo(path);

                if (fileInfo.Exists)
                {
                    return fileInfo;
                }

                return new FileInfo(path);
            }
        }

        /// <summary>
        /// The space char
        /// </summary>
        private const char SpaceChar = ' ';

        /// <summary>
        /// Takes a filename and removes invalid characters
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">filename</exception>
        public string GetValidFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException("filename");
            }

            var builder = new StringBuilder(filename);

            foreach (var c in _invalidFileNameChars)
            {
                builder = builder.Replace(c, SpaceChar);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Gets the creation time UTC.
        /// </summary>
        /// <param name="info">The info.</param>
        /// <returns>DateTime.</returns>
        public DateTime GetCreationTimeUtc(FileSystemInfo info)
        {
            // This could throw an error on some file systems that have dates out of range
            try
            {
                return info.CreationTimeUtc;
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error determining CreationTimeUtc for {0}", ex, info.FullName);
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Gets the creation time UTC.
        /// </summary>
        /// <param name="info">The info.</param>
        /// <returns>DateTime.</returns>
        public DateTime GetLastWriteTimeUtc(FileSystemInfo info)
        {
            // This could throw an error on some file systems that have dates out of range
            try
            {
                return info.LastWriteTimeUtc;
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error determining LastAccessTimeUtc for {0}", ex, info.FullName);
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Gets the last write time UTC.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>DateTime.</returns>
        public DateTime GetLastWriteTimeUtc(string path)
        {
            return GetLastWriteTimeUtc(GetFileSystemInfo(path));
        }

        /// <summary>
        /// Gets the file stream.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="mode">The mode.</param>
        /// <param name="access">The access.</param>
        /// <param name="share">The share.</param>
        /// <param name="isAsync">if set to <c>true</c> [is asynchronous].</param>
        /// <returns>FileStream.</returns>
        public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share, bool isAsync = false)
        {
            if (_supportsAsyncFileStreams && isAsync)
            {
                return new FileStream(path, mode, access, share, StreamDefaults.DefaultFileStreamBufferSize, true);
            }

            return new FileStream(path, mode, access, share, StreamDefaults.DefaultFileStreamBufferSize);
        }

        /// <summary>
        /// Swaps the files.
        /// </summary>
        /// <param name="file1">The file1.</param>
        /// <param name="file2">The file2.</param>
        public void SwapFiles(string file1, string file2)
        {
            if (string.IsNullOrEmpty(file1))
            {
                throw new ArgumentNullException("file1");
            }

            if (string.IsNullOrEmpty(file2))
            {
                throw new ArgumentNullException("file2");
            }

            var temp1 = Path.GetTempFileName();
            var temp2 = Path.GetTempFileName();

            // Copying over will fail against hidden files
            RemoveHiddenAttribute(file1);
            RemoveHiddenAttribute(file2);

			CopyFile(file1, temp1, true);
			CopyFile(file2, temp2, true);

			CopyFile(temp1, file2, true);
            CopyFile(temp2, file1, true);

            DeleteFile(temp1);
            DeleteFile(temp2);
        }

        /// <summary>
        /// Removes the hidden attribute.
        /// </summary>
        /// <param name="path">The path.</param>
        private void RemoveHiddenAttribute(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            var currentFile = new FileInfo(path);

            // This will fail if the file is hidden
            if (currentFile.Exists)
            {
                if ((currentFile.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                {
                    currentFile.Attributes &= ~FileAttributes.Hidden;
                }
            }
        }

        public bool ContainsSubPath(string parentPath, string path)
        {
            if (string.IsNullOrEmpty(parentPath))
            {
                throw new ArgumentNullException("parentPath");
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            return path.IndexOf(parentPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) != -1;
        }

        public bool IsRootPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            var parent = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(parent))
            {
                return false;
            }

            return true;
        }

        public string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            if (path.EndsWith(":\\", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return path.TrimEnd(Path.DirectorySeparatorChar);
        }

        public string SubstitutePath(string path, string from, string to)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException("path");
            }
            if (string.IsNullOrWhiteSpace(from))
            {
                throw new ArgumentNullException("from");
            }
            if (string.IsNullOrWhiteSpace(to))
            {
                throw new ArgumentNullException("to");
            }

            var newPath = path.Replace(from, to, StringComparison.OrdinalIgnoreCase);

            if (!string.Equals(newPath, path))
            {
                if (to.IndexOf('/') != -1)
                {
                    newPath = newPath.Replace('\\', '/');
                }
                else
                {
                    newPath = newPath.Replace('/', '\\');
                }
            }

            return newPath;
        }

        public string GetFileNameWithoutExtension(FileSystemInfo info)
        {
            if (info is DirectoryInfo)
            {
                return info.Name;
            }

            return Path.GetFileNameWithoutExtension(info.FullName);
        }

        public string GetFileNameWithoutExtension(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        public bool IsPathFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException("path");
            }

            // Cannot use Path.IsPathRooted because it returns false under mono when using windows-based paths, e.g. C:\\

            if (path.IndexOf("://", StringComparison.OrdinalIgnoreCase) != -1 &&
                !path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;

            //return Path.IsPathRooted(path);
        }

        public void DeleteFile(string path)
        {
            File.Delete(path);
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            Directory.Delete(path, recursive);
		}

		public void CreateDirectory(string path)
		{
			Directory.CreateDirectory(path);
		}
			
		public IEnumerable<DirectoryInfo> GetDirectories(string path, bool recursive = false) 
		{
			var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

			return new DirectoryInfo (path).EnumerateDirectories("*", searchOption);
		}

		public IEnumerable<FileInfo> GetFiles(string path, bool recursive = false) 
		{
			var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

			return new DirectoryInfo (path).EnumerateFiles("*", searchOption);
		}

		public IEnumerable<FileSystemInfo> GetFileSystemEntries(string path, bool recursive = false) 
		{
			var directoryInfo = new DirectoryInfo (path);
			var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

			return directoryInfo.EnumerateDirectories("*", searchOption)
							.Concat<FileSystemInfo>(directoryInfo.EnumerateFiles("*", searchOption));
		}

        public Stream OpenRead(string path)
        {
            return File.OpenRead(path);
        }

        public void CopyFile(string source, string target, bool overwrite)
        {
            File.Copy(source, target, overwrite);
        }

        public void MoveFile(string source, string target)
        {
            File.Move(source, target);
        }

        public void MoveDirectory(string source, string target)
        {
            Directory.Move(source, target);
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public void WriteAllText(string path, string text, Encoding encoding)
        {
            File.WriteAllText(path, text, encoding);
        }

        public void WriteAllText(string path, string text)
        {
            File.WriteAllText(path, text);
        }

        public string ReadAllText(string path, Encoding encoding)
        {
            return File.ReadAllText(path, encoding);
        }

        public IEnumerable<string> GetDirectoryPaths(string path, bool recursive = false)
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.EnumerateDirectories(path, "*", searchOption);
        }

        public IEnumerable<string> GetFilePaths(string path, bool recursive = false)
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.EnumerateFiles(path, "*", searchOption);
        }

        public IEnumerable<string> GetFileSystemEntryPaths(string path, bool recursive = false)
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.EnumerateFileSystemEntries(path, "*", searchOption);
        }
    }
}
