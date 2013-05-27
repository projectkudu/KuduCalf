using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
namespace SmartSync
{
    public class AtomicFileCounter  : IDisposable
    {
        FileLock _fileLock;
        FileInfo CounterFile { get; set; }
        public AtomicFileCounter(string counterFilePath)
        {
            CounterFile = new FileInfo(counterFilePath);
            var fileLockName = CounterFile.FullName + ".lock";
            _fileLock = new FileLock(fileLockName);
        }

        public bool Init()
        {
            FileStream fs = null;
            try
            {
                Directory.CreateDirectory(CounterFile.DirectoryName);
                _fileLock.Init();
                fs = new FileStream(CounterFile.FullName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using (var wr = new StreamWriter(fs))
                {
                    fs = null;
                    wr.WriteLine("0");
                }
                return true;
            }
            catch (System.IO.IOException ex)
            {
                ThrowIfUnexpected(ex);
                return false;
            }
            finally
            {
                if(fs != null)
                    fs.Dispose();
            }
        }

        public long GetCurrent()
        {
            return long.Parse(File.ReadAllText(CounterFile.FullName));
        }

        public bool IncrementWithAction(long expectedCount, Action act)
        {
            try
            {
                if (!_fileLock.TryAcquire(TimeSpan.FromHours(1)))
                {
                    return false;
                }
                var current = long.Parse(File.ReadAllText(CounterFile.FullName));
                var nextValue = (current + 1).ToString();
                var counterFileNext = new FileInfo(CounterFile.FullName + ".next");
                File.WriteAllText(counterFileNext.FullName, nextValue);
                if (current != expectedCount)
                {
                    return false;
                }
                try
                {
                    act();
                }
                catch (Exception)
                {
                    return false;
                }
                // We assume this is an atomic operation.
                File.Replace(counterFileNext.FullName, CounterFile.FullName, null, true);
                return true;
            }
            catch (Exception ex)
            {
                ThrowIfUnexpected(ex);
                return false;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private void ThrowIfUnexpected(Exception ex)
        {
            if ((ex is IOException) || (ex is UnauthorizedAccessException))
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

        virtual protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_fileLock != null)
                {
                    _fileLock.Dispose();
                    _fileLock = null;
                }
            }
        }
    }
}
