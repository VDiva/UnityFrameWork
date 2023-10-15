using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameData;
using Google.Protobuf;

namespace NetWork.Tool
{
    public class Tool
    {
    
        public static byte[] Serialize(Data data)
        {
            using (MemoryStream ms=new MemoryStream())
            {
                using (BinaryWriter bw=new BinaryWriter(ms))
                {
                    var stream = data.ToByteArray();
                    bw.Write(stream.Length);
                    bw.Write(stream);
                    return ms.ToArray();
                }
            }
        }
        
        public static bool DeSerialize(byte[] bytes,out Data data)
        {
            using (MemoryStream ms=new MemoryStream(bytes))
            {
                using (BinaryReader br=new BinaryReader(ms))
                {
                    if (bytes.Length>4)
                    {
                        int lenght=br.ReadInt32();
                        
                        if (bytes.Length-4>=lenght)
                        {
                            var dataBytes=new byte[lenght];
                            Buffer.BlockCopy(bytes,4,dataBytes,0,lenght);
                            data=Data.Parser.ParseFrom(dataBytes);
                            return true;
                        }
                    }
                }
            }
            data = null;
            return false;
        }
    }
}