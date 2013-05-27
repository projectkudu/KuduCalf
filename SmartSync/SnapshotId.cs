using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
namespace SmartSync
{
    /// <summary>
    /// Class used to hide abstract details of repository implementation.
    /// </summary>
    /// <remarks>
    /// Once created this class is immutable there is no need to base64 encode the value again
    /// </remarks>
    public abstract class SnapshotId :  IEquatable<SnapshotId>
    {
        protected SnapshotId(DateTimeOffset dt) : this(dt.UtcTicks)
        {
  
        }
        protected SnapshotId(Guid guid)
        {
            Data = guid.ToByteArray();
        }

        protected SnapshotId(byte[] data)
        {
            Data = data;
        }

        protected SnapshotId(int data)
        {
            Data = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(Data);
            }
        }
        protected SnapshotId(long data)
        {
            Data = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(Data);
            }
        }
        
        protected SnapshotId(string token)
        {
            Data = Convert.FromBase64String(token);
        }
        
        // Data is always in Network order
        private byte[] Data { get; set; }
        private string _token;
        public string Token 
        {
            get
            {
                if (_token == null)
                {
                    _token = Convert.ToBase64String(Data);
                }
                return _token;
            }
        }
        
        protected long AsInt64 { 
            get {
                return IPAddress.NetworkToHostOrder(BitConverter.ToInt64(Data, 0));
            }
        }

        protected int AsInt32 { 
            get {
                return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(Data, 0));
            }
        }
        protected DateTimeOffset AsDateTimeOffset
        {
            get
            {
                return new DateTimeOffset(AsInt64, TimeSpan.Zero);
            }
        }
        protected Guid AsGuid
        {
            get
            {
                return new Guid(Data);
            }
        }

        protected byte[] AsData
        {
            get
            {
                return (byte[])(Data.Clone());
            }
        }
       
        public override int GetHashCode()
        {
            return this.Token.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as SnapshotId;
            return Equals(other);
        }

        public bool Equals(SnapshotId y)
        {
            var x = this;
            if (x != null && y != null)
            {
                if (x.Data.Length != y.Data.Length)
                {
                    return false;
                }
                for (int i = 0; i < x.Data.Length;i++ )
                {
                    if (x.Data[i] != y.Data[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
    }
}
