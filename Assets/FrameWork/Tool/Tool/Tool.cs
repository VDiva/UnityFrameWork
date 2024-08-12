using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;


namespace FrameWork
{
    public class Tool
    {


        public static object ConversionType(string type,string value)
        {
            if (type=="int")
            {
                return int.Parse(value);
            }else if (type=="string")
            {
                return value;
            }else if (type=="float")
            {
                return float.Parse(value);
            }else if (type=="long")
            {
                return long.Parse(value);
            }else if (type=="double")
            {
                return double.Parse(value);
            }else if (type=="Vector3")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }
                var v = value.Split(',');
                return new Vector3(float.Parse(v[0]),float.Parse(v[1]),float.Parse(v[2]));
            }else if (type=="Vector2")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }
                var v = value.Split(',');
                return new Vector2(float.Parse(v[0]),float.Parse(v[1]));
            }else if (type=="string[]")
            {
                if (string.IsNullOrEmpty(value))
                    return new string[]{};
                else
                    return value.Split(',');
            }else if (type=="int[]")
            {
                if (string.IsNullOrEmpty(value))
                    return new int[]{};
                else
                    return value.Split(',').Select((s => int.Parse(s) ));
            }else if (type=="float[]")
            {
                if (string.IsNullOrEmpty(value))
                    return new float[]{};
                else
                    return value.Split(',').Select((s => float.Parse(s) ));
            }

            return value;
        }
        
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
        
        
        // public static string GetAbPath()
        // {
        //     string path = "";
        //     RuntimePlatform platform = Application.platform;
        //     switch (platform)
        //     {
        //         case RuntimePlatform.WindowsEditor:
        //         case RuntimePlatform.WindowsPlayer:
        //
        //             if (Directory.Exists(Application.streamingAssetsPath+"/StandaloneWindows64/"))
        //             {
        //                 path = Application.streamingAssetsPath+"/StandaloneWindows64/";
        //             }
        //             else
        //             {
        //                 path = Application.persistentDataPath + "/StandaloneWindows64/";
        //             }
        //             
        //             break;
        //         case RuntimePlatform.Android:
        //             path = Application.persistentDataPath + "/Android/";
        //             break;
        //         case RuntimePlatform.IPhonePlayer:
        //             path = Application.persistentDataPath + "/Ios/";
        //             break;
        //         case RuntimePlatform.WebGLPlayer:
        //             path = Application.persistentDataPath + "/WebGl/";
        //             break;
        //     }
        //
        //     return path;
        // }

        // public static string GetAbDictoryPath()
        // {
        //     string path = "";
        //     RuntimePlatform platform = Application.platform;
        //     switch (platform)
        //     {
        //         case RuntimePlatform.WindowsEditor:
        //         case RuntimePlatform.WindowsPlayer:
        //
        //             path = "/StandaloneWindows64/";
        //
        //             break;
        //         case RuntimePlatform.Android:
        //             path =  "/Android/";
        //             break;
        //         case RuntimePlatform.IPhonePlayer:
        //             path ="/Ios/";
        //             break;
        //         case RuntimePlatform.WebGLPlayer:
        //             path = "/WebGl/";
        //             break;
        //     }
        //
        //     return path;
        // }
        
        
        // public static string GetAbPath(RuntimePlatform platform)
        // {
        //     string path = "";
        //     //RuntimePlatform platform = Application.platform;
        //     switch (platform)
        //     {
        //         case RuntimePlatform.WindowsEditor:
        //         case RuntimePlatform.WindowsPlayer:
        //
        //             if (Directory.Exists(Application.streamingAssetsPath + "/StandaloneWindows64/"))
        //             {
        //                 path = Application.streamingAssetsPath + "/StandaloneWindows64/";
        //             }
        //             else
        //             {
        //                 path = Application.persistentDataPath + "/StandaloneWindows64/";
        //             }
        //
        //             break;
        //         case RuntimePlatform.Android:
        //             path = Application.persistentDataPath + "/Android/";
        //             break;
        //         case RuntimePlatform.IPhonePlayer:
        //             path = Application.persistentDataPath + "/Ios/";
        //             break;
        //     }
        //
        //     return path;
        // }
        
        // public static void CopyAb(string path,string path2,string key)
        // {
        //     var data = File.ReadAllBytes(path);
        //     var encryptData=Encrypt(data, key);
        //     var newName="encrypt"+Path.GetFileName(path);
        //     File.WriteAllBytes(path2+"/"+newName,encryptData);
        // }
        
        
        //private string key = "kljsdkkdlo4454GG00155sajuklmbkdl";
        
        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="toEncryptArray">明文</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static byte[] Encrypt(byte[] toEncryptArray, string key)
        {
            //byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);        
            byte[] keyArray = Convert.FromBase64String(key);        
            RijndaelManaged rDel = new RijndaelManaged();
            rDel.Key = keyArray;
            rDel.Mode = CipherMode.ECB;
            rDel.Padding = PaddingMode.PKCS7;
            ICryptoTransform cTransform = rDel.CreateEncryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return resultArray;
        }
 
        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="toEncryptArray">密文</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static byte[] Decrypt(byte[] toEncryptArray, string key)
        {
            byte[] keyArray = Convert.FromBase64String(key);        
            RijndaelManaged rDel = new RijndaelManaged();
            rDel.Key = keyArray;
            rDel.Mode = CipherMode.ECB;
            rDel.Padding = PaddingMode.PKCS7;
            ICryptoTransform cTransform = rDel.CreateDecryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return resultArray;
        }


        public static bool IsAndroid()
        {
            return CheckPlatform(RuntimePlatform.Android);
        }

        public static bool IsWindows()
        {
            return CheckPlatform(RuntimePlatform.WindowsEditor) || CheckPlatform(RuntimePlatform.WindowsPlayer);
        }

        public static bool CheckPlatform(RuntimePlatform runtimePlatform)
        {
            return Application.platform == runtimePlatform;
        }


        public static Type ByClassNameGetType(string className)
        {
            var assembly=Assembly.GetExecutingAssembly();
            var type = assembly.GetType($"FrameWork.{className}.{className}");
            return type;
        }

    }
}