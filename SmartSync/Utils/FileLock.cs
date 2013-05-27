using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
namespace SmartSync
{
    public class FileLock  : IDisposable
    {
        FileInfo _lockFile;
        FileStream _lockStrm;
        public FileLock(string lockFile)
        {
            _lockFile = new FileInfo(lockFile);
            
        }

        public bool Init()
        { 
            try
            {
                Directory.CreateDirectory(_lockFile.DirectoryName);
                // make sure we can create and delete a lock file.
                var fs = new FileStream(_lockFile.FullName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                {
                    fs.Close();
                }
                _lockFile.Delete();
                return true;
            }
            catch (Exception ex)
            {
                ThrowIfUnexpected(ex);
                return false;
            }
        }

        public bool Release()
        {
            if (_lockStrm != null)
            {
                _lockStrm.Close();
                DeleteFileSafe();
                return true;
            }
            return false;
        }

        public bool TryAcquire(TimeSpan timeout)
        {
            var stopWatch = Stopwatch.StartNew();
            int sleepMs = 1000;
            for (; ; )
            {
                try
                {
                    _lockStrm = new FileStream(_lockFile.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete);
                }
                catch (Exception ex)
                {
                    ThrowIfUnexpected(ex);
                    if (stopWatch.Elapsed > timeout)
                    {
                        return false;
                    }
                    System.Threading.Thread.Sleep(sleepMs);
                    sleepMs *= 2;
                }
                return true;
            }
        }
       
        private void DeleteFileSafe()
        {
            try
            {
                _lockFile.Delete();
            }
            catch (Exception ex)
            {
                ThrowIfUnexpected(ex);
            }
        }

        private void ThrowIfUnexpected(Exception ex)
        {
            if (!(ex is IOException) && !(ex is UnauthorizedAccessException))
            {
                return;
            }
            throw ex;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_lockStrm != null)
                {
                    _lockStrm.Dispose();
                    _lockStrm = null;
                }
            }
        }
    }
}
