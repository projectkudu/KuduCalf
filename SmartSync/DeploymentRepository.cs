using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
namespace SmartSync
{
    abstract public class DeploymentRepository
    {
        protected ILogEvent LogEvent { get; set; }
        protected DeploymentRepository()
        {
            LogEvent = DefaultLogger.LogEvent;
        }

        abstract public bool Initialize();
        abstract public SnapshotId CreateSnapshotFromFiles(IEnumerable<KeyValuePair<string, FileInfo>> fileList, string comment);
        abstract public SnapshotId GetLatestSnapshotId();
        abstract public SnapshotId GetPreviousSnapshotId(SnapshotId snapshotId);
        abstract public SnapshotInfo GetSnapshotInfo(SnapshotId snapshotId);
        abstract public IEnumerable<SnapshotItem> GetSnapshotItems(SnapshotId token);
        abstract public SnapshotId FromToken(string tok);

        virtual public SnapshotInfo GetLatestSnapshot()
        {
            var sid  = GetLatestSnapshotId();
            return GetSnapshotInfo(sid);
        }

        virtual public SnapshotInfo GetPreviousSnapshot(SnapshotInfo snapshotInfo)
        {
            var prevId = GetPreviousSnapshotId(snapshotInfo.SnapshotId);
            if (prevId == null)
            {
                return null;
            }
            return GetSnapshotInfo(prevId);
        }

        virtual public IEnumerable<SnapshotInfo> ListAllSnapshots()
        {
            var curr = GetLatestSnapshotId();
            while (curr != null)
            {
                yield return GetSnapshotInfo(curr);
                curr = GetPreviousSnapshotId(curr);
            }
        }

        /// <summary>
        /// List snapshots between first id and last id inclusive.
        /// </summary>
        /// <param name="firstId">A more recently created snapshot.</param>
        /// <param name="lastId">An id older than first Id</param>
        /// <returns>Empty set on error</returns>
        virtual public IEnumerable<SnapshotInfo> ListSnapshotsBetween(SnapshotId firstId, SnapshotId lastId)
        {
            List<SnapshotInfo> ret = new List<SnapshotInfo>();
            var curr = GetLatestSnapshotId();
            while (curr != null && !curr.Equals(firstId))
            {
                curr = GetPreviousSnapshotId(curr);
            }
            // ran out of ids
            if (curr == null)
            {
                LogEvent.SnapshotIdNotFound(firstId);
                // on error case return an empty enumeration.
                ret.Clear();
                return ret;
            }
            else
            {
                // add first id
                ret.Add(GetSnapshotInfo(curr));
            }
            // if first is equal to last we are done.
            if (firstId.Equals(lastId))
            {
                return ret;
            }
            // keep adding till we hit last. 
            while (curr != null && !curr.Equals(lastId))
            {
                ret.Add(GetSnapshotInfo(curr));
                curr = GetPreviousSnapshotId(curr);
            }
            // ran out of ids.
            if (curr == null)
            {
                LogEvent.SnapshotIdNotFound(lastId);
                // on error case return an empty enumeration.
                ret.Clear();
                return ret;
            }
            else
            {
                // add last id
                ret.Add(GetSnapshotInfo(curr));
            }
            return ret;
        }

        virtual public IEnumerable<SnapshotItem> GetAddedItemsBetweenSnapshots(SnapshotId currentId, SnapshotId targetId)
        {
            // just swap the order to get additions.
            return GetDeletedItemsBetweenSnapshots(targetId, currentId);
        }

        virtual public IEnumerable<SnapshotItem> GetDeletedItemsBetweenSnapshots(SnapshotId currentId, SnapshotId targetId)
        {
            //TODO: Allow for case sensitive compare if we ever port to Linux
            HashSet<string> newPaths = new  HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in GetSnapshotItems(targetId))
            {
                newPaths.Add(item.RelativePath);
            }
            foreach (var item in GetSnapshotItems(currentId))
            {
                if (!newPaths.Contains(item.RelativePath))
                {
                    // this is missing in the newer snapshot, so it is a deletion.
                    yield return item;
                }
            }
        }

        virtual public SnapshotId CreateSnapshotFromDirectory(DirectoryInfo dirRoot, string comment, Func<FileSystemInfo,bool> filter = null)
        {
            if (filter == null)
            {
                filter = ignored => true;
            }
            var files = ListFilesInDirectory(dirRoot, dirRoot, filter);
            return CreateSnapshotFromFiles(files, comment);
        }

        virtual public void UpdateDirectoryFromSnapshot(SnapshotId currentId, SnapshotId targetId, DirectoryInfo dirRoot)
        {
            if (currentId.Equals(targetId))
            {
                return;
            }
            PopulateDirectoryFromSnapshot(targetId, dirRoot);
            ProcessDeletionsBetweenSnapshot(currentId, targetId, dirRoot);
        }

        virtual public void ProcessDeletionsBetweenSnapshot(SnapshotId currentId, SnapshotId targetId, DirectoryInfo dirRoot)
        {
            if (currentId.Equals(targetId))
            {
                return;
            }
            var deletions = GetDeletedItemsBetweenSnapshots(currentId, targetId);
            foreach (var item in deletions)
            {
                var fullPath = SafeCombinePath(dirRoot, item.RelativePath);
                if (fullPath == null)
                {
                    LogEvent.DeleteOfItemSkipedBecauseOfError(item.RelativePath);
                }
                else
                {
                    TryDeleteFile(fullPath);
                }
            }
        }

        /// <summary>
        /// Copies files from snapshot to dirRoot.
        /// </summary>
        /// <param name="snapshotId">The snapshot id</param>
        /// <param name="dirRoot">Root to extract</param>
        /// <remarks>
        /// Doesn't delete any data, only adds or modifies data under dirRoot. 
        /// </remarks>
        virtual public void PopulateDirectoryFromSnapshot(SnapshotId snapshotId, DirectoryInfo dirRoot)
        {
            if (snapshotId == null)
            {
                throw new ArgumentNullException("snapshotId");
            }
            if (dirRoot == null)
            {
                throw new ArgumentNullException("dirRoot");
            }
            var snapshotItems = GetSnapshotItems(snapshotId);
            Parallel.ForEach(snapshotItems, item =>
            {
                var fullPath = SafeCombinePath(dirRoot, item.RelativePath);
                if (fullPath == null)
                {
                    LogEvent.UpdateOfItemSkipedBecauseOfError(item.RelativePath);
                   
                } else {
                    try
                    {
                        var itemDir = Path.GetDirectoryName(fullPath);
                        Directory.CreateDirectory(itemDir);
                        using (FileStream fs = new FileStream(fullPath, FileMode.Create))
                        {
                            item.ReadContent(content =>
                                {
                                    content.CopyTo(fs);
                                    content.Close();
                                });
                            fs.Close();
                        }
                        File.SetLastWriteTimeUtc(fullPath, item.LastModifiedTime.UtcDateTime);
                        LogEvent.ExtractedSnapshotContentTo(fullPath, item.Size, item.LastModifiedTime);
                       
                    }
                    catch (Exception ex)
                    {
                        LogEvent.UnexpectedExceptionDuringPopulate(fullPath, ex);
                    }
                }
            });
        }

        private IEnumerable<KeyValuePair<string, FileInfo>> ListFilesInDirectory(DirectoryInfo dirRoot, DirectoryInfo curDir, Func<FileSystemInfo,bool> filter)
        {
            var rootFullPath = dirRoot.FullName;
            var items = curDir.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly).Where(filter);
            foreach (var item in items)
            {
                var relPath = item.FullName.Substring(rootFullPath.Length).TrimStart(Path.DirectorySeparatorChar);
                var file = item as FileInfo;
                var subDir = item as DirectoryInfo;
                if (file != null)
                {
                    yield return new KeyValuePair<string, FileInfo>(relPath, file);
                }
                else if (subDir != null)
                {
                    foreach (var kv in ListFilesInDirectory(dirRoot, subDir, filter))
                    {
                        yield return kv;
                    }
                }
            }
        }
        /// <summary>
        /// This does a path concatenation but verifies that the resulting file path is a results in a path underneath the root.
        /// This is a security best practice to avoid folks modifying files by creating relative paths such as "foo\..\..\bar"
        /// </summary>
        /// <param name="dirInfo"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        protected string SafeCombinePath(DirectoryInfo dirInfo,string path)
        {
           var fullPath = Path.GetFullPath(Path.Combine(dirInfo.FullName, path));
           if (!fullPath.StartsWith(dirInfo.FullName))
           {
               LogEvent.CombinedPathNotUnderExpectedDirectory(dirInfo.FullName, path);
               return null;
           }
           return fullPath;
        }
        protected bool TryDeleteFile(string path)
        {
            var retries = 0;
            var fullPath = Path.GetFullPath(path);
            for (; ; )
            {
                try
                {
                    //TODO: more error handling
                    File.Delete(fullPath);
                    LogEvent.DeletedFile(fullPath);
                    return true;
                }
                catch (IOException )
                {
                    // TODO: log error
                    retries++;
                    if (retries > 5)
                    {
                        // give up
                        return false;
                    }
                    // wait and try again
                    System.Threading.Thread.Sleep(1);
                }
            }
        }
        // try really hard to copy a file.
        // returns true only if a file was copied.
        protected bool TryCopyFile(string src,string dst, bool logCopyFiles = true)
        {
            var retries = 0;
            var srcInfo = new FileInfo(src);
            var dstInfo = new FileInfo(dst);
            var srcPath = srcInfo.FullName;
            var dstPath = dstInfo.FullName;
            if (srcPath.Equals(dstPath, StringComparison.OrdinalIgnoreCase))
            {
                // nothing to do.
                return false;
            }

            if (dstInfo.Exists && srcInfo.Length == dstInfo.Length && dstInfo.LastWriteTimeUtc >= srcInfo.LastWriteTimeUtc)
            {
                //TODO: add a flag to force a copy
                // nothing to do.
                LogEvent.SkipFileCopyNoChange(src, dst);
                return false;
            }
            for(;;)
            {
                try
                {
             
                    File.Copy(src, dst, true);
                    if (logCopyFiles)
                    {
                        LogEvent.CopiedFile(src, dst);
                    }
                    return true;
                }
                catch (DirectoryNotFoundException)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    // try again
                }
                catch (UnauthorizedAccessException)
                {
                    // clear read only flag
                    var currAttrib = File.GetAttributes(dst);
                    if (!currAttrib.HasFlag(FileAttributes.ReadOnly))
                    {
                        // TODO:log error
                        // give up
                        return false;
                    }
                    File.SetAttributes(dst, currAttrib & ~FileAttributes.ReadOnly);
                    // try again
                }
                catch (IOException)
                {
                    // TODO: log error
                    retries++;
                    if (retries > 5)
                    {
                        // give up
                        return false;
                    }
                    // wait and try again
                    System.Threading.Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    LogEvent.UnexpectedExceptionDuringFileCopy(src,dst ,ex);
                    return false;
                }
            }
        }
    }
}
