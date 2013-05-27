using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartSync
{
    public class SnapshotItem
    {
        private Action<Action<Stream>> _readContent;
        public SnapshotItem(string relPath, long size, DateTimeOffset lastModified, Action<Action<Stream>> readContent)
        {
            RelativePath = relPath;
            Size = size;
            LastModifiedTime = lastModified;
            _readContent = readContent;
            // Sanity checking
            if (Path.IsPathRooted(relPath))
            {
                throw new ArgumentException("Path is not a relative path.", "relPath");
            }
            // TODO: better check
            if (relPath.StartsWith("..\\") || relPath.Contains("\\..\\"))
            {
                throw new ArgumentException("Relative relative path '"+ relPath  +"' can not contain \"..\"", "relPath");
            }
        }
        /// <summary>
        /// Relative path in native OS format
        /// </summary>
        public string RelativePath { get; private set; }
        /// <summary>
        /// Size of the item.
        /// </summary>
        public long Size { get; private set; }
        /// <summary>
        /// Last modified time of the item.
        /// </summary>
        public DateTimeOffset LastModifiedTime { get; private set; }
        /// <summary>
        /// Returns a readonly stream to the content of the item.
        /// </summary>
        /// <returns></returns>
        public void ReadContent(Action<Stream> act)
        {
            _readContent(act);
        }
    }
}
