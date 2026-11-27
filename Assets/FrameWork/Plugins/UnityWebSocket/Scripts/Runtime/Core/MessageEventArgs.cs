using System;
using System.Buffers;
using System.Text;

namespace UnityWebSocket
{
    public class MessageEventArgs : EventArgs
    {
        private byte[] _rawData;
        private byte[] _pooledBuffer;
        private int _pooledLength;
        private string _data;

        internal MessageEventArgs(Opcode opcode, byte[] rawData)
        {
            Opcode = opcode;
            _rawData = rawData;
        }

        private MessageEventArgs(Opcode opcode, byte[] pooledBuffer, int length)
        {
            Opcode = opcode;
            _pooledBuffer = pooledBuffer;
            _pooledLength = length;
        }

        internal MessageEventArgs(Opcode opcode, string data)
        {
            Opcode = opcode;
            _data = data;
        }

        public static MessageEventArgs CreateBinary(byte[] rawData) => new MessageEventArgs(Opcode.Binary, rawData);
        public static MessageEventArgs CreatePooledBinary(byte[] rentedBuffer, int length) =>
            new MessageEventArgs(Opcode.Binary, rentedBuffer, length);
        public static MessageEventArgs CreateText(byte[] rawData) => new MessageEventArgs(Opcode.Text, rawData);
        public static MessageEventArgs CreateText(string data) => new MessageEventArgs(Opcode.Text, data);

        public bool HasPooledSegment => _pooledBuffer != null;
        public byte[] PooledBuffer => _pooledBuffer;
        public int PooledLength => _pooledLength;

        public void ReleasePooledBuffer()
        {
            byte[] buf = _pooledBuffer;
            if (buf == null) return;
            _pooledBuffer = null;
            _pooledLength = 0;
            ArrayPool<byte>.Shared.Return(buf);
        }

        /// <summary>
        /// Gets the opcode for the message.
        /// </summary>
        /// <value>
        /// <see cref="Opcode.Text"/>, <see cref="Opcode.Binary"/>.
        /// </value>
        internal Opcode Opcode { get; private set; }

        /// <summary>
        /// Gets the message data as a <see cref="string"/>.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that represents the message data if its type is
        /// text and if decoding it to a string has successfully done;
        /// otherwise, <see langword="null"/>.
        /// </value>
        public string Data
        {
            get
            {
                SetData();
                return _data;
            }
        }

        /// <summary>
        /// Gets the message data as an array of <see cref="byte"/>.
        /// </summary>
        /// <value>
        /// An array of <see cref="byte"/> that represents the message data.
        /// </value>
        public byte[] RawData
        {
            get
            {
                SetRawData();
                return _rawData;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the message type is binary.
        /// </summary>
        /// <value>
        /// <c>true</c> if the message type is binary; otherwise, <c>false</c>.
        /// </value>
        public bool IsBinary
        {
            get
            {
                return Opcode == Opcode.Binary;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the message type is text.
        /// </summary>
        /// <value>
        /// <c>true</c> if the message type is text; otherwise, <c>false</c>.
        /// </value>
        public bool IsText
        {
            get
            {
                return Opcode == Opcode.Text;
            }
        }

        private void SetData()
        {
            if (_data != null) return;

            if (RawData == null)
            {
                return;
            }

            _data = Encoding.UTF8.GetString(RawData);
        }

        private void SetRawData()
        {
            if (_rawData != null) return;

            if (_pooledBuffer != null)
            {
                byte[] copy = _pooledLength > 0 ? new byte[_pooledLength] : Array.Empty<byte>();
                if (_pooledLength > 0)
                    Buffer.BlockCopy(_pooledBuffer, 0, copy, 0, _pooledLength);
                _rawData = copy;
                return;
            }

            if (_data == null)
            {
                return;
            }

            _rawData = Encoding.UTF8.GetBytes(_data);
        }
    }
}