using System;
using System.IO;

namespace QvGamsConnector
{
    public static class PathUtils
    {
        /// <summary>
        /// Removes the root from a path
        /// </summary>
        /// <param name="root"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string RemoveRootPath(string root, string path)
        {
            int place = path.IndexOf(root);
            string cleanPath = path.Remove(place, root.Length);
            cleanPath = cleanPath.StartsWith("\\") ? cleanPath.Substring(1) : cleanPath;
            return cleanPath;
        }

        /// <summary>
        /// Checks if a path is just a drive name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsDrive(string path)
        {
            DirectoryInfo info = new DirectoryInfo(path);
            return info.Exists && info.Name == info.Root.Name;
        }

        /// <summary>
        /// Checks if the child is a sub-path of the root
        /// </summary>
        /// <param name="root"></param>
        /// <param name="child"></param>
        /// <returns></returns>
        public static bool IsBaseOf(string root, string child)
        {
            FileSystemInfo rootInfo = new DirectoryInfo(root);
            FileSystemInfo childInfo;

            if(File.Exists(child))
            {
                childInfo = new FileInfo(child);
            } else if(Directory.Exists(child))
            {
                childInfo = new FileInfo(child);
            } else
            {
                // the path does not exist
                return false;
            }

            var directoryPath = EndsWithSeparator(new Uri(childInfo.FullName).AbsolutePath);
            var rootPath = EndsWithSeparator(new Uri(rootInfo.FullName).AbsolutePath);
            return directoryPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the path ends with a separator
        /// </summary>
        /// <param name="absolutePath"></param>
        /// <returns></returns>
        private static string EndsWithSeparator(string absolutePath)
        {
            return absolutePath?.TrimEnd('/', '\\') + "/";
        }
    }
}
