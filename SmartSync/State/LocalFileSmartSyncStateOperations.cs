using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.IO;
namespace SmartSync
{
    public class LocalFileSmartSyncStateOperations : SmartSyncStateOperations, IDisposable
    {

        const string StateFileName = "SmartSyncState.xml";
        private AtomicFileCounter _counter;
        private FileInfo _stateFile;
        public LocalFileSmartSyncStateOperations(DirectoryInfo stateDir)
        {
            var path = Path.Combine(stateDir.FullName, StateFileName);
            _stateFile = new FileInfo(path);
            _counter = new AtomicFileCounter(_stateFile.FullName + ".ver");
        }
        public override bool Initialize(bool force = false)
        {
           var ret =  _counter.Init();
           var mode = force ? FileMode.Create : FileMode.CreateNew;
           if (force || ret)
           {
               using (var fs = new FileStream(_stateFile.FullName, mode, FileAccess.Write))
               {
                   var initState = new SmartSyncState();
                   SmartSyncState.WriteToStream(fs, initState);
               }
           }
           return ret;
        }

        public override bool WaitForStateChange(TimeSpan timeout)
        {
            var fsWatcher = new FileSystemWatcher(_stateFile.DirectoryName, StateFileName);
            var res = fsWatcher.WaitForChanged(WatcherChangeTypes.All, (int) timeout.TotalMilliseconds);
            return !res.TimedOut;
        }

        protected override SmartSyncState ReadState()
        {
            using (var fs = new FileStream(_stateFile.FullName, FileMode.Open, FileAccess.Read))
            {
                var ret = SmartSyncState.ReadFromStream(fs);
                return ret;
            }
        }

        protected override void ReadModifyWriteState(Action<SmartSyncState> act)
        {
          
            int retries = 5;
            int sleepIntervalMs = 1000;
            for (; ; )
            {
                var current = _counter.GetCurrent();
                var success = _counter.IncrementWithAction(current, () =>
                {
                    var state = ReadState();
                    act(state);
                    var nextStateFile = _stateFile.FullName + ".next";
                    using (var fs = new FileStream(nextStateFile, FileMode.Create, FileAccess.Write))
                    {
                        SmartSyncState.WriteToStream(fs, state);
                    }
                    File.Replace(nextStateFile, _stateFile.FullName, null, true);
                });
                if (success)
                {
                    return;
                }
                else
                {
                    if (retries < 0)
                    {
                        throw new System.IO.IOException("Unable to update after several retries.");
                    }
                    System.Threading.Thread.Sleep(sleepIntervalMs);
                    sleepIntervalMs *= 2;
                    retries--;
                }
            }
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
                if (_counter != null)
                {
                    _counter.Dispose();
                    _counter = null;
                }
            }
        }
    }
}
