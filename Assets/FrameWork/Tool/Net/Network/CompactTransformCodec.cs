using System;
using System.Collections.Generic;
using System.IO;
using GameData;
using Google.Protobuf;

namespace FrameWork.Net.Network
{
    /// <summary>
    /// 高频 Transform 位压缩量化编解码器（前后端线格式严格一致）：
    /// - Smallest-Three 32 位四元数压缩（16B -> 4B，单位四元数 0B）；
    /// - IEEE-754 半精度（FP16）速度与缩放压缩（12B -> 6B，零速度/单位缩放 0B）；
    /// - Varint ObjectId / Sequence 与批次共享 RoomId / SceneEpoch / SentTime 基准。
    /// </summary>
    public static class CompactTransformCodec
    {
        public const byte MagicVersion = 0xC1;
        public const int MaxBatchCount = 256;

        private const byte HeaderHasBaseTime = 0x01;

        private const byte FlagHasPos = 0x01;
        private const byte FlagHasRot = 0x02;
        private const byte FlagHasScaleFlag = 0x04;
        private const byte FlagUniformScale = 0x08;
        private const byte FlagNonUniformScale = 0x10;
        private const byte FlagHasVelFlag = 0x20;
        private const byte FlagNonZeroVel = 0x40;
        private const byte FlagHasExtra = 0x80;

        private const byte ExtraExactBaseTime = 0x01;
        private const byte ExtraOffsetTime = 0x02;
        private const byte ExtraHasChildren = 0x04;

        private const float Sqrt2 = 1.41421356237f;
        private const float InvSqrt2 = 0.70710678118f;

        [ThreadStatic] private static MemoryStream _tlsStream;
        [ThreadStatic] private static BinaryWriter _tlsWriter;

        public static ByteString EncodeSingle(NetworkTransformData item)
        {
            if (item == null) return ByteString.Empty;
            BinaryWriter w = AcquireWriter(out MemoryStream ms);
            w.Write(MagicVersion);
            bool validTime = IsFinitePositive(item.SentTime);
            w.Write(validTime ? HeaderHasBaseTime : (byte)0);
            WriteVarUInt32(w, item.RoomId);
            WriteVarUInt32(w, item.SceneEpoch);
            if (validTime) w.Write(item.SentTime);
            WriteVarUInt32(w, 1u);
            WriteItem(w, item, validTime ? item.SentTime : 0.0, validTime);
            w.Flush();
            return ByteString.CopyFrom(ms.GetBuffer(), 0, (int)ms.Length);
        }

        public static ByteString EncodeBatch(IReadOnlyList<NetworkTransformData> items)
        {
            if (items == null || items.Count == 0) return ByteString.Empty;
            int count = Math.Min(items.Count, MaxBatchCount);
            uint roomId = items[0].RoomId;
            uint sceneEpoch = items[0].SceneEpoch;
            double baseTime = 0.0;
            bool hasBaseTime = false;
            for (int i = 0; i < count; i++)
            {
                if (IsFinitePositive(items[i].SentTime))
                {
                    baseTime = items[i].SentTime;
                    hasBaseTime = true;
                    break;
                }
            }

            BinaryWriter w = AcquireWriter(out MemoryStream ms);
            w.Write(MagicVersion);
            w.Write(hasBaseTime ? HeaderHasBaseTime : (byte)0);
            WriteVarUInt32(w, roomId);
            WriteVarUInt32(w, sceneEpoch);
            if (hasBaseTime) w.Write(baseTime);
            WriteVarUInt32(w, (uint)count);

            for (int i = 0; i < count; i++)
                WriteItem(w, items[i], baseTime, hasBaseTime);

            w.Flush();
            return ByteString.CopyFrom(ms.GetBuffer(), 0, (int)ms.Length);
        }

        public static bool TryDecode(ByteString payload, List<NetworkTransformData> output)
        {
            output.Clear();
            if (payload == null || payload.Length < 5)
                return false;

            ReadOnlySpan<byte> span = payload.Span;
            int pos = 0;
            if (span[pos++] != MagicVersion)
                return false;

            byte headerFlags = span[pos++];
            if (!TryReadVarUInt32(span, ref pos, out uint roomId) ||
                !TryReadVarUInt32(span, ref pos, out uint sceneEpoch))
                return false;

            double baseTime = 0.0;
            if ((headerFlags & HeaderHasBaseTime) != 0)
            {
                if (pos + 8 > span.Length) return false;
                baseTime = ReadDoubleLE(span, ref pos);
            }

            if (!TryReadVarUInt32(span, ref pos, out uint count) || count == 0 || count > MaxBatchCount)
                return false;

            for (int i = 0; i < (int)count; i++)
            {
                if (!TryReadItem(span, ref pos, roomId, sceneEpoch, baseTime, out NetworkTransformData item))
                {
                    output.Clear();
                    return false;
                }
                output.Add(item);
            }

            return pos == span.Length;
        }

        private static void WriteItem(BinaryWriter w, NetworkTransformData d, double baseTime, bool hasBaseTime)
        {
            WriteVarUInt32(w, d.ObjectId);
            WriteVarUInt64(w, d.Sequence);

            bool hasPos = d.PositionX != 0f || d.PositionY != 0f || d.PositionZ != 0f;
            bool hasRot = !IsIdentityQuat(d.RotationX, d.RotationY, d.RotationZ, d.RotationW);

            byte flags = 0;
            if (hasPos) flags |= FlagHasPos;
            if (hasRot) flags |= FlagHasRot;

            if (d.HasScale)
            {
                flags |= FlagHasScaleFlag;
                bool isOne = NearlyEqual(d.ScaleX, 1f) && NearlyEqual(d.ScaleY, 1f) && NearlyEqual(d.ScaleZ, 1f);
                if (!isOne)
                {
                    if (NearlyEqual(d.ScaleX, d.ScaleY) && NearlyEqual(d.ScaleY, d.ScaleZ))
                        flags |= FlagUniformScale;
                    else
                        flags |= FlagNonUniformScale;
                }
            }

            if (d.HasVelocity)
            {
                flags |= FlagHasVelFlag;
                if (Math.Abs(d.VelocityX) > 0.0005f || Math.Abs(d.VelocityY) > 0.0005f || Math.Abs(d.VelocityZ) > 0.0005f)
                    flags |= FlagNonZeroVel;
            }

            byte extra = 0;
            float timeOffset = 0f;
            if (hasBaseTime && IsFinitePositive(d.SentTime))
            {
                double diff = d.SentTime - baseTime;
                if (Math.Abs(diff) < 0.00001)
                    extra |= ExtraExactBaseTime;
                else
                {
                    extra |= ExtraOffsetTime;
                    timeOffset = (float)diff;
                }
            }
            if (d.Children.Count > 0)
                extra |= ExtraHasChildren;

            if (extra != 0)
                flags |= FlagHasExtra;

            w.Write(flags);
            if (extra != 0)
                w.Write(extra);

            if (hasPos)
            {
                w.Write(d.PositionX);
                w.Write(d.PositionY);
                w.Write(d.PositionZ);
            }

            if (hasRot)
                w.Write(PackSmallestThree(d.RotationX, d.RotationY, d.RotationZ, d.RotationW));

            if ((flags & FlagUniformScale) != 0)
            {
                w.Write(FloatToHalf(d.ScaleX));
            }
            else if ((flags & FlagNonUniformScale) != 0)
            {
                w.Write(FloatToHalf(d.ScaleX));
                w.Write(FloatToHalf(d.ScaleY));
                w.Write(FloatToHalf(d.ScaleZ));
            }

            if ((flags & FlagNonZeroVel) != 0)
            {
                w.Write(FloatToHalf(d.VelocityX));
                w.Write(FloatToHalf(d.VelocityY));
                w.Write(FloatToHalf(d.VelocityZ));
            }

            if ((extra & ExtraOffsetTime) != 0)
                w.Write(timeOffset);

            if ((extra & ExtraHasChildren) != 0)
            {
                int childCount = Math.Min(d.Children.Count, 16);
                w.Write((byte)childCount);
                for (int i = 0; i < childCount; i++)
                {
                    NetworkChildTransformData c = d.Children[i];
                    w.Write((byte)Math.Max(0, Math.Min(255, c.SyncIndex)));
                    w.Write(c.PositionX);
                    w.Write(c.PositionY);
                    w.Write(c.PositionZ);
                    w.Write(PackSmallestThree(c.RotationX, c.RotationY, c.RotationZ, c.RotationW));
                    w.Write(FloatToHalf(c.ScaleX));
                    w.Write(FloatToHalf(c.ScaleY));
                    w.Write(FloatToHalf(c.ScaleZ));
                }
            }
        }

        private static bool TryReadItem(
            ReadOnlySpan<byte> span,
            ref int pos,
            uint roomId,
            uint sceneEpoch,
            double baseTime,
            out NetworkTransformData d)
        {
            d = null;
            if (!TryReadVarUInt32(span, ref pos, out uint objectId) ||
                !TryReadVarUInt64(span, ref pos, out ulong sequence) ||
                pos >= span.Length)
                return false;

            byte flags = span[pos++];
            byte extra = 0;
            if ((flags & FlagHasExtra) != 0)
            {
                if (pos >= span.Length) return false;
                extra = span[pos++];
            }

            d = new NetworkTransformData
            {
                ObjectId = objectId,
                RoomId = roomId,
                SceneEpoch = sceneEpoch,
                Sequence = sequence,
                RotationW = 1f
            };

            if ((flags & FlagHasPos) != 0)
            {
                if (pos + 12 > span.Length) return false;
                d.PositionX = ReadSingleLE(span, ref pos);
                d.PositionY = ReadSingleLE(span, ref pos);
                d.PositionZ = ReadSingleLE(span, ref pos);
            }

            if ((flags & FlagHasRot) != 0)
            {
                if (pos + 4 > span.Length) return false;
                uint packedRot = ReadUInt32LE(span, ref pos);
                UnpackSmallestThree(packedRot, out float rx, out float ry, out float rz, out float rw);
                d.RotationX = rx;
                d.RotationY = ry;
                d.RotationZ = rz;
                d.RotationW = rw;
            }

            if ((flags & FlagHasScaleFlag) != 0)
            {
                d.HasScale = true;
                if ((flags & FlagUniformScale) != 0)
                {
                    if (pos + 2 > span.Length) return false;
                    float s = HalfToFloat(ReadUInt16LE(span, ref pos));
                    d.ScaleX = d.ScaleY = d.ScaleZ = s;
                }
                else if ((flags & FlagNonUniformScale) != 0)
                {
                    if (pos + 6 > span.Length) return false;
                    d.ScaleX = HalfToFloat(ReadUInt16LE(span, ref pos));
                    d.ScaleY = HalfToFloat(ReadUInt16LE(span, ref pos));
                    d.ScaleZ = HalfToFloat(ReadUInt16LE(span, ref pos));
                }
                else
                {
                    d.ScaleX = d.ScaleY = d.ScaleZ = 1f;
                }
            }

            if ((flags & FlagHasVelFlag) != 0)
            {
                d.HasVelocity = true;
                if ((flags & FlagNonZeroVel) != 0)
                {
                    if (pos + 6 > span.Length) return false;
                    d.VelocityX = HalfToFloat(ReadUInt16LE(span, ref pos));
                    d.VelocityY = HalfToFloat(ReadUInt16LE(span, ref pos));
                    d.VelocityZ = HalfToFloat(ReadUInt16LE(span, ref pos));
                }
            }

            if ((extra & ExtraExactBaseTime) != 0)
            {
                d.SentTime = baseTime;
            }
            else if ((extra & ExtraOffsetTime) != 0)
            {
                if (pos + 4 > span.Length) return false;
                d.SentTime = baseTime + ReadSingleLE(span, ref pos);
            }

            if ((extra & ExtraHasChildren) != 0)
            {
                if (pos >= span.Length) return false;
                int childCount = span[pos++];
                if (childCount > 16 || pos + childCount * 23 > span.Length) return false;
                for (int i = 0; i < childCount; i++)
                {
                    int syncIndex = span[pos++];
                    float cx = ReadSingleLE(span, ref pos);
                    float cy = ReadSingleLE(span, ref pos);
                    float cz = ReadSingleLE(span, ref pos);
                    UnpackSmallestThree(ReadUInt32LE(span, ref pos), out float crx, out float cry, out float crz, out float crw);
                    float csx = HalfToFloat(ReadUInt16LE(span, ref pos));
                    float csy = HalfToFloat(ReadUInt16LE(span, ref pos));
                    float csz = HalfToFloat(ReadUInt16LE(span, ref pos));
                    d.Children.Add(new NetworkChildTransformData
                    {
                        SyncIndex = syncIndex,
                        PositionX = cx,
                        PositionY = cy,
                        PositionZ = cz,
                        RotationX = crx,
                        RotationY = cry,
                        RotationZ = crz,
                        RotationW = crw,
                        ScaleX = csx,
                        ScaleY = csy,
                        ScaleZ = csz
                    });
                }
            }

            return true;
        }

        public static uint PackSmallestThree(float x, float y, float z, float w)
        {
            float lenSq = x * x + y * y + z * z + w * w;
            if (lenSq <= 0.0000001f || float.IsNaN(lenSq) || float.IsInfinity(lenSq))
                return 3u << 30 | (512u << 20) | (512u << 10) | 512u;

            float invLen = 1f / (float)Math.Sqrt(lenSq);
            x *= invLen; y *= invLen; z *= invLen; w *= invLen;

            int maxIdx = 0;
            float maxAbs = Math.Abs(x);
            float ay = Math.Abs(y), az = Math.Abs(z), aw = Math.Abs(w);
            if (ay > maxAbs) { maxIdx = 1; maxAbs = ay; }
            if (az > maxAbs) { maxIdx = 2; maxAbs = az; }
            if (aw > maxAbs) { maxIdx = 3; }

            float sign = (maxIdx == 0 ? x : maxIdx == 1 ? y : maxIdx == 2 ? z : w) < 0f ? -1f : 1f;
            x *= sign; y *= sign; z *= sign; w *= sign;

            float a, b, c;
            switch (maxIdx)
            {
                case 0: a = y; b = z; c = w; break;
                case 1: a = x; b = z; c = w; break;
                case 2: a = x; b = y; c = w; break;
                default: a = x; b = y; c = z; break;
            }

            uint qa = Quantize10(a);
            uint qb = Quantize10(b);
            uint qc = Quantize10(c);
            return ((uint)maxIdx << 30) | (qa << 20) | (qb << 10) | qc;
        }

        public static void UnpackSmallestThree(uint packed, out float x, out float y, out float z, out float w)
        {
            uint maxIdx = packed >> 30;
            float a = Dequantize10((packed >> 20) & 0x3FFu);
            float b = Dequantize10((packed >> 10) & 0x3FFu);
            float c = Dequantize10(packed & 0x3FFu);
            float sumSq = a * a + b * b + c * c;
            float d = sumSq < 1f ? (float)Math.Sqrt(1f - sumSq) : 0f;

            switch (maxIdx)
            {
                case 0: x = d; y = a; z = b; w = c; break;
                case 1: x = a; y = d; z = b; w = c; break;
                case 2: x = a; y = b; z = d; w = c; break;
                default: x = a; y = b; z = c; w = d; break;
            }
        }

        private static uint Quantize10(float v)
        {
            float norm = (v * Sqrt2 + 1f) * 0.5f;
            if (norm <= 0f) return 0u;
            if (norm >= 1f) return 1023u;
            return (uint)(norm * 1023f + 0.5f);
        }

        private static float Dequantize10(uint q) => ((q / 1023f) * 2f - 1f) * InvSqrt2;

        public static ushort FloatToHalf(float value)
        {
            uint fbits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
            uint sign = (fbits >> 16) & 0x8000u;
            int exp = (int)((fbits >> 23) & 0xFFu) - 127 + 15;
            uint mantissa = fbits & 0x007FFFFFu;

            if (exp <= 0)
            {
                if (exp < -10) return (ushort)sign;
                mantissa = (mantissa | 0x00800000u) >> (1 - exp);
                return (ushort)(sign | (mantissa >> 13));
            }
            if (exp >= 31)
                return (ushort)(sign | 0x7BFFu);
            return (ushort)(sign | ((uint)exp << 10) | (mantissa >> 13));
        }

        public static float HalfToFloat(ushort half)
        {
            uint sign = ((uint)half & 0x8000u) << 16;
            uint exp = ((uint)half >> 10) & 0x1Fu;
            uint mantissa = (uint)half & 0x03FFu;

            if (exp == 0)
            {
                if (mantissa == 0) return BitConverter.Int32BitsToSingle(unchecked((int)sign));
                while ((mantissa & 0x0400u) == 0)
                {
                    mantissa <<= 1;
                    exp--;
                }
                exp++;
                mantissa &= ~0x0400u;
            }
            else if (exp == 31)
            {
                uint infNan = sign | 0x7F800000u | (mantissa << 13);
                return BitConverter.Int32BitsToSingle(unchecked((int)infNan));
            }

            uint fullExp = (exp + (127 - 15)) << 23;
            uint fullBits = sign | fullExp | (mantissa << 13);
            return BitConverter.Int32BitsToSingle(unchecked((int)fullBits));
        }

        private static BinaryWriter AcquireWriter(out MemoryStream stream)
        {
            if (_tlsStream == null)
            {
                _tlsStream = new MemoryStream(256);
                _tlsWriter = new BinaryWriter(_tlsStream);
            }
            _tlsStream.SetLength(0);
            _tlsStream.Position = 0;
            stream = _tlsStream;
            return _tlsWriter;
        }

        private static void WriteVarUInt32(BinaryWriter w, uint v)
        {
            while (v >= 0x80u) { w.Write((byte)(v | 0x80u)); v >>= 7; }
            w.Write((byte)v);
        }

        private static void WriteVarUInt64(BinaryWriter w, ulong v)
        {
            while (v >= 0x80UL) { w.Write((byte)(v | 0x80UL)); v >>= 7; }
            w.Write((byte)v);
        }

        private static bool TryReadVarUInt32(ReadOnlySpan<byte> s, ref int pos, out uint value)
        {
            value = 0;
            int shift = 0;
            while (pos < s.Length && shift < 35)
            {
                byte b = s[pos++];
                value |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return true;
                shift += 7;
            }
            return false;
        }

        private static bool TryReadVarUInt64(ReadOnlySpan<byte> s, ref int pos, out ulong value)
        {
            value = 0;
            int shift = 0;
            while (pos < s.Length && shift < 70)
            {
                byte b = s[pos++];
                value |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return true;
                shift += 7;
            }
            return false;
        }

        private static ushort ReadUInt16LE(ReadOnlySpan<byte> s, ref int pos)
        {
            ushort v = (ushort)(s[pos] | (s[pos + 1] << 8));
            pos += 2;
            return v;
        }

        private static uint ReadUInt32LE(ReadOnlySpan<byte> s, ref int pos)
        {
            uint v = (uint)(s[pos] | (s[pos + 1] << 8) | (s[pos + 2] << 16) | (s[pos + 3] << 24));
            pos += 4;
            return v;
        }

        private static float ReadSingleLE(ReadOnlySpan<byte> s, ref int pos) =>
            BitConverter.Int32BitsToSingle(unchecked((int)ReadUInt32LE(s, ref pos)));

        private static double ReadDoubleLE(ReadOnlySpan<byte> s, ref int pos)
        {
            ulong lo = ReadUInt32LE(s, ref pos);
            ulong hi = ReadUInt32LE(s, ref pos);
            return BitConverter.Int64BitsToDouble(unchecked((long)(lo | (hi << 32))));
        }

        private static bool IsIdentityQuat(float x, float y, float z, float w) =>
            Math.Abs(x) < 0.0002f && Math.Abs(y) < 0.0002f && Math.Abs(z) < 0.0002f && Math.Abs(Math.Abs(w) - 1f) < 0.0002f;

        private static bool NearlyEqual(float a, float b) => Math.Abs(a - b) < 0.0005f;

        private static bool IsFinitePositive(double v) => v > 0.0 && !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
