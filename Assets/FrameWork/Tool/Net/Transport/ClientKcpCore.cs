#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Collections.Generic;

namespace FrameWork.Net.Transport
{
    /// <summary>
    /// 纯 C# 标准 KCP ARQ 协议核心（与服务端 <c>WebSocketDemo.Transport.KcpCore</c> 线协议完全一致）。
    /// 针对客户端热路径优化：Segment 与分片缓冲对象池复用、Check() 精确唤醒调度。
    /// </summary>
    internal sealed class ClientKcpCore
    {
        public const int Overhead = 24;
        private const uint RtoNdl = 30;
        private const uint RtoMin = 100;
        private const uint RtoDef = 200;
        private const uint RtoMax = 60000;

        private const byte CmdPush = 81;
        private const byte CmdAck = 82;
        private const byte CmdWask = 83;
        private const byte CmdWins = 84;

        private const uint AskSend = 1;
        private const uint AskTell = 2;

        private const uint WndSndDefault = 32;
        private const uint WndRcvDefault = 128;
        private const uint MtuDefault = 1200;
        private const uint IntervalDefault = 100;
        private const uint DeadLinkDefault = 20;
        private const uint ProbeInit = 7000;
        private const uint ProbeLimit = 120000;
        private const int MaxPooledSegments = 256;
        private const int SmallBufSize = 256;
        private const int MtuBufSize = 1400;

        private sealed class Segment
        {
            public uint Conv;
            public byte Cmd;
            public byte Frg;
            public ushort Wnd;
            public uint Ts;
            public uint Sn;
            public uint Una;
            public uint ResendTs;
            public uint Rto;
            public uint FastAck;
            public uint Xmit;
            public byte[] Data;
            public int DataLength;
        }

        private readonly uint _conv;
        private uint _mtu = MtuDefault;
        private uint _mss = MtuDefault - Overhead;
        private int _state;

        private uint _sndUna;
        private uint _sndNxt;
        private uint _rcvNxt;

        private uint _ssthresh = 2;
        private int _rxRttval;
        private int _rxSrtt;
        private uint _rxRto = RtoDef;
        private uint _rxMinRto = RtoMin;

        private uint _sndWnd = WndSndDefault;
        private uint _rcvWnd = WndRcvDefault;
        private uint _rmtWnd = WndRcvDefault;
        private uint _cwnd;
        private uint _probe;

        private uint _current;
        private uint _interval = IntervalDefault;
        private uint _tsFlush = IntervalDefault;
        private uint _xmit;

        private bool _nodelay;
        private bool _updated;
        private uint _tsProbe;
        private uint _probeWait;
        private uint _deadLink = DeadLinkDefault;
        private uint _incr;

        private readonly LinkedList<Segment> _sndQueue = new LinkedList<Segment>();
        private readonly LinkedList<Segment> _rcvQueue = new LinkedList<Segment>();
        private readonly LinkedList<Segment> _sndBuf = new LinkedList<Segment>();
        private readonly LinkedList<Segment> _rcvBuf = new LinkedList<Segment>();
        private readonly List<(uint Sn, uint Ts)> _ackList = new List<(uint Sn, uint Ts)>();
        private readonly Stack<Segment> _segPool = new Stack<Segment>();
        private readonly Stack<byte[]> _smallBufPool = new Stack<byte[]>();
        private readonly Stack<byte[]> _mtuBufPool = new Stack<byte[]>();

        private byte[] _buffer;
        private int _fastResend;
        private bool _nocwnd;
        private readonly Action<byte[], int> _output;

        public uint Conv => _conv;
        public bool IsDead => _state != 0;
        public int WaitSnd => _sndBuf.Count + _sndQueue.Count;

        public ClientKcpCore(uint conv, Action<byte[], int> output)
        {
            _conv = conv;
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _buffer = new byte[(_mtu + Overhead) * 3];
        }

        public static uint ReadConv(byte[] data, int offset, int length)
        {
            return length >= 4 ? ClientTransportProtocol.ReadUInt32LE(data, offset) : 0;
        }

        public void SetNoDelay(int nodelay, int interval, int resend, int nc)
        {
            if (nodelay >= 0)
            {
                _nodelay = nodelay != 0;
                _rxMinRto = _nodelay ? RtoNdl : RtoMin;
            }
            if (interval >= 0)
            {
                if (interval > 5000) interval = 5000;
                else if (interval < 10) interval = 10;
                _interval = (uint)interval;
            }
            if (resend >= 0) _fastResend = resend;
            if (nc >= 0) _nocwnd = nc != 0;
        }

        public void SetWndSize(int sndWnd, int rcvWnd)
        {
            if (sndWnd > 0) _sndWnd = (uint)sndWnd;
            if (rcvWnd > 0) _rcvWnd = Math.Max((uint)rcvWnd, WndRcvDefault);
        }

        public void SetMtu(int mtu)
        {
            if (mtu < 50 || mtu < Overhead)
                throw new ArgumentOutOfRangeException(nameof(mtu));
            _mtu = (uint)mtu;
            _mss = _mtu - Overhead;
            _buffer = new byte[(_mtu + Overhead) * 3];
        }

        public int PeekSize()
        {
            if (_rcvQueue.Count == 0) return -1;
            var head = _rcvQueue.First.Value;
            if (head.Frg == 0) return head.DataLength;
            if (_rcvQueue.Count < head.Frg + 1) return -1;

            int length = 0;
            for (var node = _rcvQueue.First; node != null; node = node.Next)
            {
                length += node.Value.DataLength;
                if (node.Value.Frg == 0) break;
            }
            return length;
        }

        public int Recv(byte[] buffer)
        {
            if (_rcvQueue.Count == 0) return -1;
            int peekSize = PeekSize();
            if (peekSize < 0) return -2;
            if (peekSize > buffer.Length) return -3;

            bool recover = _rcvQueue.Count >= _rcvWnd;
            int offset = 0;
            while (_rcvQueue.Count > 0)
            {
                var seg = _rcvQueue.First.Value;
                _rcvQueue.RemoveFirst();
                int segLen = seg.DataLength;
                byte frg = seg.Frg;
                if (segLen > 0)
                {
                    Buffer.BlockCopy(seg.Data, 0, buffer, offset, segLen);
                    offset += segLen;
                }
                ReturnSegment(seg);
                if (frg == 0) break;
            }

            MoveRcvBufToQueue();

            if (_rcvQueue.Count < _rcvWnd && recover)
                _probe |= AskTell;

            return offset;
        }

        public int Send(byte[] data, int offset, int length)
        {
            if (data == null || length <= 0) return -1;
            int count = length <= _mss ? 1 : (int)((length + _mss - 1) / _mss);
            if (count >= 255) return -2;
            if (count == 0) count = 1;

            int pos = 0;
            for (int i = 0; i < count; i++)
            {
                int size = Math.Min(length - pos, (int)_mss);
                var seg = RentSegment(size);
                seg.Frg = (byte)(count - i - 1);
                if (size > 0)
                    Buffer.BlockCopy(data, offset + pos, seg.Data, 0, size);
                pos += size;
                _sndQueue.AddLast(seg);
            }
            return 0;
        }

        public int Input(byte[] data, int startOffset, int totalLength)
        {
            uint prevUna = _sndUna;
            uint maxAck = 0;
            uint latestTs = 0;
            bool flag = false;

            if (totalLength < Overhead) return -1;

            int offset = startOffset;
            int end = startOffset + totalLength;
            while (end - offset >= Overhead)
            {
                uint conv = ClientTransportProtocol.ReadUInt32LE(data, offset);
                if (conv != _conv) return -1;

                byte cmd = data[offset + 4];
                byte frg = data[offset + 5];
                ushort wnd = ClientTransportProtocol.ReadUInt16LE(data, offset + 6);
                uint ts = ClientTransportProtocol.ReadUInt32LE(data, offset + 8);
                uint sn = ClientTransportProtocol.ReadUInt32LE(data, offset + 12);
                uint una = ClientTransportProtocol.ReadUInt32LE(data, offset + 16);
                uint len = ClientTransportProtocol.ReadUInt32LE(data, offset + 20);
                offset += Overhead;

                if (end - offset < len) return -2;
                if (cmd != CmdPush && cmd != CmdAck && cmd != CmdWask && cmd != CmdWins) return -3;

                _rmtWnd = wnd;
                ParseUna(una);
                ShrinkBuf();

                if (cmd == CmdAck)
                {
                    if (TimeDiff(_current, ts) >= 0)
                        UpdateAck(TimeDiff(_current, ts));
                    ParseAck(sn);
                    ShrinkBuf();
                    if (!flag)
                    {
                        flag = true;
                        maxAck = sn;
                        latestTs = ts;
                    }
                    else if (TimeDiff(sn, maxAck) > 0)
                    {
                        maxAck = sn;
                        latestTs = ts;
                    }
                }
                else if (cmd == CmdPush)
                {
                    if (TimeDiff(sn, _rcvNxt + _rcvWnd) < 0)
                    {
                        _ackList.Add((sn, ts));
                        if (TimeDiff(sn, _rcvNxt) >= 0)
                        {
                            var seg = RentSegment((int)len);
                            seg.Conv = conv;
                            seg.Cmd = cmd;
                            seg.Frg = frg;
                            seg.Wnd = wnd;
                            seg.Ts = ts;
                            seg.Sn = sn;
                            seg.Una = una;
                            if (len > 0)
                                Buffer.BlockCopy(data, offset, seg.Data, 0, (int)len);
                            ParseData(seg);
                        }
                    }
                }
                else if (cmd == CmdWask)
                {
                    _probe |= AskTell;
                }

                offset += (int)len;
            }

            if (flag)
                ParseFastAck(maxAck, latestTs);

            if (TimeDiff(_sndUna, prevUna) > 0 && _cwnd < _rmtWnd)
            {
                uint mss = _mss;
                if (_cwnd < _ssthresh)
                {
                    _cwnd++;
                    _incr += mss;
                }
                else
                {
                    if (_incr < mss) _incr = mss;
                    _incr += (mss * mss) / _incr + (mss / 16);
                    if ((_cwnd + 1) * mss <= _incr)
                        _cwnd = (_incr + mss - 1) / (mss > 0 ? mss : 1);
                }
                if (_cwnd > _rmtWnd)
                {
                    _cwnd = _rmtWnd;
                    _incr = _rmtWnd * mss;
                }
            }

            return 0;
        }

        public void Update(uint current)
        {
            _current = current;
            if (!_updated)
            {
                _updated = true;
                _tsFlush = _current;
            }

            int slap = TimeDiff(_current, _tsFlush);
            if (slap >= 10000 || slap < -10000)
            {
                _tsFlush = _current;
                slap = 0;
            }

                if (slap >= 0)
                {
                    _tsFlush += _interval;
                    if (TimeDiff(_current, _tsFlush) >= 0)
                        _tsFlush = _current + _interval;
                    Flush();
                }
        }

        public uint Check(uint current)
        {
            if (!_updated) return current;

            uint tsFlush = _tsFlush;
            int tmPacket = int.MaxValue;

            if (TimeDiff(current, tsFlush) >= 10000 || TimeDiff(current, tsFlush) < -10000)
                tsFlush = current;

            if (TimeDiff(current, tsFlush) >= 0)
                return current;

            int tmFlush = TimeDiff(tsFlush, current);
            for (var node = _sndBuf.First; node != null; node = node.Next)
            {
                int diff = TimeDiff(node.Value.ResendTs, current);
                if (diff <= 0) return current;
                if (diff < tmPacket) tmPacket = diff;
            }

            int minimal = tmPacket < tmFlush ? tmPacket : tmFlush;
            if (minimal >= _interval) minimal = (int)_interval;
            return current + (uint)minimal;
        }

        public void Clear()
        {
            while (_sndQueue.Count > 0) { var s = _sndQueue.First.Value; _sndQueue.RemoveFirst(); ReturnSegment(s); }
            while (_rcvQueue.Count > 0) { var s = _rcvQueue.First.Value; _rcvQueue.RemoveFirst(); ReturnSegment(s); }
            while (_sndBuf.Count > 0) { var s = _sndBuf.First.Value; _sndBuf.RemoveFirst(); ReturnSegment(s); }
            while (_rcvBuf.Count > 0) { var s = _rcvBuf.First.Value; _rcvBuf.RemoveFirst(); ReturnSegment(s); }
            _ackList.Clear();
        }

        public void Flush()
        {
            if (!_updated) return;

            uint current = _current;
            int offset = 0;
            ushort wndUnused = WndUnused();

            for (int i = 0; i < _ackList.Count; i++)
            {
                if (offset + Overhead > _mtu)
                {
                    _output(_buffer, offset);
                    offset = 0;
                }
                var ack = _ackList[i];
                offset = EncodeHeader(_buffer, offset, _conv, CmdAck, 0, wndUnused, ack.Ts, ack.Sn, _rcvNxt, 0);
            }
            _ackList.Clear();

            if (_rmtWnd == 0)
            {
                if (_probeWait == 0)
                {
                    _probeWait = ProbeInit;
                    _tsProbe = current + _probeWait;
                }
                else if (TimeDiff(current, _tsProbe) >= 0)
                {
                    if (_probeWait < ProbeInit) _probeWait = ProbeInit;
                    _probeWait += _probeWait / 2;
                    if (_probeWait > ProbeLimit) _probeWait = ProbeLimit;
                    _tsProbe = current + _probeWait;
                    _probe |= AskSend;
                }
            }
            else
            {
                _tsProbe = 0;
                _probeWait = 0;
            }

            if ((_probe & AskSend) != 0)
            {
                if (offset + Overhead > _mtu)
                {
                    _output(_buffer, offset);
                    offset = 0;
                }
                offset = EncodeHeader(_buffer, offset, _conv, CmdWask, 0, wndUnused, 0, 0, _rcvNxt, 0);
            }

            if ((_probe & AskTell) != 0)
            {
                if (offset + Overhead > _mtu)
                {
                    _output(_buffer, offset);
                    offset = 0;
                }
                offset = EncodeHeader(_buffer, offset, _conv, CmdWins, 0, wndUnused, 0, 0, _rcvNxt, 0);
            }
            _probe = 0;

            uint cwnd = Math.Min(_sndWnd, _rmtWnd);
            if (!_nocwnd) cwnd = Math.Min(_cwnd, cwnd);

            while (TimeDiff(_sndNxt, _sndUna + cwnd) < 0 && _sndQueue.Count > 0)
            {
                var seg = _sndQueue.First.Value;
                _sndQueue.RemoveFirst();
                seg.Conv = _conv;
                seg.Cmd = CmdPush;
                seg.Wnd = wndUnused;
                seg.Ts = current;
                seg.Sn = _sndNxt++;
                seg.Una = _rcvNxt;
                seg.ResendTs = current;
                seg.Rto = _rxRto;
                seg.FastAck = 0;
                seg.Xmit = 0;
                _sndBuf.AddLast(seg);
            }

            uint resent = _fastResend > 0 ? (uint)_fastResend : uint.MaxValue;
            uint rtoMin = _nodelay ? 0 : (_rxRto >> 3);
            bool change = false;
            bool lost = false;

            for (var node = _sndBuf.First; node != null; node = node.Next)
            {
                var seg = node.Value;
                bool needSend = false;
                if (seg.Xmit == 0)
                {
                    needSend = true;
                    seg.Xmit++;
                    seg.Rto = _rxRto;
                    seg.ResendTs = current + seg.Rto + rtoMin;
                }
                else if (TimeDiff(current, seg.ResendTs) >= 0)
                {
                    needSend = true;
                    seg.Xmit++;
                    _xmit++;
                    if (!_nodelay) seg.Rto += Math.Max(seg.Rto, _rxRto);
                    else seg.Rto += seg.Rto / 2;
                    seg.ResendTs = current + seg.Rto;
                    lost = true;
                }
                else if (seg.FastAck >= resent)
                {
                    needSend = true;
                    seg.Xmit++;
                    seg.FastAck = 0;
                    seg.ResendTs = current + seg.Rto;
                    change = true;
                }

                if (needSend)
                {
                    seg.Ts = current;
                    seg.Wnd = wndUnused;
                    seg.Una = _rcvNxt;

                    int segLen = seg.DataLength;
                    int need = Overhead + segLen;
                    if (offset + need > _mtu && offset > 0)
                    {
                        _output(_buffer, offset);
                        offset = 0;
                    }
                    offset = EncodeHeader(_buffer, offset, seg.Conv, seg.Cmd, seg.Frg, seg.Wnd,
                        seg.Ts, seg.Sn, seg.Una, (uint)segLen);
                    if (segLen > 0)
                    {
                        Buffer.BlockCopy(seg.Data, 0, _buffer, offset, segLen);
                        offset += segLen;
                    }

                    if (seg.Xmit >= _deadLink)
                        _state = -1;
                }
            }

            if (offset > 0)
                _output(_buffer, offset);

            if (change)
            {
                uint inflight = _sndNxt - _sndUna;
                _ssthresh = Math.Max(2, inflight / 2);
                _cwnd = _ssthresh + resent;
                _incr = _cwnd * _mss;
            }
            if (lost)
            {
                _ssthresh = Math.Max(2, cwnd / 2);
                _cwnd = 1;
                _incr = _mss;
            }
            if (_cwnd < 1)
            {
                _cwnd = 1;
                _incr = _mss;
            }
        }

        private void UpdateAck(int rtt)
        {
            if (_rxSrtt == 0)
            {
                _rxSrtt = rtt;
                _rxRttval = rtt / 2;
            }
            else
            {
                int delta = Math.Abs(rtt - _rxSrtt);
                _rxRttval = (3 * _rxRttval + delta) / 4;
                _rxSrtt = (7 * _rxSrtt + rtt) / 8;
                if (_rxSrtt < 1) _rxSrtt = 1;
            }
            int rto = _rxSrtt + Math.Max((int)_interval, 4 * _rxRttval);
            if (rto < (int)_rxMinRto) rto = (int)_rxMinRto;
            else if (rto > (int)RtoMax) rto = (int)RtoMax;
            _rxRto = (uint)rto;
        }

        private void ShrinkBuf()
        {
            _sndUna = _sndBuf.Count > 0 ? _sndBuf.First.Value.Sn : _sndNxt;
        }

        private void ParseAck(uint sn)
        {
            if (TimeDiff(sn, _sndUna) < 0 || TimeDiff(sn, _sndNxt) >= 0) return;
            for (var node = _sndBuf.First; node != null;)
            {
                var next = node.Next;
                if (sn == node.Value.Sn)
                {
                    var seg = node.Value;
                    _sndBuf.Remove(node);
                    ReturnSegment(seg);
                    break;
                }
                if (TimeDiff(sn, node.Value.Sn) < 0) break;
                node = next;
            }
        }

        private void ParseUna(uint una)
        {
            while (_sndBuf.Count > 0 && TimeDiff(una, _sndBuf.First.Value.Sn) > 0)
            {
                var seg = _sndBuf.First.Value;
                _sndBuf.RemoveFirst();
                ReturnSegment(seg);
            }
        }

        private void ParseFastAck(uint sn, uint ts)
        {
            if (TimeDiff(sn, _sndUna) < 0 || TimeDiff(sn, _sndNxt) >= 0) return;
            for (var node = _sndBuf.First; node != null; node = node.Next)
            {
                if (TimeDiff(sn, node.Value.Sn) < 0) break;
                if (sn != node.Value.Sn) node.Value.FastAck++;
            }
        }

        private void ParseData(Segment newSeg)
        {
            uint sn = newSeg.Sn;
            if (TimeDiff(sn, _rcvNxt + _rcvWnd) >= 0 || TimeDiff(sn, _rcvNxt) < 0)
            {
                ReturnSegment(newSeg);
                return;
            }

            bool repeat = false;
            LinkedListNode<Segment> insertAfter = null;
            for (var node = _rcvBuf.Last; node != null; node = node.Previous)
            {
                if (node.Value.Sn == sn)
                {
                    repeat = true;
                    break;
                }
                if (TimeDiff(sn, node.Value.Sn) > 0)
                {
                    insertAfter = node;
                    break;
                }
            }

            if (!repeat)
            {
                if (insertAfter == null) _rcvBuf.AddFirst(newSeg);
                else _rcvBuf.AddAfter(insertAfter, newSeg);
            }
            else
            {
                ReturnSegment(newSeg);
            }

            MoveRcvBufToQueue();
        }

        private Segment RentSegment(int size)
        {
            Segment seg = _segPool.Count > 0 ? _segPool.Pop() : new Segment();
            seg.Conv = 0;
            seg.Cmd = 0;
            seg.Frg = 0;
            seg.Wnd = 0;
            seg.Ts = 0;
            seg.Sn = 0;
            seg.Una = 0;
            seg.ResendTs = 0;
            seg.Rto = 0;
            seg.FastAck = 0;
            seg.Xmit = 0;
            seg.DataLength = size;
            if (size <= 0)
            {
                seg.Data = Array.Empty<byte>();
            }
            else if (size <= SmallBufSize)
            {
                seg.Data = _smallBufPool.Count > 0 ? _smallBufPool.Pop() : new byte[SmallBufSize];
            }
            else if (size <= MtuBufSize)
            {
                seg.Data = _mtuBufPool.Count > 0 ? _mtuBufPool.Pop() : new byte[MtuBufSize];
            }
            else
            {
                seg.Data = new byte[size];
            }
            return seg;
        }

        private void ReturnSegment(Segment seg)
        {
            if (seg == null) return;
            byte[] buf = seg.Data;
            seg.Data = Array.Empty<byte>();
            seg.DataLength = 0;

            if (buf != null)
            {
                if (buf.Length == SmallBufSize && _smallBufPool.Count < MaxPooledSegments)
                    _smallBufPool.Push(buf);
                else if (buf.Length == MtuBufSize && _mtuBufPool.Count < MaxPooledSegments)
                    _mtuBufPool.Push(buf);
            }

            if (_segPool.Count < MaxPooledSegments)
                _segPool.Push(seg);
        }

        private void MoveRcvBufToQueue()
        {
            while (_rcvBuf.Count > 0)
            {
                var seg = _rcvBuf.First.Value;
                if (seg.Sn == _rcvNxt && _rcvQueue.Count < _rcvWnd)
                {
                    _rcvBuf.RemoveFirst();
                    _rcvQueue.AddLast(seg);
                    _rcvNxt++;
                }
                else
                {
                    break;
                }
            }
        }

        private ushort WndUnused()
        {
            return _rcvQueue.Count < _rcvWnd ? (ushort)(_rcvWnd - _rcvQueue.Count) : (ushort)0;
        }

        private static int EncodeHeader(byte[] buffer, int offset, uint conv, byte cmd, byte frg,
            ushort wnd, uint ts, uint sn, uint una, uint len)
        {
            ClientTransportProtocol.WriteUInt32LE(buffer, offset, conv);
            buffer[offset + 4] = cmd;
            buffer[offset + 5] = frg;
            ClientTransportProtocol.WriteUInt16LE(buffer, offset + 6, wnd);
            ClientTransportProtocol.WriteUInt32LE(buffer, offset + 8, ts);
            ClientTransportProtocol.WriteUInt32LE(buffer, offset + 12, sn);
            ClientTransportProtocol.WriteUInt32LE(buffer, offset + 16, una);
            ClientTransportProtocol.WriteUInt32LE(buffer, offset + 20, len);
            return offset + Overhead;
        }

        private static int TimeDiff(uint later, uint earlier) => unchecked((int)(later - earlier));
    }
}
#endif
