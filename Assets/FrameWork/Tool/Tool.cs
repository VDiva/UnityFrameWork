using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FrameWork.Global;
using UnityEngine;

namespace FrameWork.Tool
{
    public class Tool
    {
        
        public static string GetMd5(string path)
        {
            using (FileStream fs=new FileStream(path,FileMode.Open))
            {
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] md5Info = md5.ComputeHash(fs);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < md5Info.Length; i++)
                    sb.Append(md5Info[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static byte[] Serialize<T>(T obj)
        {
            try
            {
                using (MemoryStream ms=new MemoryStream())
                {
                    ProtoBuf.Serializer.Serialize<T>(ms,obj);
                    byte[] result = new byte[ms.Length];
                    ms.Position = 0;
                    ms.Read(result, 0, result.Length);
                    return result;
                }
            }
            catch (Exception e)
            {
                Debug.Log("序列化失败:"+e);
                return null;
            }
        }



        public static T DeSerialize<T>(byte[] data)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(data, 0, data.Length);
                    ms.Position = 0;
                    T result = ProtoBuf.Serializer.Deserialize<T>(ms);
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("反序列化失败: " + ex.ToString());
                return (T)default;
            }
        }
    }
}