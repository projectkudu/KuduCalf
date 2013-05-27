using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartSync
{
    public class SnapshotInfo
    {
        public SnapshotInfo(string comment, DateTimeOffset time, SnapshotId snapshotId)
        {
            Comment = comment;
            Time = time;
            SnapshotId = snapshotId;
        }
        public string Comment { get; private set; }
        public DateTimeOffset Time { get; private set; }
        public SnapshotId SnapshotId { get; private set; }
    }
}
