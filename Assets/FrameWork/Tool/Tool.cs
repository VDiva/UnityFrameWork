using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FrameWork.Global;
using GameData;
using UnityEngine;
using NetWork;

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
        
        public static long ConvertDateTimep(DateTime time)
        {
            return ((time.ToUniversalTime().Ticks - 621355968000000000) / 10000000);
            //等价于：
            //return ((time.ToUniversalTime().Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks) / 10000000) * 1000;
        }

    }
}