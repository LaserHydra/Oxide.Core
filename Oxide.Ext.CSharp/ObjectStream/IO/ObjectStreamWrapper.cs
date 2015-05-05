﻿using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;

namespace ObjectStream.IO
{
    public class ObjectStreamWrapper<TReadWrite> : ObjectStreamWrapper<TReadWrite, TReadWrite>
        where TReadWrite : class
    {
        public ObjectStreamWrapper(Stream inStream, Stream outStream) : base(inStream, outStream)
        {
        }
    }

    public class ObjectStreamWrapper<TRead, TWrite>
        where TRead : class
        where TWrite : class
    {
        private readonly BinaryFormatter _binaryFormatter = new BinaryFormatter { Binder = new BindChanger() };
        private readonly Stream _inStream;
        private readonly Stream _outStream;

        private bool _run = true;

        public ObjectStreamWrapper(Stream inStream, Stream outStream)
        {
            _inStream = inStream;
            _outStream = outStream;
        }

        public bool CanRead
        {
            get { return _run && _inStream.CanRead; }
        }

        public bool CanWrite
        {
            get { return _run && _outStream.CanWrite; }
        }

        public void Close()
        {
            _run = false;
            _inStream.Close();
            _outStream.Close();
        }

        public TRead ReadObject()
        {
            var len = ReadLength();
            return len == 0 ? default(TRead) : ReadObject(len);
        }

        #region Private stream readers

        private int ReadLength()
        {
            const int lensize = sizeof(int);
            var lenbuf = new byte[lensize];
            var bytesRead = _inStream.Read(lenbuf, 0, lensize);
            if (bytesRead == 0)
            {
                return 0;
            }
            if (bytesRead != lensize)
                throw new IOException(string.Format("Expected {0} bytes but read {1}", lensize, bytesRead));
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenbuf, 0));
        }

        private TRead ReadObject(int len)
        {
            var data = new byte[len];
            int count;
            int sum = 0;
            while ((count = _inStream.Read(data, sum, len - sum)) > 0)
                sum += count;
            using (var memoryStream = new MemoryStream(data))
            {
                return (TRead)_binaryFormatter.Deserialize(memoryStream);
            }
        }

        #endregion

        public void WriteObject(TWrite obj)
        {
            var data = Serialize(obj);
            WriteLength(data.Length);
            WriteObject(data);
            Flush();
        }

        #region Private stream writers

        private byte[] Serialize(TWrite obj)
        {
            using (var memoryStream = new MemoryStream())
            {
                _binaryFormatter.Serialize(memoryStream, obj);
                return memoryStream.ToArray();
            }
        }

        private void WriteLength(int len)
        {
            var lenbuf = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(len));
            _outStream.Write(lenbuf, 0, lenbuf.Length);
        }

        private void WriteObject(byte[] data)
        {
            _outStream.Write(data, 0, data.Length);
        }

        private void Flush()
        {
            _outStream.Flush();
        }

        #endregion
    }
}