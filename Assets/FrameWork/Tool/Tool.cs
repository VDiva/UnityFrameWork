// using System;
// using System.IO;
// using System.Linq;
// using System.Security.Cryptography;
// using System.Text;
// using FrameWork.Global;
// using GameData;
// using UnityEngine;
// using NetWork;
//
// namespace FrameWork.Tool
// {
//     public class Tool
//     {
//         
//         public static string GetMd5(string path)
//         {
//             using (FileStream fs=new FileStream(path,FileMode.Open))
//             {
//                 MD5 md5 = new MD5CryptoServiceProvider();
//                 byte[] md5Info = md5.ComputeHash(fs);
//
//                 StringBuilder sb = new StringBuilder();
//                 for (int i = 0; i < md5Info.Length; i++)
//                     sb.Append(md5Info[i].ToString("x2"));
//                 return sb.ToString();
//             }
//         }
//
//         
//         
//         
//         public static byte[] Serialize(Data data)
//         {
//             using (MemoryStream ms=new MemoryStream())
//             {
//                 using (BinaryWriter bw=new BinaryWriter(ms))
//                 {
//                     var stream = data.ToByteArray();
//                     bw.Write(stream.Length);
//                     bw.Write(stream);
//                     return ms.ToArray();
//                 }
//             }
//         }
//         
//         public static bool DeSerialize(byte[] bytes,out Data data)
//         {
//             using (MemoryStream ms=new MemoryStream(bytes))
//             {
//                 using (BinaryReader br=new BinaryReader(ms))
//                 {
//                     if (bytes.Length>4)
//                     {
//                         int lenght=br.ReadInt32();
//                         Debug.Log(lenght);
//                         if (bytes.Length-4>=lenght)
//                         {
//                             var dataBytes=new byte[bytes.Length-4];
//                             Buffer.BlockCopy(bytes,4,dataBytes,0,lenght);
//                             data=Data.Parser.ParseFrom(dataBytes);
//                             return true;
//                         }
//                     }
//                 }
//             }
//             data = null;
//             return false;
//         }
//     }
// }